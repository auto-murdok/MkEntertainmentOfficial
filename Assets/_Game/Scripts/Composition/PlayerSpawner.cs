using UnityEngine;

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

    [Header("Game-flow event channels (SO assets)")]
    [SerializeField] private VoidEventChannel _playerDiedChannel;
    [SerializeField] private BoolEventChannel _spawningEnabledChannel;

    private void Awake()
    {
        // Game-flow: the state manager lives on the composition root so the
        // arena scene needs no extra setup object.
        GameStateManager gameStateManager = gameObject.AddComponent<GameStateManager>();
        // Esc / gamepad-Start system menu (disconnect/exit) for both arenas.
        gameObject.AddComponent<PauseMenuController>();

        ZombieSpawner zombieSpawner = FindFirstObjectByType<ZombieSpawner>();
        if (zombieSpawner != null)
        {
            zombieSpawner.spawningEnabledChannel = _spawningEnabledChannel;
        }

        // Networked scenes: the player prefab is NGO-spawned from
        // NetworkManager.PlayerPrefab on every peer, and the owner composes the
        // local rig in NetworkedPlayerComposition.OnNetworkSpawn. Composing here
        // as well would double-spawn the rig (locally + over the network).
        if (FindFirstObjectByType<Unity.Netcode.NetworkManager>() != null)
        {
            return;
        }

        // Spawn at the spawner's own transform so scenes control the spawn point.
        InputHandler inputHandler = Instantiate(_inputHandlerPrefab, transform.position, transform.rotation).GetComponent<InputHandler>();
        GameObject playerCore = Instantiate(_playerCorePrefab, transform.position, transform.rotation);
        GameObject player = Instantiate(_spawnablePlayer, transform.position, transform.rotation);

        PlayerRigging.WireLocalRig(player, inputHandler, playerCore);

        // Game-flow wiring through SO event channels: the player's death is
        // broadcast on the channel (any number of listeners may react), and the
        // state manager consumes it and broadcasts the spawning toggle. Neither
        // side references the other directly.
        CharacterBrain brain = player.GetComponent<CharacterBrain>();
        brain.Died += _playerDiedChannel.Raise;
        gameStateManager.playerDiedChannel = _playerDiedChannel;
        gameStateManager.spawningEnabledChannel = _spawningEnabledChannel;
    }
}
