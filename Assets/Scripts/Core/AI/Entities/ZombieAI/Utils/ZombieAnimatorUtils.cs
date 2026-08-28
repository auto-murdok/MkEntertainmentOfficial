using UnityEngine;

public class ZombieAnimatorUtils
{
    private const string HorizontalParameter = "Horizontal";
    private const string VerticalParameter = "Vertical";
    private const float MovementSmoothingTime = 0.5f;

    public static void ApplyRootMotionMovement(Animator animator, Vector2 movementThreshold)
    {
        animator.SetFloat(HorizontalParameter, movementThreshold.x, MovementSmoothingTime, Time.deltaTime);
        animator.SetFloat(VerticalParameter, movementThreshold.y, MovementSmoothingTime, Time.deltaTime);
    }

    public static void DisableRootMotionMovement(Animator animator, float speed)
    {
        animator.SetFloat(HorizontalParameter, 0f, speed, Time.deltaTime);
        animator.SetFloat(VerticalParameter, 0f, speed, Time.deltaTime);
    }
}
