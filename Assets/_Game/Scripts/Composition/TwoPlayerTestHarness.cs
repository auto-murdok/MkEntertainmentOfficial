using System;
using Unity.Multiplayer.Tools.NetworkSimulator.Runtime;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

// Two-player proof-test harness (development/test builds only — it activates
// solely when the process is launched with -testrole; normal launches are
// completely unaffected).
//
//   -testrole target   : this peer hosts, teleports in front of the client,
//                        strafes side to side, and faces the shooter.
//   -testrole shooter  : this peer joins (-mlclient not needed; JoinGame is
//                        called directly) and plays an aim-bot on the real
//                        input surface (setLookInput / setIsAiming /
//                        HandleShoot) against the target.
//   -testrole cheater  : joins, then attacks the server validator directly
//                        through NetworkedShooter.DebugSubmitShotForTest —
//                        20 shots/s (the legal rate is 5/s), then a burst of
//                        origin-spoofed requests — before finishing with
//                        honest FSM fire.
//
// Both peers render an evidence HUD (role, measured RTT, server verdict
// counters, target HP) so the screen recordings are self-evidencing.
// A ZombieSpawner in the scene is disabled: a controlled range.
public class TwoPlayerTestHarness : MonoBehaviour
{
    private enum Role { None, Target, Shooter, Cheater }

    private const float ScenarioLength = 46f;
    private const float FireInterval = 0.8f;
    private const float StrafeFlipInterval = 1.1f;
    private const float AimToleranceDegrees = 3.5f;

    private Role _role = Role.None;
    private float _simDelayMs;
    private bool _menuDriven;
    private bool _rangeReady;
    private bool _spawnerDisabled;
    private float _t0 = -1f;
    private float _nextShotTime;
    private float _nextStrafeFlip;
    private float _strafeSign = 1f;
    private uint _rawShotId = 100000;
    private int _honestShotsFired;
    private int _rawShotsSent;
    private bool _targetDead;
    private string _phase = "boot";
    private int _framesSinceFpsLog;
    private float _lastFpsLogAt;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        string role = GetArg("-testrole");
        if (string.IsNullOrEmpty(role))
        {
            return; // normal launch: the harness does not exist
        }

        GameObject host = new GameObject(nameof(TwoPlayerTestHarness));
        DontDestroyOnLoad(host);
        host.AddComponent<TwoPlayerTestHarness>().Init(role);
    }

    private static string GetArg(string name)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }
        return null;
    }

    private static float GetFloatArg(string name, float fallback)
    {
        string value = GetArg(name);
        return value != null && float.TryParse(value, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float parsed)
            ? parsed
            : fallback;
    }

    private void Init(string role)
    {
        _role = role switch
        {
            "target" => Role.Target,
            "shooter" => Role.Shooter,
            "cheater" => Role.Cheater,
            _ => Role.None,
        };
        _simDelayMs = GetFloatArg("-simdelay", 0f);
        Application.runInBackground = true;
        // Two game instances share this machine with the Editor: run the
        // test builds at the lowest quality tier so the render load stays
        // honest. (Also makes any fps collapse in the log unambiguous.)
        QualitySettings.SetQualityLevel(0, applyExpensiveChanges: true);
        Debug.Log($"[TPTEST] harness active, role={_role}, simDelay={_simDelayMs}ms, quality={QualitySettings.names[QualitySettings.GetQualityLevel()]}");

        if (_simDelayMs > 0f)
        {
            // Latency injection for THIS peer only (research rule: simulator
            // delay is bi-directional per peer — putting it on both peers
            // would double the intended round trip). Created before the
            // session connects; NEVER UnityTransport's obsolete debug
            // simulator, which is a no-op in NGO 2.13.
            GameObject simObject = new GameObject("NetworkSimulator");
            DontDestroyOnLoad(simObject);
            NetworkSimulator simulator = simObject.AddComponent<NetworkSimulator>();
            float jitter = GetFloatArg("-simjitter", 5f);
            simulator.ConnectionPreset = NetworkSimulatorPreset.Create(
                $"TPTEST {_simDelayMs:F0}ms",
                "Two-player test harness CLI preset",
                packetDelayMs: Mathf.RoundToInt(_simDelayMs),
                packetJitterMs: Mathf.RoundToInt(jitter),
                packetLossInterval: 0,
                packetLossPercent: 0);
            Debug.Log($"[TPTEST] NetworkSimulator preset applied: {_simDelayMs:F0}ms delay, {jitter:F0}ms jitter");
        }
    }

    private void Update()
    {
        if (_role == Role.None)
        {
            return;
        }

        // Render-rate evidence: if a capture ever looks frozen, this log
        // says whether the game itself stopped presenting frames.
        _framesSinceFpsLog++;
        if (Time.unscaledTime - _lastFpsLogAt >= 5f)
        {
            float span = Time.unscaledTime - _lastFpsLogAt;
            if (_lastFpsLogAt > 0f)
            {
                Debug.Log($"[TPTEST] fps {(_framesSinceFpsLog / span):F1} (phase {_phase})");
            }
            _framesSinceFpsLog = 0;
            _lastFpsLogAt = Time.unscaledTime;
        }

        // Quit check at the very top: after the host finishes and leaves,
        // this peer's NetworkManager stops listening and every guard below
        // would starve the quit forever.
        if (_rangeReady && Time.unscaledTime - _t0 >= ScenarioLength)
        {
            Debug.Log("[TPTEST] scenario complete, quitting");
            Application.Quit();
            return;
        }

        if (!_menuDriven)
        {
            DriveMenu();
            return;
        }

        NetworkManager nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsListening || nm.LocalClient == null || nm.LocalClient.PlayerObject == null)
        {
            _phase = "connecting";
            return;
        }

        if (!_rangeReady)
        {
            SetupRange(nm);
            return;
        }

        float t = Time.unscaledTime - _t0;

        if (!_spawnerDisabled)
        {
            ZombieSpawner spawner = FindFirstObjectByType<ZombieSpawner>();
            if (spawner != null)
            {
                spawner.gameObject.SetActive(false);
                _spawnerDisabled = true;
                Debug.Log("[TPTEST] ZombieSpawner disabled (controlled range)");
            }
        }

        NetworkedShooter mine = FindShooter(nm, wantMine: true);
        NetworkedShooter theirs = FindShooter(nm, wantMine: false);
        if (mine == null || theirs == null)
        {
            _phase = "waiting for both players";
            return;
        }

        CharacterLocomotion myLocomotion = mine.GetComponent<CharacterLocomotion>();
        ActorBrainBase theirBrain = theirs.GetComponent<ActorBrainBase>();

        switch (_role)
        {
            case Role.Target:
                RunTarget(t, myLocomotion, theirs);
                break;
            case Role.Shooter:
                RunShooter(t, myLocomotion, theirs, theirBrain, cheater: false);
                break;
            case Role.Cheater:
                RunShooter(t, myLocomotion, theirs, theirBrain, cheater: true);
                break;
        }
    }

    private void DriveMenu()
    {
        if (SceneManager.GetActiveScene().name != "MainMenu")
        {
            return;
        }

        MainMenuController menu = FindFirstObjectByType<MainMenuController>();
        if (menu == null)
        {
            return;
        }

        _menuDriven = true;
        if (_role == Role.Target)
        {
            Debug.Log("[TPTEST] menu -> HostGame()");
            menu.HostGame();
        }
        else
        {
            Debug.Log("[TPTEST] menu -> JoinGame()");
            menu.JoinGame();
        }
    }

    private void SetupRange(NetworkManager nm)
    {
        NetworkedShooter mine = FindShooter(nm, wantMine: true);
        NetworkedShooter theirs = FindShooter(nm, wantMine: false);
        if (mine == null || theirs == null)
        {
            _phase = "waiting for both players";
            return;
        }

        if (_role == Role.Target)
        {
            // Stand 9 m down +Z from wherever the client spawned; the client
            // aim-bot handles its own facing, the strafe runs along X.
            Vector3 anchor = theirs.transform.position;
            Vector3 pos = mine.transform.position;
            mine.transform.position = new Vector3(anchor.x, pos.y, anchor.z + 9f);
            Debug.Log($"[TPTEST] target teleported to {mine.transform.position}");
        }

        ActorBrainBase theirBrain = theirs.GetComponent<ActorBrainBase>();
        if (theirBrain != null)
        {
            theirBrain.Died += () =>
            {
                _targetDead = true;
                Debug.Log("[TPTEST] TARGET DIED");
            };
        }

        _t0 = Time.unscaledTime;
        _nextShotTime = _t0 + 3f;
        _nextStrafeFlip = _t0 + 2f;
        _rangeReady = true;
        _phase = "range ready";
        Debug.Log("[TPTEST] range ready, scenario clock started");
    }

    private static NetworkedShooter FindShooter(NetworkManager nm, bool wantMine)
    {
        foreach (NetworkedShooter shooter in FindObjectsByType<NetworkedShooter>(FindObjectsSortMode.None))
        {
            if (shooter.IsOwner == wantMine)
            {
                return shooter;
            }
        }
        return null;
    }

    // --- Roles --------------------------------------------------------------

    private void RunTarget(float t, CharacterLocomotion myLocomotion, NetworkedShooter theirs)
    {
        _phase = _targetDead ? "target dead" : "strafing";
        if (_targetDead || myLocomotion == null)
        {
            return;
        }

        // Face the shooter so the strafe is lateral to their view.
        FaceTarget(theirs.transform.position + Vector3.up * 1.3f, myLocomotion);

        if (t < 2f)
        {
            myLocomotion.setMovementInput(Vector2.zero);
            return;
        }

        if (Time.unscaledTime >= _nextStrafeFlip)
        {
            _strafeSign = -_strafeSign;
            _nextStrafeFlip = Time.unscaledTime + StrafeFlipInterval;
        }
        myLocomotion.setMovementInput(new Vector2(_strafeSign, 0f));
    }

    private void RunShooter(float t, CharacterLocomotion myLocomotion, NetworkedShooter theirs,
        ActorBrainBase theirBrain, bool cheater)
    {
        if (myLocomotion == null || theirs == null)
        {
            return;
        }

        Vector3 chest = theirs.transform.position + Vector3.up * 1.3f;
        float yawError = FaceTarget(chest, myLocomotion);
        myLocomotion.setIsAiming(t >= 2f);

        if (!cheater)
        {
            _phase = _targetDead ? "target dead - run complete" : "honest fire";
            if (t >= 3f && !_targetDead && Time.unscaledTime >= _nextShotTime && Mathf.Abs(yawError) < AimToleranceDegrees)
            {
                myLocomotion.HandleShoot();
                _honestShotsFired++;
                _nextShotTime = Time.unscaledTime + FireInterval;
                Debug.Log($"[TPTEST] honest shot #{_honestShotsFired} (yawErr {yawError:F1})");
            }
            return;
        }

        // Cheater script: raw requests straight at the RPC seam.
        Handgun myGun = myLocomotion.GetComponentInChildren<Handgun>();
        NetworkedShooter myShooter = myLocomotion.GetComponent<NetworkedShooter>();
        if (myGun == null || myShooter == null)
        {
            return;
        }

        if (t >= 3f && t < 7f)
        {
            _phase = "CHEAT: 20 shots/s raw requests";
            // Every frame (~60/s) is far past the legal 5/s; the server's
            // fire-rate gate is the only thing that can stop these.
            SubmitRaw(myShooter, myGun, chest, spoofOrigin: false);
        }
        else if (t >= 9f && t < 13f)
        {
            _phase = "CHEAT: origin-spoofed requests";
            if (Time.unscaledTime >= _nextShotTime)
            {
                SubmitRaw(myShooter, myGun, chest, spoofOrigin: true);
                _nextShotTime = Time.unscaledTime + 0.6f;
            }
        }
        else if (t >= 15f)
        {
            _phase = _targetDead ? "target dead - run complete" : "honest fire (after cheat)";
            if (!_targetDead && Time.unscaledTime >= _nextShotTime && Mathf.Abs(yawError) < AimToleranceDegrees)
            {
                myLocomotion.HandleShoot();
                _honestShotsFired++;
                _nextShotTime = Time.unscaledTime + FireInterval;
            }
        }
        else
        {
            _phase = "cheat pause";
        }
    }

    private void SubmitRaw(NetworkedShooter myShooter, Handgun myGun, Vector3 chest, bool spoofOrigin)
    {
        Vector3 origin = myGun.muzzleWorldPosition;
        Vector3 claimedOrigin = spoofOrigin ? origin + Vector3.up * 10f : origin;
        myShooter.DebugSubmitShotForTest(new ShotRequest
        {
            WeaponId = 1,
            ShotId = ++_rawShotId,
            Origin = claimedOrigin,
            Direction = (chest - origin).normalized,
            ClientTick = NetworkManager.Singleton.LocalTime.Tick,
            Seed = _rawShotId * 2654435761u,
        });
        _rawShotsSent++;
    }

    // Proportional look controller on the production input surface. Returns
    // the remaining yaw error in degrees.
    private float FaceTarget(Vector3 worldPoint, CharacterLocomotion locomotion)
    {
        Camera cam = Camera.main;
        if (cam == null || locomotion == null)
        {
            return 180f;
        }

        Vector3 toTarget = worldPoint - cam.transform.position;
        float desiredYaw = Mathf.Atan2(toTarget.x, toTarget.z) * Mathf.Rad2Deg;
        float error = Mathf.DeltaAngle(cam.transform.eulerAngles.y, desiredYaw);
        float input = Mathf.Clamp(error * 0.35f, -7f, 7f);
        locomotion.setLookInput(new Vector2(input, 0f), "Keyboard&Mouse");
        return error;
    }

    // --- Evidence HUD --------------------------------------------------------

    private void OnGUI()
    {
        if (_role == Role.None)
        {
            return;
        }

        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 26,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white },
        };
        GUIStyle small = new GUIStyle(style) { fontSize = 20 };

        string roleLine = _role == Role.Target
            ? "HOST (server) — TARGET"
            : $"CLIENT — {_role.ToString().ToUpperInvariant()}";
        string simLine = _simDelayMs > 0f ? $"sim delay {_simDelayMs:F0} ms (this peer)" : "no sim delay";

        GUI.Label(new Rect(14, 10, 1100, 34), $"{roleLine}   |   {simLine}", style);
        GUI.Label(new Rect(14, 46, 1100, 30), $"phase: {_phase}", small);

        NetworkManager nm = NetworkManager.Singleton;
        if (nm != null && nm.IsListening)
        {
            if (nm.IsServer)
            {
                GUI.Label(new Rect(14, 76, 1100, 30),
                    $"server verdicts: accepted {NetworkedShooter.DebugShotsAccepted}  rejected {NetworkedShooter.DebugShotsRejected}  last rejection: {NetworkedShooter.DebugLastRejection}",
                    small);
            }
            else if (nm.NetworkConfig.NetworkTransport is UnityTransport transport)
            {
                ulong rtt = transport.GetCurrentRtt(NetworkManager.ServerClientId);
                GUI.Label(new Rect(14, 76, 1100, 30),
                    $"measured RTT {rtt} ms   |   honest shots {_honestShotsFired}   raw requests {_rawShotsSent}",
                    small);
            }
        }

        if (_rangeReady && !_targetDead)
        {
            NetworkedShooter theirs = nm != null ? FindShooter(nm, wantMine: false) : null;
            ActorBrainBase brain = theirs != null ? theirs.GetComponent<ActorBrainBase>() : null;
            if (brain != null)
            {
                GUI.Label(new Rect(14, 106, 1100, 30), $"other player HP {brain.remainingHitPoints:F0}", small);
            }
        }

        // Sync flash: visible on both peers in the first seconds of the clock.
        if (_rangeReady && Time.unscaledTime - _t0 < 2f)
        {
            GUIStyle banner = new GUIStyle(style) { fontSize = 54, alignment = TextAnchor.MiddleCenter };
            GUI.Label(new Rect(0, 150, Screen.width, 80), "— RUN START —", banner);
        }
    }
}
