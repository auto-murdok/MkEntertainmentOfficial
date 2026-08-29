# Shooting Engine Notes

Design decisions, verified behaviors and debugging workflows for the shooting
engine (`Handgun`, `BulletProjectile`, `Weapon`, `DebugHud`). All items below
were verified in Play Mode on Unity 6000.3 via `unity-cli`.

---

## ⚠️ AIM FIRST — the weapon points down when not aiming (by design)

**You must be aiming (RMB / crosshair active) for shots to fly toward the
crosshair.** Shooting without aiming is not a bug, but the bullets go into the
ground:

- While aiming, the world aim point (`CharacterLocomotion._aimTarget`, the
  `AimTarget` child of `PlayerCoreComponents`) tracks the center-screen ray, and
  `Handgun.Shoot` stores the direction toward it (`HandgunContext.aimDirection`).
- While **not** aiming, the character is in its rest pose — the gun hangs
  pointing **down**. `Handgun.Shoot` falls back to the muzzle's forward
  (`_shootPoint.forward`), so an unaimed shot simply fires into the dirt.
- This is intended "realistic" behavior: raise the weapon before firing.

Related wiring (do not break):
- `PlayerSpawner.Awake` re-injects `AimTarget` and every
  `MultiAimConstraint.sourceObjects` at spawn time (prefab → scene references
  are stripped on save), then rebuilds the `RigBuilder` graph.
- `PlayerCoreUI._aimTarget` toggles the *UI crosshair object* — it is **not**
  the world aim point.

---

## Projectile pipeline (gold-standard pattern)

- **Pooling:** `Handgun` owns an `ObjectPool<BulletProjectile>`
  (`UnityEngine.Pool`, Unity Manual "Pooling and reusing objects" pattern).
  Bullets are never `Instantiate`/`Destroy`-ed per shot.
- **Physics teleports:** pooled rigidbodies are moved with
  `Rigidbody.position/rotation`, *not* transform-only moves — a transform move
  on a freshly re-activated body left the physics pose at the pool's creation
  point (bullets "hit the ground" at the world origin). See
  `BulletProjectile.Launch`.
- **CCD:** `CollisionDetectionMode.ContinuousSpeculative` is set once in
  `Awake`. `ContinuousDynamic` skips kinematic bodies, which let 50 m/s bullets
  tunnel through ragdoll limb colliders; speculative contacts catch all body
  types.
- **Single hit / single release:** one bullet can produce several
  `OnCollisionEnter` callbacks in one physics step (one per overlapping limb
  collider). `_hasHit` guarantees exactly one damage event per flight;
  `_isReleased` guarantees one pool release.
- **Shooter immunity:** `Launch(..., owner)` calls `Physics.IgnoreCollision`
  against every collider under the owner root — the bullet spawns inside the
  player rig and must never hit the shooter (this used to silently damage the
  player).
- **Muzzle exit offset:** bullets spawn `0.1 m` along the shot direction so
  they start clear of the gun geometry.

## Config flow (single source of truth)

- `Weapon._fireRate` → `Handgun.SetFireRate` → `HandgunContext.fireRate` →
  rechambering time in `HandgunShootingState` (no hardcoded cadence).
- Projectile damage lives on the **`Projectile.prefab`** (`BulletProjectile._damage`).
  Do not re-introduce a per-shot damage override from `Weapon`.
- `HandgunContext.reserveAmmo` is `int.MaxValue` (= infinite) until Ammo
  pickups are wired into an inventory; `HandgunReloadingState` already pulls
  from the reserve correctly.

## Input semantics

Input System button actions notify once per phase (started / performed /
canceled). `CharacterBrain.OnNotify` gates `Shoot` and `Reload` on
`inputValue.isPressed` — without that gate a single click fires twice.

---

## Debug HUD (F3)

`DebugHud` (in `Game.UI`) is attached to the player instance by
`PlayerSpawner.Awake`. It builds its own screen-space overlay canvas (no prefab
or scene authoring needed) and is toggled with **F3**.

Shown, refreshed at 10 Hz with the allocation-free
`TMP_SetText(StringBuilder)` path:

- FPS
- Player HP
- Player FSM state
- Gun FSM state, clip/max, reserve (`INF` = infinite)
- Live bullet count
- **Combat log** — last 6 events, newest last

### CombatLog (`Game.Core`)

Static fixed-capacity (8) ring buffer of formatted entries; the HUD copies it
into a preallocated buffer.

- Damage reports flow through exactly one choke point:
  `ActorBrainBase.ApplyDamage` → `CombatLog.ReportDamage` (victim + remaining
  HP). The attacker label comes from a scoped
  `CombatLog.BeginSource("Bullet" | "ZombieBite")` around the `TakeDamage`
  call.
- Non-damaging physics events are reported as impacts, e.g. a bullet hitting
  scenery: `Bullet hit Plane [Default] at (x, y, z) — no IDamageable`. This is
  the fastest way to diagnose "bullets do nothing" — the log shows exactly what
  every bullet collided with and where.

### Debug workflow for shooting issues

1. Enter Play Mode, toggle the HUD with F3 (or read
   `CombatLog.CopyRecent` via `unity-cli eval_file`).
2. Fire once and read the log: you should see exactly one
   `Bullet launched from … dir …` line followed by either a
   `Bullet -> <victim> took 25.0` damage line or a `Bullet hit …` impact line.
3. `Bullet launched … dir (…, …, -1)` with a ground impact right below the
   muzzle = **shooting without aiming** (rest pose points down — see top of
   this document).
