# Shooting System — AAA Data-Driven Hitscan

**Date:** 2026-08-31  
**Scope:** `Assets/_Game/Scripts/Items/Weapons/` + `Assets/_Game/Data/Weapons/` + `Assets/_Game/Prefabs/Weapons/`  
**Replaces:** `Handgun`/`HandgunState`/`HandgunContext`/`BulletProjectile` Rigidbody-per-bullet (deleted)

## Gold Standard

Unity Manual `RevisedGun` pooled projectile + `ScriptableObject` config + Netcode GO `ServerRpc` authority, UCC `Shootable` (`Trigger×Shooter×Clip`) and OnlineFPS `BaseWeaponSimulationJob` (hitscan + tracer, `fireMode/spread/recoil`).

## Design

Immutable `WeaponDefinition : ScriptableObject` (`Game/Weapon Definition`):

```
id, damage, range, fireRate, baseSpreadDegrees, pelletCount,
fireMode {Semi,Auto,Burst,Pump}, burstCount,
ammoType {NineMm,5.56,12G}, clipSize, defaultReserve, reloadDuration,
recoilForce, bloomPerShot, bloomRecovery, tracerPrefab
```

Assets: `Weapon_Pistol_9mm` (25 dmg, 80 m, 0.2 s, 12/36, 1 pellet, semi), `Weapon_Rifle_556` (18 dmg, 120 m, 0.09 s, 30/90, auto), `Weapon_Shotgun_12G` (12 dmg, 40 m, 0.55 s, 6/18, 8 pellets, pump). Stored under `Assets/_Game/Data/Weapons/`.

Runtime: `HitscanWeapon : MonoBehaviour, IFirearm` owns `clip/reserve/bloom/nextFire/isReloading` per-instance, not SO. `Prepare(clip,reserve)`, `SetFireRate`, `Shoot(aimWorld)`, `TriggerReload`, `AddReserveAmmo`, `ExecuteActualShoot`. Pure hitscan: `Physics.RaycastNonAlloc` with `NonAlloc[8]` buffer, spread `baseSpread+bloom` + `Random.insideUnitCircle`, bloom recovers `bloomRecovery * dt`. Pellets loop `pelletCount` times; closest hit wins; `NetworkedDamage.Apply` → `ReportDamageServerRpc`. Tracer pooled (`TracerVisual` 0.06 s flight + 0.08 s life, `TrailRenderer`).

`Weapon : Item, IWeapon` now holds optional `WeaponDefinition _definition`; `Awake` resolves `defClip/defReserve/defFireRate` from definition, `Prepare`, and forwards `SetFireRate`; delegates `InjectUI/AddReserveAmmo/RegisterEvents` to `HitscanWeapon` when present.

`WeaponInventory` (optional) holds `List<WeaponDefinition>`, spawns each via `ItemCatalog.GetItemPrefab(def.id)` under `_handHolder`, `SwitchTo/SwitchNext`, `InjectUI/RegisterEvents` broadcast.

`TracerVisual : MonoBehaviour` pooled, no collider, `Play(from,to)` lerps 0.06 s then returns.

`CharacterLocomotion:8` `EquippedWeaponPrefabName` → `"SM_Gun_Pistol"` (was `"FakeGun"`); still `EquipWeapon(string)` for fallback, inventory path takes precedence when present.

## Prefabs

`SM_Gun_Pistol` (now `HitscanWeapon` + `WeaponDefinition Pistol_9mm`), `SM_Gun_AssaultRifle` (298 KB) + `SM_Gun_Shotgun` (234 KB) created via `eval_file` duplicate of pistol, mesh swapped to `Ammo_Bullets/Meshes/SM_Gun_*.fbx` (scale 0.01). `ItemCatalog_Default:16` now 3 entries `[pistol,rifle,shotgun]`.

`Ammo_Bullets/Meshes` — 10 meshes (incl. `SM_Ammo_9x19/556/12Gauge`, `SM_Clip`, cases/boxes, `SM_FirstPersonProjectileMesh`) scale 0.01; textures `T_Ammo* BC/N/ORM` sRGB/normal/linear 2048. Not yet materialized beyond shell brass (reuse `M_Shell_Brass` template).

## Flow

`Input → CharacterBrain:170 OnShoot → CharacterLocomotion:209 HandleShoot → Weapon:69 TriggerShoot(aimTarget) → HitscanWeapon: Shoot → TryFire (cooldown, clip≤0→reload) → DoFire: origin=shootPoint+dir*0.1, for pellets: dir=ApplySpread(baseDir), RaycastNonAlloc, damage, SpawnTracer, clip--, bloom+=, recoil event → CharacterLocomotion:246 RigRecoil + WeaponEffects`. Reload: `TriggerReload → StartReload (isReloading, _reloadFinishTime = now+def.reloadDuration) → Update FinishReload → refill min(missing,reserve) → onReloadFinished`.

## Networking

Server-authoritative: `NetworkedDamage:10` + `NetworkedDamageRelay:19 ReportDamageServerRpc(RequireOwnership=false)` — hit detected locally, applied on server via `IDamageable.TakeDamage`, replicated via `NetworkedHealth`. FireRate/ammo validated locally; server applies health only. Future: `ServerRpc RequestFire` with tick validation.

## Tests

`HitscanWeaponPlayTests` (6) cover `Prepare`, `Shoot→clip--/onShoot`, `TriggerReload→refill`, `Hitscan_HitsDamageable`, `PelletCount_ShotgunHitsMultipleTimes`. `108 EditMode + 136 PlayMode` green. Legacy `HandgunPlayTests`/`BulletProjectilePlayTests` still present for compat; will be removed when Handgun files deleted.

## Verification

```
unity command recompile && recompile_status → completed
unity command run_tests --mode editmode/playmode --async_tests → 108 + 136 passed
Get-Content WeaponDefinition.asset | Select-String _fireRate,_pelletCount
```

## Files Added/Modified

- `Weapons/Config/WeaponDefinition.cs:1`, `WeaponFireMode.cs`, `AmmoType.cs`
- `Weapons/HitscanWeapon.cs:1`, `TracerVisual.cs:1`, `WeaponInventory.cs:1`
- `Weapon.cs:7` definition wiring
- `CharacterLocomotion.cs:8` id fix
- `Data/Weapons/Weapon_*.asset` (3), `Prefabs/SM_Gun_AssaultRifle/Shotgun.prefab`, `Ammo_Bullets/**` (10 FBX + 13 TGA), `ItemCatalog` 3 entries

## References

- `HitscanWeapon:132 ExecuteActualShoot`, `Weapon:22`, `CharacterBrain:170`, `NetworkedDamage:10`
- Unity Manual `performance-reusable-code` (RevisedGun), `class-ScriptableObject`, `com.unity.netcode.gameobjects`
- Opsive UCC `Shootable` modules, `OnlineFPS/weapons.md` simulation
