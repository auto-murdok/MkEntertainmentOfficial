using UnityEngine;

/// <summary>Centralises replicated trigger routing so player (owner-authoritative) and
/// zombie (server-authoritative) stay in sync — one place to change the
/// authority check if the mode ever changes.
/// </summary>
public static class NetworkAnimatorUtils
{
    public static void TrySetTrigger(Animator animator, Unity.Netcode.Components.NetworkAnimator netAnimator, bool isAuthority, int hash)
    {
        if (animator == null) return;
        if (netAnimator != null && netAnimator.IsSpawned && isAuthority)
        {
            netAnimator.SetTrigger(hash);
        }
        else
        {
            animator.SetTrigger(hash);
        }
    }
}
