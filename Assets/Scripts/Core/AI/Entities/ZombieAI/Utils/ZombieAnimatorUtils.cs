using UnityEngine;

public class ZombieAnimatorUtils
{
    private const float MovementSmoothingTime = 0.5f;

    public static void ApplyRootMotionMovement(Animator animator, Vector2 movementThreshold)
    {
        AnimatorUtils.SetMovementRootMotion(animator, movementThreshold, MovementSmoothingTime);
    }

    public static void DisableRootMotionMovement(Animator animator, float speed)
    {
        AnimatorUtils.DisableMovementRootMotion(animator, speed);
    }
}
