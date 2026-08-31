using Unity.Netcode;
using UnityEngine;

// Server-authoritative damage routing (see NetworkedDamageRelay): damage is
// computed where the hit is detected, applied on the server, and replicated
// via NetworkedHealth. Single-player (no session) and unregistered actors
// fall back to a direct local hit.
public static class NetworkedDamage
{
    public static void Apply(Component victim, float amount)
    {
        if (victim == null)
        {
            return;
        }

        IDamageable damageable = victim.GetComponentInParent<IDamageable>();
        if (damageable == null)
        {
            return;
        }

        NetworkManager networkManager = NetworkManager.Singleton;
        NetworkObject victimNetworkObject = victim.GetComponentInParent<NetworkObject>();
        if (networkManager == null || !networkManager.IsListening || victimNetworkObject == null)
        {
            damageable.TakeDamage(amount);
            return;
        }

        NetworkedDamageRelay relay = victimNetworkObject.GetComponent<NetworkedDamageRelay>();
        if (relay != null)
        {
            relay.ReportDamageServerRpc(amount);
        }
        else
        {
            Debug.LogWarning($"[NetworkedDamage] {victimNetworkObject.name} has no {nameof(NetworkedDamageRelay)} — hit dropped (local damage would desync the server).");
        }
    }
}
