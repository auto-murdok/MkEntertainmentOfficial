using System;
using UnityEngine;

public class RagdollUtils
{
    public static void EnableRagdoll(Transform root, Action onEnableRagdoll)
    {
        Rigidbody[] rigidbodies = root.GetComponentsInChildren<Rigidbody>();
        foreach (Rigidbody rigidBody in rigidbodies)
        {
            rigidBody.isKinematic = false;
        }

        onEnableRagdoll?.Invoke();
    }

    public static void DisableRagdoll(Transform root)
    {
        Rigidbody[] rigidbodies = root.GetComponentsInChildren<Rigidbody>();
        foreach (Rigidbody rigidBody in rigidbodies)
        {
            rigidBody.isKinematic = true;
        }
    }
}
