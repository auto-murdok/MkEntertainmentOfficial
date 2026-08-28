using UnityEngine;
using UnityEngine.AI;

public class ZombieContext : ActorBlackboard
{
    public ZombieData data;
    public ZombieSockets sockets;
    public Transform visionHook;
    public LayerMask detectionLayerMask;
    public LayerMask ignoreLayerMask;
    public ISurvivor target;
    public IInteractable interactable;
    public ZombieBrain brain;
    public ZombieHand[] hands;
    public float biteDuration;
    public bool isBitting;
    public bool isPreparing;
    public float attackCooldownTimer;
}