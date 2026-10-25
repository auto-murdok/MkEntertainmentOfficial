using UnityEngine;

// Look-sensitivity scaling across a focal-length (FOV) change.
//
// When the aim camera zooms (this project: follow FOV 40 -> aim FOV 30),
// the same mouse movement must rotate the view LESS, in exact proportion
// to the zoom — otherwise on-screen tracking speeds up while aiming and
// precision aim feels twitchy and overshoots. The gold standard is
// focal-length scaling (DICE's Uniform Soldier Aiming "ViewSpeed", a.k.a.
// 0% monitor-distance matching):
//
//     scale = tan(aimFov / 2) / tan(hipFov / 2)
//
// For the project's 40 -> 30 pair that is ~0.736.
public static class AimSensitivity
{
    // Sensitivity multiplier to apply when moving from fromFovDegrees to
    // toFovDegrees. Returns 1 (unscaled) for degenerate inputs.
    public static float FovRatioScale(float fromFovDegrees, float toFovDegrees)
    {
        if (fromFovDegrees <= 0f || toFovDegrees <= 0f ||
            fromFovDegrees >= 180f || toFovDegrees >= 180f)
        {
            return 1f;
        }

        float fromTan = Mathf.Tan(fromFovDegrees * 0.5f * Mathf.Deg2Rad);
        float toTan = Mathf.Tan(toFovDegrees * 0.5f * Mathf.Deg2Rad);
        return fromTan > 0f ? toTan / fromTan : 1f;
    }
}
