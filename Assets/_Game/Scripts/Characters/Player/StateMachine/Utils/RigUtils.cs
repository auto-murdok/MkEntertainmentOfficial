using UnityEngine;
using UnityEngine.Animations.Rigging;

public class RigUtils
{
    private const float RigWeightIncreaseSpeed = 20f;
    private const float RigWeightDecreaseSpeed = 10f;

    public static void HandleIncreaseRigWeight(Rig rig)
    {
        rig.weight = Mathf.Lerp(rig.weight, 1f, Time.deltaTime * RigWeightIncreaseSpeed);
    }

    public static void HandleDecreaseRigWeight(Rig rig)
    {
        rig.weight = Mathf.Lerp(rig.weight, 0f, Time.deltaTime * RigWeightDecreaseSpeed);
    }
}
