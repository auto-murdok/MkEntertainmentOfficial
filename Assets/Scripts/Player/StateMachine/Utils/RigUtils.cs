using UnityEngine;
using UnityEngine.Animations.Rigging;

public class RigUtils
{
    public static void HandleIncreaseRigWeight(Rig rig)
    {
        rig.weight = Mathf.Lerp(rig.weight, 1f, Time.deltaTime * 20f);
    }

    public static void HandleDecreaseRigWeight(Rig rig)
    {
        rig.weight = Mathf.Lerp(rig.weight, 0f, Time.deltaTime * 10f);
    }
}