using Cinemachine;
using UnityEngine;
using UnityEngine.Animations.Rigging;

/// <summary>
/// Composition root for the playable layer. Spawns every player-related object
/// (input handler, core components, character) and wires the cross-references
/// on the instances. Scenes only contain map + camera + this spawner.
/// </summary>
public class PlayerSpawner : MonoBehaviour
{
    [Header("Prefabs (spawned at runtime)")]
    [SerializeField] private GameObject _spawnablePlayer;
    [SerializeField] private GameObject _inputHandlerPrefab;
    [SerializeField] private GameObject _playerCorePrefab;

    private void Awake()
    {
        // Game-flow: the state manager lives on the composition root so the
        // arena scene needs no extra setup object.
        GameStateManager gameStateManager = gameObject.AddComponent<GameStateManager>();

        // Spawn at the spawner's own transform so scenes control the spawn point.
        InputHandler inputHandler = Instantiate(_inputHandlerPrefab, transform.position, transform.rotation).GetComponent<InputHandler>();
        GameObject playerCore = Instantiate(_playerCorePrefab, transform.position, transform.rotation);
        GameObject player = Instantiate(_spawnablePlayer, transform.position, transform.rotation);

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
        // prefab assets cannot store it, so the spawner re-injects it. Fail loudly
        // instead of silently degrading the aim fallback.
        Transform mouseWorldHook = null;
        UnityEngine.Camera mainCamera = UnityEngine.Camera.main;
        if (mainCamera == null)
        {
            Debug.LogWarning("[PlayerSpawner] No MainCamera in the scene — AimTarget mouse-world fallback disabled.");
        }
        else
        {
            mouseWorldHook = mainCamera.transform.Find("MousePosition");
            if (mouseWorldHook == null)
            {
                Debug.LogWarning("[PlayerSpawner] MainCamera has no MousePosition child — AimTarget mouse-world fallback disabled.");
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
        foreach (CinemachineVirtualCamera vcam in playerCore.GetComponentsInChildren<CinemachineVirtualCamera>())
        {
            vcam.Follow = locomotion._cinemachineTarget;
        }

        // The UI observes the spawned character's UI subject.
        coreUI._subject = player.GetComponent<CharacterUIController>();

        // Debug overlay (F3 toggle) — lives on the player instance so it can
        // read the brain, locomotion and equipped weapon directly.
        player.AddComponent<DebugHud>();

        // Game-flow wiring: player death triggers game over; zombie spawning is
        // the system the state manager switches off on game over.
        brain.Died += gameStateManager.NotifyPlayerDied;
        ZombieSpawner zombieSpawner = FindFirstObjectByType<ZombieSpawner>();
        gameStateManager.RegisterSpawningToggle(zombieSpawner.SetSpawningEnabled);
    }
}
