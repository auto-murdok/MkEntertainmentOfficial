using Unity.Netcode;
using UnityEngine;

// Server-authoritative damage relay for networked actors (NGO client-server
// gold standard): a hit is DETECTED on the shooter's peer (bullet collision is
// peer-local physics) but APPLIED on the server, which owns every actor's
// health — NetworkedHealth then replicates the result to everyone. Without
// this, a client's bullets only damage its local copies and the host's actors
// never die ("zombies are not dying").
//
// Lives on every shootable actor prefab (player + zombie).
public class NetworkedDamageRelay : NetworkBehaviour
{
    // Any peer may report a hit (RequireOwnership=false — the shooter does not
    // own the zombie it shot). From the host this executes locally; from a
    // client it travels as a ServerRpc. The bullet is the only ranged damage
    // source, so the combat-log source is fixed here.
    [ServerRpc(RequireOwnership = false)]
    public void ReportDamageServerRpc(float amount)
    {
        using (CombatLog.BeginSource("Bullet"))
        {
            GetComponent<IDamageable>()?.TakeDamage(amount);
        }
    }
}
