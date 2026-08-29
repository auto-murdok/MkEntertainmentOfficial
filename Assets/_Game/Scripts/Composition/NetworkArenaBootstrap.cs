using Unity.Netcode;
using UnityEngine;

// Milestone-1 networking bootstrap for the NetworkedCombatArena scene.
// Auto-starts a host after the composition root (PlayerSpawner) has built the
// local player rig, then spawns the already-composed character's NetworkObject
// as this host's player object. NetworkTransform on the player prefab
// replicates its root transform (server-authoritative) to connected clients.
//
// Intentionally NOT part of the single-player arena: this component only
// exists in the networked scene. NGO components on shared prefabs are dormant
// while no NetworkManager is running, so the original arena is unaffected.
public class NetworkArenaBootstrap : MonoBehaviour
{
    private void Start()
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null)
        {
            Debug.LogError("[NetworkArenaBootstrap] No NetworkManager in the scene — add one with a UnityTransport.");
            return;
        }
        if (networkManager.IsListening)
        {
            return; // already started (e.g. scene reloaded during a session)
        }

        if (!networkManager.StartHost())
        {
            Debug.LogError("[NetworkArenaBootstrap] StartHost failed — check the UnityTransport settings.");
            return;
        }

        // The host's own connection is approved a few frames after StartHost
        // returns; spawning as player object before that silently loses the
        // player-object registration (IsPlayerObject stays false).
        StartCoroutine(SpawnPlayerWhenConnected(networkManager));
    }

    private System.Collections.IEnumerator SpawnPlayerWhenConnected(NetworkManager networkManager)
    {
        // Spawn strictly after the host's own connection is fully approved:
        // spawning in the same frame as StartHost returns silently loses the
        // player-object registration (IsPlayerObject stays false).
        float deadline = Time.realtimeSinceStartup + 5f;
        while (!networkManager.IsConnectedClient || !ContainsClientId(networkManager, networkManager.LocalClientId))
        {
            if (Time.realtimeSinceStartup > deadline)
            {
                Debug.LogError("[NetworkArenaBootstrap] Host connection was never approved — player object not spawned.");
                yield break;
            }
            yield return null;
        }

        // PlayerSpawner.Awake has already instantiated and wired the player rig.
        // Spawn its NetworkObject so remote clients instantiate the same prefab
        // (resolved via the NetworkPrefabs list) and NetworkTransform streams it.
        CharacterBrain brain = FindFirstObjectByType<CharacterBrain>();
        if (brain == null)
        {
            Debug.LogError("[NetworkArenaBootstrap] No player character found — PlayerSpawner did not spawn one.");
            yield break;
        }

        NetworkObject networkObject = brain.GetComponent<NetworkObject>();
        if (networkObject == null)
        {
            Debug.LogError("[NetworkArenaBootstrap] The player prefab has no NetworkObject — add one (with NetworkTransform) to the prefab root.");
            yield break;
        }

        if (networkObject.IsSpawned && !networkObject.IsPlayerObject)
        {
            networkObject.Despawn(false); // re-spawn with the player-object flag
        }
        if (!networkObject.IsSpawned)
        {
            networkObject.SpawnAsPlayerObject(networkManager.LocalClientId);
        }

        Debug.Log($"[NetworkArenaBootstrap] Host started; player network object spawned (IsSpawned={networkObject.IsSpawned}, IsPlayerObject={networkObject.IsPlayerObject}).");
    }

    private static bool ContainsClientId(NetworkManager networkManager, ulong clientId)
    {
        foreach (ulong id in networkManager.ConnectedClientsIds)
        {
            if (id == clientId) return true;
        }
        return false;
    }
}
