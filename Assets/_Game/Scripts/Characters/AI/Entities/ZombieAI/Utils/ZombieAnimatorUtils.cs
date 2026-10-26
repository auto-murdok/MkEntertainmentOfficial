using UnityEngine;

public class ZombieAnimatorUtils
{
    // Blend responsiveness: at 0.5 s the Horizontal/Vertical floats took
    // ~2.75 s to reach full stride (measured in-engine: the chase blend
    // ramped 0 -> 1 over 11 samples of 0.25 s), so zombies oozed into
    // motion and kept striding long after their steering changed. 0.12 s
    // tracks velocity/steering changes within a few frames while still
    // smoothing per-frame noise.
    private const float MovementSmoothingTime = 0.12f;

    // The AC_Zombie locomotion blend tree places the walk clip at
    // (0, 0.5): a blend input of this magnitude plays the current walk
    // clip at exactly its authored speed (weight 1, no extrapolation).
    // Feeding 1.0 — the old behaviour — silently doubled every gait, and
    // each variant clip doubled from a different authored speed, so no
    // two zombies paced alike. Pinned against the controller asset by
    // ZombieLocomotionAssetTests.
    public const float LocomotionHomeMagnitude = 0.5f;

    // Playback-rate window for speed matching: beyond ~1.6x a gait reads
    // as strobing, below 0.6x as wading. Intended speeds outside the
    // window clamp instead of warping the clip further.
    public const float MinSpeedMul = 0.6f;
    public const float MaxSpeedMul = 1.6f;

    public static readonly int SpeedMulHash = Animator.StringToHash("SpeedMul");

    public static void ApplyRootMotionMovement(Animator animator, Vector2 movementThreshold)
    {
        AnimatorUtils.SetMovementRootMotion(animator, movementThreshold, MovementSmoothingTime);
    }

    public static void DisableRootMotionMovement(Animator animator, float speed)
    {
        AnimatorUtils.DisableMovementRootMotion(animator, speed);
    }

    // The playback rate that makes the current walk clip cover ground at
    // the data-intended speed (walk or chase), clamped to the sane
    // window. The data speeds were decorative before this: nothing read
    // them, and the emergent pace was clip speed x blend extrapolation
    // (a Walker "chasing" at 3.5 m/s on paper actually moved ~0.5 m/s).
    public static float ComputeSpeedMul(ZombieData data, bool chasing, float clipAuthoredSpeed)
    {
        if (data == null || clipAuthoredSpeed <= 0.01f)
        {
            return 1f;
        }
        float intended = chasing ? data.chaseSpeed : data.walkSpeed;
        return Mathf.Clamp(intended / clipAuthoredSpeed, MinSpeedMul, MaxSpeedMul);
    }

    // Blend response curve: how much stride to feed for a body moving
    // at measuredSpeed toward a target pace. A linear
    // measured -> magnitude feed is a NEUTRAL loop (measured in-engine:
    // ~6 s to converge — the body under-strides, so it stays slow, so it
    // keeps under-striding). The square root gives the loop gain below
    // pace (a body at 25% pace is fed 50% stride) and damping above it,
    // so pace converges in a few steps from either side. Two floors
    // cover the ends: a startup stride during the first moments of a
    // movement episode (a stationary body measures zero, so the curve
    // alone can never start it), and a small strain floor afterwards —
    // a zombie held by the crowd shuffles weakly instead of freezing
    // mid-pose, and re-accelerates from it the moment it breaks free.
    public const float StartupWindowSeconds = 0.5f;
    public const float StartupStrideFraction = 0.6f;
    public const float StallFloorFraction = 0.25f;

    public static float ComputeBlendMagnitude(float measuredSpeed, float targetSpeed, float episodeAgeSeconds)
    {
        if (targetSpeed <= 0.01f)
        {
            return 0f;
        }
        float ratio = Mathf.Clamp01(measuredSpeed / targetSpeed);
        float feed = Mathf.Sqrt(ratio);
        if (episodeAgeSeconds < StartupWindowSeconds)
        {
            feed = Mathf.Max(feed, StartupStrideFraction);
        }
        else
        {
            feed = Mathf.Max(feed, StallFloorFraction * (1f - ratio));
        }
        return LocomotionHomeMagnitude * feed;
    }
}
