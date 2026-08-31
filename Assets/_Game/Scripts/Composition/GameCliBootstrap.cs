using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Runtime bootstrap that makes the entire game testable via CLI args.
///
/// Created before the first scene loads (RuntimeInitializeOnLoadMethod) so
/// automated agents can drive the game without any manual UI interaction:
///
///   Builds/NetworkClient.exe --scene NetworkedCombatArena --mode host --autoQuit 30
///   Builds/NetworkClient.exe --scene NetworkedCombatArena --mode client --connect 127.0.0.1:7777 --autoQuit 30
///   Unity.exe -batchmode -projectPath . -scene ExpandedCombatArena -noSpawning -maxDuration 10 -quit
///
/// Responsibilities:
///  - Handle --help (log + quit when batchmode).
///  - Redirect to --scene if the active scene differs.
///  - Push networking overrides into <see cref="NetworkSession"/> before the
///    network bootstrap runs (bootstrap reads desiredMode in Start).
///  - Apply gameplay overrides (timeScale, seed, noSpawning etc.) after scene load.
///  - Schedule an auto-quit after --autoQuit / --maxDuration.
///
/// The object is DontDestroyOnLoad and survives scene switches.
/// </summary>
[DefaultExecutionOrder(-10000)]
public class GameCliBootstrap : MonoBehaviour
{
    private static GameCliBootstrap _instance;
    private bool _redirecting;
    private bool _autoQuitScheduled;

    // Created before any scene loads so the CLI is honoured even for the boot scene.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void CreateBootstrap()
    {
        if (_instance != null)
        {
            return;
        }
        GameObject go = new GameObject("~GameCliBootstrap");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<GameCliBootstrap>();
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
        ApplyEarly();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void ApplyEarly()
    {
        // Help.
        if (GameCliArgs.IsHelpRequested)
        {
            Debug.Log(GameCliArgs.HelpText);
            if (GameCliArgs.IsBatchMode)
            {
                QuitWithCode(0);
            }
            return;
        }

        if (GameCliArgs.IsVerbose)
        {
            Debug.Log(GameCliArgs.Dump());
        }

        // Networking overrides – must land before NetworkArenaBootstrap.Start().
        NetworkSessionMode? modeOverride = GameCliArgs.NetworkingModeOverride;
        if (modeOverride.HasValue && NetworkSession.desiredMode == NetworkSessionMode.Auto)
        {
            NetworkSession.desiredMode = modeOverride.Value;
            if (GameCliArgs.IsVerbose)
            {
                Debug.Log($"[GameCliBootstrap] NetworkSession.desiredMode overridden to {modeOverride.Value} via CLI.");
            }
        }

        // Address/port overrides (mutable fields on NetworkSession).
        string address = GameCliArgs.ConnectAddress;
        if (!string.IsNullOrEmpty(address))
        {
            NetworkSession.OverrideAddress = address;
            if (GameCliArgs.IsVerbose) Debug.Log($"[GameCliBootstrap] Network address overridden to {address}");
        }
        int? port = GameCliArgs.ConnectPort;
        if (port.HasValue)
        {
            NetworkSession.OverridePort = (ushort)port.Value;
            if (GameCliArgs.IsVerbose) Debug.Log($"[GameCliBootstrap] Network port overridden to {port.Value}");
        }

        // Seed for determinism (UnityEngine.Random – called before any Random.Range use).
        int? seed = GameCliArgs.SeedOverride;
        if (seed.HasValue)
        {
            UnityEngine.Random.InitState(seed.Value);
            if (GameCliArgs.IsVerbose) Debug.Log($"[GameCliBootstrap] Random seed set to {seed.Value}");
        }

        // Time scale.
        float? timeScale = GameCliArgs.TimeScaleOverride;
        if (timeScale.HasValue)
        {
            Time.timeScale = timeScale.Value;
            if (GameCliArgs.IsVerbose) Debug.Log($"[GameCliBootstrap] Time.timeScale set to {timeScale.Value}");
        }

        // Scene redirect – handled once per boot so we do not loop when the
        // target scene is already active.
        string requestedScene = GameCliArgs.RequestedScene;
        if (!string.IsNullOrEmpty(requestedScene))
        {
            string activeScene = SceneManager.GetActiveScene().name;
            if (!string.Equals(activeScene, requestedScene, System.StringComparison.OrdinalIgnoreCase))
            {
                if (_redirecting)
                {
                    return;
                }
                // Validate that the scene is in Build Settings before attempting to load.
                bool foundInBuild = false;
                for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
                {
                    string path = SceneUtility.GetScenePathByBuildIndex(i);
                    string nameOnly = System.IO.Path.GetFileNameWithoutExtension(path);
                    if (string.Equals(nameOnly, requestedScene, System.StringComparison.OrdinalIgnoreCase))
                    {
                        foundInBuild = true;
                        break;
                    }
                }
                if (!foundInBuild)
                {
                    Debug.LogWarning($"[GameCliBootstrap] Requested scene '{requestedScene}' not found in Build Settings – ignoring --scene.");
                }
                else
                {
                    _redirecting = true;
                    Debug.Log($"[GameCliBootstrap] CLI --scene {requestedScene} (active={activeScene}) – loading requested scene.");
                    SceneManager.LoadScene(requestedScene);
                    // Remaining early work (auto-quit etc.) will apply after the new scene loads.
                    return;
                }
            }
        }

        // MainMenu autoStart: if we are in MainMenu and --autoStart or --scene implied
        // a game scene was not requested, jump straight to the arena.
        TryApplyMainMenuAutoStart();

        ScheduleAutoQuitIfNeeded();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Re-apply scene-agnostic overrides after each load (e.g. timeScale could be reset).
        float? timeScale = GameCliArgs.TimeScaleOverride;
        if (timeScale.HasValue)
        {
            Time.timeScale = timeScale.Value;
        }

        // Re-evaluate MainMenu auto-start for the newly loaded scene.
        TryApplyMainMenuAutoStart();

        // Zombie spawner gameplay overrides (noSpawning / maxZombies / interval).
        ApplyGameplayOverrides();

        ScheduleAutoQuitIfNeeded();
    }

    private void TryApplyMainMenuAutoStart()
    {
        // Only act in the MainMenu scene when --autoStart is set. The menu's own
        // flow owns the launch path; we call the appropriate Host/Join/Start entry.
        if (!GameCliArgs.AutoStart)
        {
            return;
        }
        Scene active = SceneManager.GetActiveScene();
        if (!string.Equals(active.name, "MainMenu", System.StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
        MainMenuController controller = FindFirstObjectByType<MainMenuController>();
        if (controller == null || controller.isTransitioning)
        {
            return;
        }

        // Resolve target from --scene or networking mode, defaulting to the normal arena.
        string requestedScene = GameCliArgs.RequestedScene;
        if (!string.IsNullOrEmpty(requestedScene))
        {
            // Already handled by the scene redirect – if we are still in MainMenu
            // it means the redirect did not fire (scene already MainMenu requested).
            // Proceed to launch the requested scene directly.
            if (!string.Equals(requestedScene, "MainMenu", System.StringComparison.OrdinalIgnoreCase))
            {
                Debug.Log($"[GameCliBootstrap] --autoStart + --scene {requestedScene} – loading directly.");
                SceneManager.LoadScene(requestedScene);
                return;
            }
        }

        // No explicit scene – infer from networking mode: client/host => networked arena.
        NetworkSessionMode? netMode = GameCliArgs.NetworkingModeOverride;
        if (netMode == NetworkSessionMode.Client)
        {
            Debug.Log("[GameCliBootstrap] --autoStart + --mode client – joining networked arena.");
            controller.JoinGame();
        }
        else if (netMode == NetworkSessionMode.Host)
        {
            Debug.Log("[GameCliBootstrap] --autoStart + --mode host – hosting networked arena.");
            controller.HostGame();
        }
        else
        {
            Debug.Log("[GameCliBootstrap] --autoStart – starting single-player arena.");
            controller.StartGame();
        }
    }

    private void ApplyGameplayOverrides()
    {
        ZombieSpawner spawner = FindFirstObjectByType<ZombieSpawner>();
        if (spawner == null)
        {
            return;
        }

        if (GameCliArgs.NoSpawning)
        {
            spawner.SetSpawningEnabled(false);
            if (GameCliArgs.IsVerbose) Debug.Log("[GameCliBootstrap] Zombie spawning disabled via --noSpawning.");
        }

        // MaxZombies and spawnInterval are serialized private fields; use reflection
        // so the override stays compatible if the fields are renamed (test seam).
        int? maxZombies = GameCliArgs.MaxZombiesOverride;
        if (maxZombies.HasValue)
        {
            SerializedFieldHelper.TrySetInt(spawner, "_maxZombies", maxZombies.Value);
            if (GameCliArgs.IsVerbose) Debug.Log($"[GameCliBootstrap] MaxZombies overridden to {maxZombies.Value}");
        }
        float? interval = GameCliArgs.SpawnIntervalOverride;
        if (interval.HasValue)
        {
            SerializedFieldHelper.TrySetFloat(spawner, "_spawnInterval", interval.Value);
            if (GameCliArgs.IsVerbose) Debug.Log($"[GameCliBootstrap] SpawnInterval overridden to {interval.Value}");
        }
    }

    private void ScheduleAutoQuitIfNeeded()
    {
        if (_autoQuitScheduled)
        {
            return;
        }
        float seconds = GameCliArgs.AutoQuitAfterSeconds;
        if (seconds <= 0f)
        {
            return;
        }
        _autoQuitScheduled = true;
        Debug.Log($"[GameCliBootstrap] Auto-quit scheduled in {seconds}s (--autoQuit).");
        StartCoroutine(AutoQuitCoroutine(seconds));
    }

    private IEnumerator AutoQuitCoroutine(float seconds)
    {
        // Use realtime so timeScale overrides do not stretch the wait.
        yield return new WaitForSecondsRealtime(seconds);
        Debug.Log($"[GameCliBootstrap] Auto-quit triggered after {seconds}s.");
        QuitWithCode(0);
    }

    private static void QuitWithCode(int exitCode)
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.Exit(exitCode);
#else
        Application.Quit(exitCode);
#endif
    }

    // Minimal reflection helper for private serialized fields (no editor dependency at runtime).
    private static class SerializedFieldHelper
    {
        public static bool TrySetInt(Object target, string fieldName, int value)
        {
            System.Reflection.FieldInfo field = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null && field.FieldType == typeof(int))
            {
                field.SetValue(target, value);
                return true;
            }
            return false;
        }
        public static bool TrySetFloat(Object target, string fieldName, float value)
        {
            System.Reflection.FieldInfo field = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null && field.FieldType == typeof(float))
            {
                field.SetValue(target, value);
                return true;
            }
            return false;
        }
    }
}
