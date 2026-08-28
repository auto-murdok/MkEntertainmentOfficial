using UnityEngine;
using UnityEngine.AI;

public class ZombieContext : Blackboard
{
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