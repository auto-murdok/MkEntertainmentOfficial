using UnityEngine;
using UnityEngine.AI;

public class ZombieContext : Blackboard
{
    public ZombieData data;
    public ZombieSockets sockets;
    public Transform visionHook;
    public LayerMask detectionLayerMask;
    public LayerMask ignoreLayerMask;
    public NavMeshAgent agent;
    public Animator animator;
    public ISurvivor target;
    public IInteractable interactable;
    public bool isBitting;
    public bool isPreparing;
}