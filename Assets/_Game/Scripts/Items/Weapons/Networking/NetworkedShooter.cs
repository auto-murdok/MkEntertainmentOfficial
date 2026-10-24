using System;
using Unity.Netcode;
using UnityEngine;

// Server-authoritative shooting for one player (NGO client-server). Lives
// on the player prefab next to the brain; the weapon itself stays a purely
// local state machine (Handgun), and this component is the bridge:
//
// OWNER: the moment the local FSM launches a shot, the shot is reported as
// a ShotRequest (intent, not damage) and a predicted tracer is drawn
// instantly — the gun never waits on the network to feel responsive.
// Remote peers' copies of this component stay silent.
//
// SERVER: the request passes ShotValidator's gauntlet against this
// component's own ammo copy, then resolves as a hitscan raycast against
// hitbox poses rewound to the shooter's fire tick (HitHistory). Damage is
// applied here, on the authority, through the same IDamageable choke point
// the old bullet used — NetworkedHealth replication carries the result to
// every peer exactly as before.
//
// EVERYONE: the single confirmed-shot broadcast is what makes the shot
// visible — tracer + the gun's fire animation on the shooter's model.
// The owner skips it (it already played the predicted version) and only
// takes the server ammo sync. Hit/kill confirmation travels on a separate
// owner-only channel, so latency never makes the gun itself feel dead.
//
// This replaces the bullet's damage path in networked play
// (NetworkedDamageRelay's trusting float RPC). The relay remains for
// melee/other sources until those get request types of their own, and the
// pooled BulletProjectile remains the offline/test path and the future
// slow-ordnance toolbox.
public class NetworkedShooter : NetworkBehaviour
{
    [SerializeField] private WeaponData _weaponData;

    // Wired on the player prefab. Handed to TracerView so the tracer shader
    // is asset-referenced and can never be stripped from a build.
    [SerializeField] private Material _tracerMaterial;

    // Owner-only: raised when the server confirms this peer's shot hit
    // (true = the hit killed). The seam for hitmarker UI/audio.
    public event Action<bool> HitConfirmed;

    private WeaponStats Stats => _weaponData != null ? _weaponData.Stats : WeaponStats.Handgun;

    private Handgun _handgun;
    private ServerWeaponState _serverState; // server only
    private uint _nextShotId = 1;

    // World geometry is static in the arena, so it is tested at current
    // poses; damageable actors live on the player/zombie layers and are
    // tested exclusively through their rewound capsules.
    private static int GeometryMask => ~((1 << 6) | (1 << 7));

    public override void OnNetworkSpawn()
    {
        if (_tracerMaterial != null)
        {
            TracerView.SharedMaterial = _tracerMaterial;
        }

        _handgun = GetComponentInChildren<Handgun>();
        if (_handgun == null)
        {
            Debug.LogWarning($"[{name}] NetworkedShooter found no Handgun in children — shooting stays local-only.");
            return;
        }

        if (IsOwner)
        {
            _handgun.useNetworkShot = true;
            _handgun.ShotLaunched += OnLocalShotLaunched;
            _handgun.ReloadStarted += OnLocalReloadStarted;
        }

        if (IsServer)
        {
            _serverState = new ServerWeaponState();
            _serverState.Init(Stats);
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsOwner && _handgun != null)
        {
            _handgun.ShotLaunched -= OnLocalShotLaunched;
            _handgun.ReloadStarted -= OnLocalReloadStarted;
            _handgun.useNetworkShot = false;
        }
    }

    private void Update()
    {
        // Reloads commit on the server's clock even when no shot forces the
        // bookkeeping, so the owner's ammo sync is never late.
        if (IsServer && _serverState != null &&
            ShotValidator.CommitReloadIfDue(_serverState, NetworkManager.LocalTime.Time))
        {
            ReloadCommittedRpc(_serverState.Clip, _serverState.Reserve);
        }
    }

    // --- Owner: report intent, play predicted FX ---------------------------

    private void OnLocalShotLaunched(Vector3 origin, Vector3 direction)
    {
        uint shotId = _nextShotId++;

        // Predicted tracer: endpoint from a local raycast against current
        // poses. Cosmetic only — the server's confirmed endpoint is what
        // everyone else sees, and damage never follows this ray.
        Vector3 end = origin + direction * Stats.Range;
        if (Physics.Raycast(origin, direction, out RaycastHit hit, Stats.Range, ~0, QueryTriggerInteraction.Ignore) &&
            !hit.transform.IsChildOf(transform))
        {
            end = hit.point;
        }
        TracerView.Spawn(origin, end);

        SubmitShotRpc(new ShotRequest
        {
            WeaponId = Stats.WeaponId,
            ShotId = shotId,
            Origin = origin,
            Direction = direction,
            ClientTick = NetworkManager.LocalTime.Tick,
            Seed = shotId * 2654435761u, // Knuth multiplicative hash: stable per-shot stream
        });
    }

    private void OnLocalReloadStarted()
    {
        SubmitReloadRpc();
    }

    // --- Server: validate, resolve, confirm --------------------------------

    [Rpc(SendTo.Server)]
    private void SubmitShotRpc(ShotRequest request, RpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId || _serverState == null)
        {
            return; // a peer may only fire its own weapon
        }
        if (request.WeaponId != Stats.WeaponId || request.Direction.sqrMagnitude < 0.5f)
        {
            return;
        }

        double now = NetworkManager.LocalTime.Time;
        Vector3 serverMuzzle = _handgun != null ? _handgun.muzzleWorldPosition : transform.position + Vector3.up * 1.4f;
        ShotRejection verdict = ShotValidator.TryAccept(_serverState, Stats, now, request.Origin, serverMuzzle);
        if (verdict != ShotRejection.None)
        {
            return; // rejected silently: the owner's predicted tracer was cosmetic only
        }

        Vector3 direction = request.Direction.normalized;
        ResolveShot(request, direction, out Vector3 endPoint, out NetworkObject victim, out bool killed);

        ShotConfirmedRpc(request.ShotId, request.Origin, endPoint,
            victim != null ? victim.NetworkObjectId : 0UL, _serverState.Clip, _serverState.Reserve);
        if (victim != null)
        {
            ShotHitRpc(request.ShotId, killed);
        }
    }

    [Rpc(SendTo.Server)]
    private void SubmitReloadRpc(RpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId || _serverState == null)
        {
            return;
        }

        ShotValidator.TryStartReload(_serverState, Stats, NetworkManager.LocalTime.Time);
        // The commit (and the owner's ammo sync) happens in Update when the
        // server timer elapses — an interrupted client animation can never
        // lose or duplicate the refill.
    }

    private void ResolveShot(ShotRequest request, Vector3 direction,
        out Vector3 endPoint, out NetworkObject victim, out bool killed)
    {
        victim = null;
        killed = false;

        int currentTick = NetworkManager.LocalTime.Tick;
        int targetTick = Mathf.Clamp(request.ClientTick,
            currentTick - HitHistory.MaxRewindTicks, currentTick);

        float bestDistance = Stats.Range;
        HitHistoryRecorder bestRecorder = null;
        foreach (HitHistoryRecorder recorder in HitHistoryRecorder.ServerRecorders)
        {
            if (recorder.NetworkObject == NetworkObject)
            {
                continue; // never shoot yourself
            }
            if (recorder.TryRewindRaycast(targetTick, request.Origin, direction, bestDistance, out float hitDistance))
            {
                bestDistance = hitDistance;
                bestRecorder = recorder;
            }
        }

        float geometryDistance = float.PositiveInfinity;
        Vector3 geometryPoint = request.Origin + direction * Stats.Range;
        if (Physics.Raycast(request.Origin, direction, out RaycastHit hit, Stats.Range,
                GeometryMask, QueryTriggerInteraction.Ignore))
        {
            geometryDistance = hit.distance;
            geometryPoint = hit.point;
        }

        if (bestRecorder != null && bestDistance <= geometryDistance)
        {
            endPoint = request.Origin + direction * bestDistance;
            victim = bestRecorder.NetworkObject;

            IDamageable damageable = bestRecorder.GetComponent<IDamageable>();
            if (damageable != null)
            {
                using (CombatLog.BeginSource("Bullet"))
                {
                    damageable.TakeDamage(Stats.Damage);
                }
                killed = damageable.remainingHitPoints <= 0f;
            }
        }
        else
        {
            endPoint = geometryPoint;
        }
    }

    // --- Everyone: make the confirmed shot visible --------------------------

    [Rpc(SendTo.ClientsAndHost)]
    private void ShotConfirmedRpc(uint shotId, Vector3 origin, Vector3 endPoint,
        ulong hitObjectId, int serverClip, int serverReserve)
    {
        if (IsOwner)
        {
            // Already played the predicted tracer/animation at fire time;
            // only the authority's ammo bookkeeping is news.
            _handgun?.SyncAmmo(serverClip, serverReserve);
            return;
        }

        TracerView.Spawn(origin, endPoint);
        _handgun?.PlayFireAnimation();
    }

    [Rpc(SendTo.Owner)]
    private void ShotHitRpc(uint shotId, bool killed)
    {
        HitConfirmed?.Invoke(killed);
    }

    [Rpc(SendTo.Owner)]
    private void ReloadCommittedRpc(int serverClip, int serverReserve)
    {
        _handgun?.SyncAmmo(serverClip, serverReserve);
    }
}
