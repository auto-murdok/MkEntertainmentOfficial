using UnityEngine;

public class HandgunContext : Blackboard
{
    public Animator animator;
    public int maxClipSize;
    public int clipSize;
    // Reserve pool the reload pulls from (int.MaxValue = infinite reserve).
    public int reserveAmmo = int.MaxValue;
    // Seconds between shots (rechambering cadence), supplied by the Weapon config.
    public float fireRate;
    public int gunKick;

    public Vector3 aimDirection;
    public bool isTriggerPressed;
    public bool isReloading;
    public CharacterUIController UIController;
}
