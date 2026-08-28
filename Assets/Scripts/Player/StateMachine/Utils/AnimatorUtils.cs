using UnityEditor;
using UnityEngine;

public class AnimatorUtils
{
    public static void SetMovementRootMotion(Animator animator, Vector2 motion, float speed)
    {
        animator.SetFloat("Horizontal", motion.x, speed, Time.deltaTime);
        animator.SetFloat("Vertical", motion.y, speed, Time.deltaTime);
    }

    public static void SetLayerWeight(Animator animator, int layer, float weight, float speed) {
        animator.SetLayerWeight(layer, Mathf.Lerp(animator.GetLayerWeight(layer), weight, Time.deltaTime * speed));
    }
}