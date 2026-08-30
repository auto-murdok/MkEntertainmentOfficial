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
    public override void OnNetworkSpawn()
    {
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
