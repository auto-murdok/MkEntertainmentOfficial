using UnityEngine;

public class HandgunContext : Blackboard
{
    public Animator animator;
    public int maxClipSize;
    public int clipSize;
    public int gunKick;
    
    public Vector3 aimDirection;
    public bool isTriggerPressed;
    public bool isReloading;
    public CharacterUIController UIController;
}
