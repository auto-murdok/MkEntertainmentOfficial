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
    public float biteDuration;
    // Set when a bite starts; cleared once the victim leaves bite range so the
    // zombie only bites once per contact (re-arms after the push-off separates them).
    public bool recentlyBitten;
    public bool isBiting;
    // Secondary attack against a victim already pinned by another zombie's
    // bite grab (see ZombieHandAttackState). Exclusive per zombie like isBiting.
    public bool isHandAttacking;
    public bool isPreparing;
    public float attackCooldownTimer;

    // Networking: triggers must go through the NetworkAnimator on the zombie's
    // authority (the host/server) to replicate; everything else falls back to
    // the raw animator (single-player, tests, non-authority copies).
    public Unity.Netcode.Components.NetworkAnimator networkAnimator;

    public void SetAnimatorTrigger(int triggerHash)
    {
        NetworkAnimatorUtils.TrySetTrigger(animator, networkAnimator,
            networkAnimator != null && networkAnimator.IsServer, triggerHash);
    }
}