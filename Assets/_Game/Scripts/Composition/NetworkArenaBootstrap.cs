using Unity.Netcode;
using UnityEngine;

// Networking bootstrap for the NetworkedCombatArena scene.
//
// The player prefab is assigned to NetworkManager.PlayerPrefab, so NGO spawns
// it automatically on every peer and NetworkedPlayerComposition composes the
// local rig in OnNetworkSpawn. This component only decides the session role:
// host by default, client when launched with a "-mlclient" command-line
// argument (used by the standalone client build for milestone testing).
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

        // Spawn-position approval must be installed before the session starts
        // (the host's own connection goes through it as well).
        networkManager.ConnectionApprovalCallback = ApproveConnection;

        // Role resolution: an explicit menu choice wins; otherwise the command
        // line decides (-mlclient joins, anything else hosts).
        NetworkSessionMode mode = NetworkSession.desiredMode;
        bool asClient = mode switch
        {
            NetworkSessionMode.Host => false,
            NetworkSessionMode.Client => true,
            _ => IsCommandLineClient(),
        };

        if (asClient)
        {
            // Loopback for now — everything points at localhost until a real
            // server/IP flow exists.
            networkManager.GetComponent<Unity.Netcode.Transports.UTP.UnityTransport>()
                .SetConnectionData(NetworkSession.ServerAddress, NetworkSession.ServerPort);
        }

        bool started = asClient ? networkManager.StartClient() : networkManager.StartHost();
        if (!started)
        {
            Debug.LogError($"[NetworkArenaBootstrap] Start{(asClient ? "Client" : "Host")} failed — check the UnityTransport settings.");
            return;
        }

        Debug.Log($"[NetworkArenaBootstrap] Session started as {(asClient ? "client" : "host")} (mode={mode}, {NetworkSession.ServerAddress}:{NetworkSession.ServerPort}).");
    }

    // NGO gold-standard connection approval: auto-create the player object and
    // pin its creation pose to the spawn point this component sits at.
    internal void ApproveConnection(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        response.Approved = true;
        response.CreatePlayerObject = true;
        response.Position = transform.position;
        response.Rotation = transform.rotation;
    }

    public static bool IsCommandLineClient()
    {
        // Editor and player command line (lets an editor instance act as a
        // dedicated client by launching it with -mlclient as well).
        string[] arguments = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < arguments.Length; i++)
        {
            string argument = arguments[i].ToLowerInvariant();
            if (argument == "-mlclient" || argument == "--mlclient" || argument == "-client" || argument == "--client")
            {
                return true;
            }
        }
        return false;
    }
}
