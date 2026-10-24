using System;
using UnityEngine;

// The single source of truth for a weapon's gameplay numbers, shared by the
// client-side firearm FSM (feel) and the server-side shot validator
// (authority). Before this, the numbers were split three ways — Weapon
// serialized fields, the projectile prefab, and code constants — and the
// server had no copy of them at all, so it could not validate anything.
//
// Values are plain data (no Unity networking types) so the validation rules
// built on them stay unit-testable in EditMode.
[Serializable]
public struct WeaponStats
{
    public int WeaponId;
    public float Damage;
    // Seconds between shots (the FSM's rechamber cadence).
    public float FireInterval;
    public int ClipSize;
    // Starting reserve pool a reload pulls from (int.MaxValue = infinite).
    public int ReserveAmmo;
    public float ReloadDuration;
    // Maximum engagement distance; also the hitscan ray length.
    public float Range;
    // Cone half-angle in degrees. 0 = the shot is exact (current behaviour);
    // spread is applied identically on every peer from the shot's seed.
    public float SpreadDegrees;

    // The shipped handgun, exactly as tuned today: damage 25 (was
    // BulletProjectile._damage on the prefab), 0.2 s cadence, clip 5,
    // reserve 45 (Weapon fields), 2 s reload (HandgunReloadingState
    // constant), 30 m (was BulletProjectile._maxTravelDistance). A test pins
    // these numbers so a tuning change is always a deliberate edit here.
    public static WeaponStats Handgun => new WeaponStats
    {
        WeaponId = 1,
        Damage = 25f,
        FireInterval = 0.2f,
        ClipSize = 5,
        ReserveAmmo = 45,
        ReloadDuration = 2f,
        Range = 30f,
        SpreadDegrees = 0f,
    };
}
