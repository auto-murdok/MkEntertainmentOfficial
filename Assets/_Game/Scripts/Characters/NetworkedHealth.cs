using Unity.Netcode;
using UnityEngine;

// Server-authoritative health replication (Boss Room / NGO Bitesize pattern):
// the server computes all damage (zombie AI runs host-side) and writes the
// NetworkVariable; every other peer mirrors the replicated value into its
// local brain, so HP, HUD and death behave identically everywhere.
//
// The local ActorBrainBase remains the source of truth for this peer's own
// pipeline (CombatLog, Damaged/Died events, ragdoll). The server's writes come
// from its local brain; clients never write.
public class NetworkedHealth : NetworkBehaviour
{
    private readonly NetworkVariable<float> _health = new NetworkVariable<float>(
        0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private ActorBrainBase _brain;

    public override void OnNetworkSpawn()
    {
        _brain = GetComponent<ActorBrainBase>();
        _health.OnValueChanged += OnHealthChanged;

        if (IsServer)
        {
            // Publish the authority's starting HP (zombie Awake already set it).
            _health.Value = _brain != null ? _brain.remainingHitPoints : 0f;
        }
        else if (_brain != null)
        {
            _brain.MirrorHitPoints(_health.Value);
        }
    }

    public override void OnNetworkDespawn()
    {
        _health.OnValueChanged -= OnHealthChanged;
    }

    private void Update()
    {
        // Damage events only fire on the receiving brain; the server mirrors
        // its local pipeline into the variable (change-checked by NGO).
        if (IsServer && _brain != null && _health.Value != _brain.remainingHitPoints)
        {
            _health.Value = _brain.remainingHitPoints;
        }
    }

    private void OnHealthChanged(float previousValue, float newValue)
    {
        if (IsServer || _brain == null)
        {
            return; // the server's brain already applied the damage locally
        }
        _brain.MirrorHitPoints(newValue);
    }
}
