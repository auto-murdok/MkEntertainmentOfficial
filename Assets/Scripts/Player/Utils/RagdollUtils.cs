using System;
using UnityEngine;

public class RagdollUtils
{
    public static void EnableRagdoll(Transform root, Action onEnableRagdoll)
    {
        Rigidbody[] rigidbodies = root.GetComponentsInChildren<Rigidbody>();
        foreach (Rigidbody rigidbody in rigidbodies)
        {
            rigidbody.isKinematic = false;
        }

        onEnableRagdoll?.Invoke();
    }

    public static void DisableRagdoll(Transform root)
    {
        Rigidbody[] rigidbodies = root.GetComponentsInChildren<Rigidbody>();
        foreach (Rigidbody rigidbody in rigidbodies)
        {
            rigidbody.isKinematic = true;
        }
    }
}