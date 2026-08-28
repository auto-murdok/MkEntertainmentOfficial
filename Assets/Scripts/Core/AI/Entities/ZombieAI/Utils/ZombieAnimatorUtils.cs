using UnityEngine;

public class ZombieAnimatorUtils
{
    public static void ApplyRootMotionMovement(Animator animator, Vector2 movementThreshold)
    {
        animator.SetFloat("Horizontal", movementThreshold.x, 0.5f, Time.deltaTime);
        animator.SetFloat("Vertical", movementThreshold.y, 0.5f, Time.deltaTime);
    }

    public static void DisableRootMotionMovement(Animator animator, float speed)
    {
        animator.SetFloat("Horizontal", 0f, speed, Time.deltaTime);
        animator.SetFloat("Vertical", 0f, speed, Time.deltaTime);
    }
}