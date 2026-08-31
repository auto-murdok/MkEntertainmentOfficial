using UnityEngine;

public static class AnimatorUtils
{
    public const int AimLayerIndex = 1;

    public static readonly int HorizontalHash = Animator.StringToHash("Horizontal");
    public static readonly int VerticalHash = Animator.StringToHash("Vertical");
    public static readonly int IsReloadingHash = Animator.StringToHash("isReloading");
    public static readonly int BiteHash = Animator.StringToHash("Bite");
    public static readonly int TakeBiteHash = Animator.StringToHash("TakeBite");
    public static readonly int RHandAttackHash = Animator.StringToHash("RHandAttack");

    // Handgun weapon animator hashes
    public static readonly int HandgunShootHash = Animator.StringToHash("fakeGun_shoot");
    public static readonly int HandgunIdleHash = Animator.StringToHash("Idle");

    // Frame-rate-independent damping factor: Lerp(a, b, DampFactor(speed, dt))
    // converges at the same rate at any framerate, unlike Lerp(a, b, k * dt)
    // which speeds up as the framerate rises.
    public static float DampFactor(float speed, float deltaTime)
    {
        return 1f - Mathf.Exp(-speed * Mathf.Max(deltaTime, 0f));
    }

    public static void SetMovementRootMotion(Animator animator, Vector2 motion, float speed)
    {
        if (animator == null) return;
        animator.SetFloat(HorizontalHash, motion.x, speed, Time.deltaTime);
        animator.SetFloat(VerticalHash, motion.y, speed, Time.deltaTime);
    }

    public static void DisableMovementRootMotion(Animator animator, float speed)
    {
        if (animator == null) return;
        animator.SetFloat(HorizontalHash, 0f, speed, Time.deltaTime);
        animator.SetFloat(VerticalHash, 0f, speed, Time.deltaTime);
    }

    public static void SetLayerWeight(Animator animator, int layer, float weight, float speed)
    {
        if (animator == null) return;
        animator.SetLayerWeight(layer, Mathf.Lerp(animator.GetLayerWeight(layer), weight, DampFactor(speed, Time.deltaTime)));
    }
}
