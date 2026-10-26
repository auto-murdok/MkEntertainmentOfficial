using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Animations.Rigging;

// How the locomotion motor may move the character this frame. Written by
// the active FSM state on the shared blackboard, consumed only by
// CharacterLocomotion.OnAnimatorMove — exactly one owner of displacement
// per mode (see docs/grounded_motor_and_corpse_layer.md).
public enum MotorMode
{
    Locomotion,  // clip root motion on the ground plane + grounded vertical channel
    ScriptedPin, // a scripted interaction (bite grapple) owns displacement; motor stands down
    AuthoredY,   // clip root motion verbatim, authored Y included (bite push-off)
    Disabled     // motor never moves the character
}

public class CharacterStateContext : ActorBlackboard
{
    public Rig rig;
    public MotorMode motorMode;
    // Set by a state that hands control back to the motor (bite exit,
    // respawn): the motor clears its vertical state before the next Move.
    public bool motorResetRequested;
    public Vector2 lookInput;
    public Vector2 movementInput;
    public bool isRunning;
    public bool isAiming;
    public bool isCurrentDeviceMouse;
    public bool isReloading;
    public bool isBeingAttacked;
    public IInteractable attacker;
    public CharacterController characterController;
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
