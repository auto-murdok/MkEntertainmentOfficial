using System;
using UnityEngine;

public class RagdollUtils
{
    // A balanced standing skeleton is a stable physics "tower": gravity alone
    // lets it sleep in place, so the corpse freezes upright (seen on both the
    // dying peer and mirrored peers). Waking the bodies plus a small impulse
    // on the pelvis guarantees a topple from any pose.
    private const float ToppleImpulse = 1.6f;

    public static void SetRagdollState(Transform root, bool isRagdoll, Action onStateApplied = null)
    {
        Rigidbody[] rigidbodies = root.GetComponentsInChildren<Rigidbody>();
        Rigidbody hips = null;
        for (int i = 0; i < rigidbodies.Length; i++)
        {
            rigidbodies[i].isKinematic = !isRagdoll;
            if (isRagdoll)
            {
                rigidbodies[i].WakeUp();
                if (hips == null && rigidbodies[i].name.Contains("Hips"))
                {
                    hips = rigidbodies[i];
                }
            }
        }

        if (isRagdoll && hips != null)
        {
            Vector2 direction = UnityEngine.Random.insideUnitCircle.normalized;
            hips.AddForce(new Vector3(direction.x, 0.2f, direction.y) * ToppleImpulse, ForceMode.Impulse);
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
