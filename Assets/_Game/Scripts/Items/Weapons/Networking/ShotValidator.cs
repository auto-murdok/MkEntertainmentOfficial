using UnityEngine;

// Server-side weapon state for one shooter: the authority's own copy of the
// ammo economy. The client's Handgun FSM keeps identical numbers for feel;
// this copy is the one that decides. Every accepted shot and every reload
// re-syncs the owner from these values (see NetworkedShooter), so the two
// copies heal any drift on the very next action.
public sealed class ServerWeaponState
{
    public int Clip;
    public int Reserve;
    public double LastShotTime = double.NegativeInfinity;
    public bool Reloading;
    public double ReloadEndTime;

    private int _maxClip;

    public void Init(in WeaponStats stats)
    {
        _maxClip = stats.ClipSize;
        Clip = stats.ClipSize;
        Reserve = stats.ReserveAmmo;
        LastShotTime = double.NegativeInfinity;
        Reloading = false;
        ReloadEndTime = 0d;
    }

    // The reload refill, mirroring HandgunReloadingState's exit math exactly
    // (an int.MaxValue reserve behaves as infinite and is left untouched).
    internal void CommitReload()
    {
        int taken = Mathf.Min(_maxClip - Clip, Reserve);
        Clip += taken;
        if (Reserve != int.MaxValue)
        {
            Reserve -= taken;
        }
        Reloading = false;
    }
}

public enum ShotRejection
{
    None,
    FireRate,
    EmptyClip,
    Reloading,
    OriginTooFar,
}

// The validation gauntlet every shot request passes on the server, in
// order: reload bookkeeping -> reloading? -> ammo? -> fire rate against the
// network clock -> claimed muzzle origin vs the server's own copy of the
// gun. Pure logic over WeaponStats + ServerWeaponState (no networking
// types), so the whole gauntlet is EditMode-testable.
public static class ShotValidator
{
    // The client's muzzle and the server's copy of it legitimately differ by
    // replication lag in the aim pose; 2.5 m absorbs that without letting a
    // shot originate somewhere the player is not.
    public const float OriginToleranceMeters = 2.5f;

    // Network clocks and frame quantization make an exact-interval check
    // flaky at the boundary; the tolerance is far below any useful cheat.
    public const double FireRateToleranceSeconds = 0.02d;

    public static ShotRejection TryAccept(ServerWeaponState state, in WeaponStats stats,
        double serverTime, Vector3 claimedOrigin, Vector3 serverMuzzle)
    {
        CommitReloadIfDue(state, serverTime);

        if (state.Reloading)
        {
            return ShotRejection.Reloading;
        }
        if (state.Clip <= 0)
        {
            return ShotRejection.EmptyClip;
        }
        if (serverTime - state.LastShotTime < stats.FireInterval - FireRateToleranceSeconds)
        {
            return ShotRejection.FireRate;
        }
        if ((claimedOrigin - serverMuzzle).sqrMagnitude > OriginToleranceMeters * OriginToleranceMeters)
        {
            return ShotRejection.OriginTooFar;
        }

        state.Clip--;
        state.LastShotTime = serverTime;
        return ShotRejection.None;
    }

    // Same gate as the client FSM's TriggerReload: only from a partial clip
    // with reserve left, and never twice.
    public static bool TryStartReload(ServerWeaponState state, in WeaponStats stats, double serverTime)
    {
        CommitReloadIfDue(state, serverTime);

        if (state.Reloading || state.Clip >= stats.ClipSize || state.Reserve <= 0)
        {
            return false;
        }

        state.Reloading = true;
        state.ReloadEndTime = serverTime + stats.ReloadDuration;
        return true;
    }

    // Returns true exactly once: on the call that applies the refill.
    public static bool CommitReloadIfDue(ServerWeaponState state, double serverTime)
    {
        if (!state.Reloading || serverTime < state.ReloadEndTime)
        {
            return false;
        }

        state.CommitReload();
        return true;
    }
}
