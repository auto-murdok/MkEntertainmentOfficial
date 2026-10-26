using UnityEngine;
using UnityEngine.AI;

public class ZombieContext : ActorBlackboard
{
    public ZombieData data;
    public ZombieSockets sockets;
    public Transform visionHook;
    public LayerMask detectionLayerMask;
    public LayerMask obstacleLayerMask;
    public ISurvivor target;
    public IInteractable interactable;
    public InteractableRegistry registry;
    public ZombieBrain brain;
    public ZombieHand[] hands;
    // Set when a bite starts; cleared once the victim leaves bite range so the
    // zombie only bites once per contact (re-arms after the push-off separates them).
    public bool recentlyBitten;
    public bool isBiting;
    // Secondary attack against a victim already pinned by another zombie's
    // bite grab (see ZombieHandAttackState). Exclusive per zombie like isBiting.
    public bool isHandAttacking;
    public bool isPreparing;
    public float attackCooldownTimer;

    // Displacement-measured locomotion (AITransformUtils): the chase
    // blend is fed from the body's real velocity, tracked across frames.
    // The NavMeshAgent cannot supply it — its speed is 0 in this project
    // (root motion is the only mover), so agent.velocity reads zero for
    // the entire chase.
    public Vector3 measuredPosition;
    public Vector3 measuredVelocity;
    public float measuredTimestamp;
    public float measuredEpisodeStart;
    public bool hasMeasuredPosition;

    // Networking: triggers must go through the NetworkAnimator on the zombie's
    // authority (the host/server) to replicate; everything else falls back to
    // the raw animator (single-player, tests, non-authority copies).
    public Unity.Netcode.Components.NetworkAnimator networkAnimator;

    public void SetAnimatorTrigger(int triggerHash)
    {
        if (animator == null)
        {
            return;
        }
        if (networkAnimator != null && networkAnimator.IsSpawned && networkAnimator.IsServer)
        {
            networkAnimator.SetTrigger(triggerHash);
        }
        else
        {
            animator.SetTrigger(triggerHash);
        }
    }
}