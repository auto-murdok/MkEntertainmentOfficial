using UnityEngine;
using UnityEngine.AI;

// Shared blackboard for any actor-driven state machine. Concrete entity contexts
// (CharacterStateContext, ZombieContext) derive from this so common states
// (death, move-to-target, ...) can be written once and reused across entities.
public class ActorBlackboard : Blackboard
{
    public Animator animator;
    public NavMeshAgent agent;

    // Lifecycle / death
    public bool isAlive = true;
    public bool isRagdoll;

    // Entity-specific death routine (enable ragdoll + teardown) invoked by the
    // shared Dead state. Kept as a callback so the Core assembly stays decoupled
    // from entity-specific utilities (RagdollUtils, etc.).
    public System.Action onDeath;

    // Commanded movement (reusable click-to-move style control, per entity).
    public Vector3? moveDestination;
}
