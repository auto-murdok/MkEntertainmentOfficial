using UnityEngine;

public class AnimatorUtils
{
    private const string HorizontalParameter = "Horizontal";
    private const string VerticalParameter = "Vertical";

    public static void SetMovementRootMotion(Animator animator, Vector2 motion, float speed)
    {
        animator.SetFloat(HorizontalParameter, motion.x, speed, Time.deltaTime);
        animator.SetFloat(VerticalParameter, motion.y, speed, Time.deltaTime);
    }

    public static void SetLayerWeight(Animator animator, int layer, float weight, float speed)
    {
        animator.SetLayerWeight(layer, Mathf.Lerp(animator.GetLayerWeight(layer), weight, Time.deltaTime * speed));
    }
}
