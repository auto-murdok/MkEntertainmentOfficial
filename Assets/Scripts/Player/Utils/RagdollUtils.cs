using System;
using UnityEngine;

public class RagdollUtils
{
    public static void SetRagdollState(Transform root, bool isRagdoll, Action onStateApplied = null)
    {
        Rigidbody[] rigidbodies = root.GetComponentsInChildren<Rigidbody>();
        for (int i = 0; i < rigidbodies.Length; i++)
        {
            rigidbodies[i].isKinematic = !isRagdoll;
        }

        onStateApplied?.Invoke();
    }

    public static void EnableRagdoll(Transform root, Action onEnableRagdoll = null)
    {
        SetRagdollState(root, true, onEnableRagdoll);
    }

    public static void DisableRagdoll(Transform root)
    {
        SetRagdollState(root, false);
    }
}
