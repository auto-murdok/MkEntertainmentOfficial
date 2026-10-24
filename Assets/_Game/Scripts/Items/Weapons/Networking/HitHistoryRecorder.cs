using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

// Server-side companion to HitHistory: lives on every damageable actor
// prefab (player + zombie). On the server it samples the actor's capsule
// once per network tick; clients run it inert. NetworkedShooter consults
// the static registry when resolving a shot against rewound poses.
//
// The capsule is approximated from live physics bounds each sample, so it
// follows whatever collider the actor actually has (CapsuleCollider,
// CharacterController); actors with neither fall back to their NavMeshAgent
// dimensions. Sampling from Update keyed on the tick value keeps this free
// of tick-system event plumbing — one sample per tick is the contract.
public class HitHistoryRecorder : NetworkBehaviour
{
    // Server-only in practice: populated in OnNetworkSpawn when IsServer.
    public static readonly List<HitHistoryRecorder> ServerRecorders = new List<HitHistoryRecorder>();

    private HitHistory _history;
    private int _lastRecordedTick = int.MinValue;
    private Collider _capsuleSource;
    private NavMeshAgent _agent;

    public override void OnNetworkSpawn()
    {
        if (!IsServer)
        {
            return;
        }

        _capsuleSource = GetComponent<CapsuleCollider>();
        if (_capsuleSource == null)
        {
            _capsuleSource = GetComponent<CharacterController>();
        }
        _agent = GetComponent<NavMeshAgent>();

        _history = new HitHistory();
        RecordSample(NetworkManager.LocalTime.Tick); // never sample an empty buffer
        ServerRecorders.Add(this);
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer)
        {
            ServerRecorders.Remove(this);
        }
    }

    private void Update()
    {
        if (!IsServer || _history == null)
        {
            return;
        }

        int tick = NetworkManager.LocalTime.Tick;
        if (tick != _lastRecordedTick)
        {
            RecordSample(tick);
        }
    }

    private void RecordSample(int tick)
    {
        _lastRecordedTick = tick;

        Vector3 center;
        float radius;
        float halfHeight;

        if (_capsuleSource != null)
        {
            Bounds bounds = _capsuleSource.bounds;
            center = bounds.center;
            radius = Mathf.Max(bounds.extents.x, bounds.extents.z);
            halfHeight = Mathf.Max(0f, bounds.extents.y - radius);
        }
        else if (_agent != null)
        {
            center = transform.position + Vector3.up * (_agent.height * 0.5f);
            radius = _agent.radius;
            halfHeight = Mathf.Max(0f, _agent.height * 0.5f - radius);
        }
        else
        {
            center = transform.position + Vector3.up * 0.9f;
            radius = 0.5f;
            halfHeight = 0.4f;
        }

        _history.Record(tick, center, radius, halfHeight);
    }

    // The rewound hit test for one shot. `tick` must already be clamped by
    // the caller (see HitHistory.MaxRewindTicks).
    public bool TryRewindRaycast(int tick, Vector3 origin, Vector3 direction, float maxDistance, out float distance)
    {
        if (_history == null)
        {
            distance = 0f;
            return false;
        }

        (Vector3 center, float radius, float halfHeight) = _history.Sample(tick);
        return HitHistory.RaycastCapsule(origin, direction, center, radius, halfHeight, maxDistance, out distance);
    }
}
