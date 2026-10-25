using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Animations.Rigging;

public class CharacterStateContext : ActorBlackboard
{
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
    public CharacterUIController UIController;
    public PlayerData data;
}

public struct CinemachineContext
{
    // NOTE: no targetYaw field — an earlier version stored
    // Transform.rotation.y here (the quaternion's Y component, sin(yaw/2),
    // not an angle) and nothing ever consumed it. Camera seeding lives in
    // CameraUtils.SeedFrom, which reads eulerAngles at placement time.
    public float topClamp;
    public float bottomClamp;
    public float lookSensivity;
}
