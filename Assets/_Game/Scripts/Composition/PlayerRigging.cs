using UnityEngine;
using UnityEngine.Animations.Rigging;
using Cinemachine;

// Shared player-rig wiring, used by both composition paths:
// - PlayerSpawner (single-player arena): instantiates everything itself.
// - NetworkedPlayerComposition (networked arena): NGO spawns the player
//   prefab on every peer and the owner wires the local-only parts around the
//   network-spawned instance in OnNetworkSpawn.
public static class PlayerRigging
{
    // Wires the local-only rig parts around an already-instantiated player:
    // input subject, world aim target (+ rig constraints + graph rebuild),
    // camera fallback hook, Cinemachine follows, UI subject and HUDs.
    // All prefab -> scene references must be (re-)injected here because Unity
    // strips them from prefab assets on save.
    public static void WireLocalRig(GameObject player, InputHandler inputHandler, GameObject playerCore)
    {
        CharacterBrain brain = player.GetComponent<CharacterBrain>();
        CharacterLocomotion locomotion = player.GetComponent<CharacterLocomotion>();
        PlayerCoreUI coreUI = playerCore.GetComponentInChildren<PlayerCoreUI>();

        // Input flow: the InputHandler subject broadcasts to the character brain.
        brain._subject = inputHandler;

        // Combat: the world-space aim point is the AimTarget child of the core
        // components (PlayerCoreUI._aimTarget is the crosshair UI toggle, not
        // the aim point — matches the arena wiring).
        Transform aimTarget = playerCore.transform.Find("AimTarget");
        locomotion._aimTarget = aimTarget;

        // The aim target's fallback hook is a scene reference (MainCamera child);
        // prefab assets cannot store it, so it is re-injected here. Fail loudly
        // instead of silently degrading the aim fallback.
        Transform mouseWorldHook = null;
        UnityEngine.Camera mainCamera = UnityEngine.Camera.main;
        if (mainCamera == null)
        {
            Debug.LogWarning("[PlayerRigging] No MainCamera in the scene — AimTarget mouse-world fallback disabled.");
        }
        else
        {
            mouseWorldHook = mainCamera.transform.Find("MousePosition");
            if (mouseWorldHook == null)
            {
                Debug.LogWarning("[PlayerRigging] MainCamera has no MousePosition child — AimTarget mouse-world fallback disabled.");
            }
        }
        aimTarget.GetComponent<AimTarget>()._fallbackMouseWorldHook = mouseWorldHook;

        // Rigging: re-inject the aim source into every MultiAimConstraint —
        // prefab assets cannot store scene references, so they arrive NULL.
        foreach (MultiAimConstraint constraint in player.GetComponentsInChildren<MultiAimConstraint>())
        {
            var data = constraint.data;
            data.sourceObjects = new WeightedTransformArray { new WeightedTransform(aimTarget, 1f) };
            constraint.data = data;
        }

        // RigBuilder built its animation graph during Instantiate, before the
        // sources above existed — rebuild it so the constraints pick them up.
        var rigBuilder = player.GetComponent<RigBuilder>();
        rigBuilder.Clear();
        rigBuilder.Build();

        // Both cinemachine cameras follow the player's camera hook.
        CinemachineVirtualCamera followCamera = null;
        CinemachineVirtualCamera aimCamera = null;
        foreach (CinemachineVirtualCamera vcam in playerCore.GetComponentsInChildren<CinemachineVirtualCamera>(true))
        {
            vcam.Follow = locomotion._cinemachineTarget;

            // Both cameras stay active from here on: CameraModeController
            // selects between them by priority. (The old path left the aim
            // camera inactive and SetActive-toggled it from a UI
            // notification, which deregistered it from the Brain's queue
            // and froze its standby state.)
            if (!vcam.gameObject.activeSelf)
            {
                vcam.gameObject.SetActive(true);
            }

            if (vcam.name.Contains("Aim"))
            {
                aimCamera = vcam;
            }
            else if (vcam.name.Contains("Follow"))
            {
                followCamera = vcam;
            }

            // NOTE: no OnTargetObjectWarped here. That call shifts the
            // vcam's own transform by the supplied delta to preserve an
            // EXISTING framing across a teleport; at spawn there is no
            // prior framing to preserve, and passing the hook's position
            // would displace the camera. Fresh vcams compute their pose
            // from the follow target on their first update.
        }

        // Seed the camera yaw/pitch from the placed hook now that placement
        // is final — otherwise the first LateUpdate writes (0, 0) over the
        // spawn facing and the camera opens world-north until first aim.
        locomotion.SeedCamera();

        // Camera mode is driven by the character state machine (the owner
        // of Aiming) through the mode controller — never by UI observers.
        if (followCamera != null && aimCamera != null)
        {
            CameraModeController cameraModes = playerCore.AddComponent<CameraModeController>();
            cameraModes.Wire(locomotion, followCamera, aimCamera);
        }
        else
        {
            Debug.LogWarning("[PlayerRigging] Expected Follow + Aim virtual cameras in the player core — camera mode control disabled.");
        }

        // The UI observes the spawned character's UI subject.
        coreUI._subject = player.GetComponent<CharacterUIController>();
        // The PlayerHud owns the ammo readout — retire the legacy clip text.
        coreUI.SetClipInfoActive(false);

        // HUDs live on the player instance so they can read the brain,
        // locomotion and equipped weapon directly. PlayerHud is the visible
        // gameplay HUD; DebugHud is the hidden F3 diagnostics overlay.
        player.AddComponent<PlayerHud>();
        player.AddComponent<DebugHud>();
    }
}
