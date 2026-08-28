using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Animations.Rigging;

public class CharacterStateContext : Blackboard
{
    public Animator animator;
    public NavMeshAgent agent;
    public Rig rig;
    public Vector2 lookInput;
    public Vector2 movementInput;
    public bool isRunning;
    public bool isAiming;
    public bool isCurrentDeviceMouse;
    public bool isReloading;
    public bool isBeingAttacked;
    public IInteractable attacker;
    public Transform mainCameraTarget;
}

public struct CinemachineContext
{
    public float targetYaw;
    public float topClamp;
    public float bottomClamp;
    public float lookSensivity;
}
