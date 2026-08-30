using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

// Server-authoritative zombie runtime gate + death despawn.
//
// The zombie simulation (FSM, NavMeshAgent pathing, bite damage) runs on the
// host only; clients receive pose + animation via the server-authoritative
// NetworkTransform/NetworkAnimator. On networked clients the behaviour and its
// agent are disabled so they cannot fight the replication.
//
// On death the server despawns WITHOUT destroying: clients remove the zombie
// while the host keeps the GameObject so the local ragdoll plays out and the
// corpse cleanup timer runs as in single-player.
public class NetworkedZombieController : NetworkBehaviour
{
    // Server-write mirror of the zombie FSM's grab/prepare phase: the victim's
    // owner reads it to know when to lock position to the bite socket
    // (CharacterTakeBiteState pins only while attacker.isPreparing).
    private readonly NetworkVariable<bool> _isPreparing = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private ZombieBehavior _behavior;

    public bool SimulatesRemotely => IsSpawned && !IsServer;

    public bool MirroredIsPreparing => _isPreparing.Value;

    public override void OnNetworkSpawn()
    {
        _behavior = GetComponent<ZombieBehavior>();
        ActorBrainBase brain = GetComponent<ActorBrainBase>();
        if (brain != null)
        {
            brain.Died += OnDied;
        }

        if (!IsServer)
        {
            DisableLocalSimulation();
        }
    }

    public override void OnNetworkDespawn()
    {
        ActorBrainBase brain = GetComponent<ActorBrainBase>();
        if (brain != null)
        {
            brain.Died -= OnDied;
        }
    }

    private void Update()
    {
        // Server: push the FSM's prepare flag into the replicated variable
        // (NGO only sends on actual writes).
        if (IsServer && _behavior != null && _behavior._context != null && _isPreparing.Value != _behavior._context.isPreparing)
        {
            _isPreparing.Value = _behavior._context.isPreparing;
        }
    }

    private void DisableLocalSimulation()
    {
        GetComponent<ZombieBehavior>().enabled = false;
        NavMeshAgent agent = GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            agent.enabled = false;
        }
    }

    private void OnDied()
    {
        if (IsServer && NetworkObject.IsSpawned)
        {
            NetworkObject.Despawn(false);
        }
    }
}
