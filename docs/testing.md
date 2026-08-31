# Testing Guide — Unity 6000.3 / Unity Test Framework 1.6

This project keeps a regression-proof test suite over all gameplay logic. Tests live in
`Assets/_Game/Tests/` and are split into two NUnit assemblies following the official
Unity Test Framework (UTF) recommendations for Unity 6:

| Assembly | Location | Platform | Covers |
|---|---|---|---|
| `Game.Tests.EditMode` | `Assets/_Game/Tests/EditMode/` | Editor only | Pure logic, statics, utils, math, serialization-driven values |
| `Game.Tests.PlayMode` | `Assets/_Game/Tests/PlayMode/` | Editor only (all-platforms + `UNITY_INCLUDE_TESTS` define constraint — compiles in the Editor, skipped by player builds) | MonoBehaviour lifecycle (`Awake`/`Start`/`Update`), physics, pooling, singletons |

Both asmdefs reference all game assemblies (`Game.Core`, `Game.Characters`, `Game.Items`,
`Game.UI`, `Game.Composition`) plus `UnityEngine.TestRunner` / `UnityEditor.TestRunner`
with `nunit.framework.dll` as a precompiled reference (`overrideReferences: true`).

## Running the tests

Via the live editor (preferred — see AGENTS.md `unity-cli` section):

```bash
unity command run_tests --mode editmode --async_tests
unity command run_tests --mode playmode --async_tests
unity command test_status          # poll until "completed"
```

Via the editor UI: `Window > General > Test Runner` (EditMode / PlayMode tabs).

## What is covered (224 tests: 94 EditMode + 130 PlayMode, all green)

Note: the networked bite relay (`IPlayerBiteRelay`), bite-state mirror and
`isPreparing` replication are session-dependent (real `NetworkManager` +
spawned objects) and are verified by two-instance play sessions
(`docs/networking_notes.md`), not unit tests — testing.md's seams cannot
fake NGO ownership/spawn state. Same for the overlay input gating
(`PlayerInputGate`): simulating Input System devices against a real
`PlayerInput` in PlayMode tests hung the runner (see
`docs/networking_notes.md`, milestone 4 gotcha). `GameStateManagerPlayTests`
still covers the time-freeze behavior (`freezeTimeOnGameOver` defaults true;
`PlayerSpawner` sets it false in networked scenes — verified by two-instance
sessions).

- **Core**
  - `CombatLog` — ring buffer capacity/overflow, `CopyRecent` truncation, `BeginSource`
    scoping & nesting, damage/impact formatting, destroyed-victim placeholder.
  - `Subject<TAction,TValue>` / `CharacterUIController` — add/remove/notify, reverse
    notification order, no dedupe (documented gotcha), destroyed-observer skipping,
    self-removal during notify.
  - `StateMachine<TStateKey,TContext>` — deferred transitions (first request wins per
    frame), exit→enter ordering, `OnCommonUpdate`/`UpdateState`/`CheckTransitions`
    pipeline order, `OnStateChanged`, unregistered-state error, global transition guard
    (death) including override of a same-frame pending transition (`ChangeState(force)`),
    initial state from non-default enum, empty-state assertion.
  - `InteractableRegistry` (SO) — register/unregister/overwrite, by-id and by-reference
    interaction (both sides notified), `TryGet`, null safety, clean-slate reset on unload.
  - `NetworkedPlayerPrefabTests` — the player prefab's networking contract:
    `NetworkTransform` is **owner-authoritative** (server authority makes
    client movement snap back — see `docs/networking_notes.md` lesson 8),
    `NetworkAnimator` is owner-authoritative with the Animator wired
    (lesson 9), `BuildRemoteRig` wires every `MultiAimConstraint` to the
    local `RemoteAimTarget` (idempotent), `NetworkedHealth` is present
    (missing it makes player HP/death never replicate), plus `NetworkObject` +
    `NetworkedPlayerComposition` presence.
  - `VoidEventChannel` / `BoolEventChannel` (SO) — Raise/subscriber notification,
    unsubscribe semantics, no-throw with no subscribers, bool payload fan-out.
  - `ArchitectureConformanceTests` — reflection fitness function: no game type may
    expose a public static self-referencing member (singleton/service-locator shape),
    enforcing the SO-architecture rules by suite.
  - `RagdollUtils` — kinematic toggling, callbacks, empty hierarchies.
- **Items**
  - `Ammo` — clip draw math (partial/exact/overflow/empty, parameterized).
  - `Item` / `ItemCatalog` (SO) — id lookup, null/empty/unknown id handling.
  - `Handgun` / `Weapon` / handgun states — state registration, `Prepare`, fire-rate
    fallback, aim-direction resolution (muzzle-forward fallback), trigger/reload
    gating, dry-fire vs live-fire `onShoot`, empty-clip → auto-reload transition,
    out-of-ammo (0 clip + 0 reserve) stays Ready, reload refill math (finite &
    de-facto infinite reserve), reload UI notification, `Weapon` → firearm forwarding,
    `AddReserveAmmo` (positive only), dry fire never consumes a round.
  - `AmmoPickup` (PlayMode) — grants reserve to a `Weapon`-carrying target and is
    consumed exactly once, non-weapon targets ignored, null-target safety, both
    ZombieData archetypes have the drop prefab assigned.
  - `BulletProjectile` (PlayMode physics) — launch velocity, hit scoring (exactly one
    damage per flight), self-destruct without pool, owner-collider ignore, max-range
    release, pooled re-flight (same instance scores again — guards the stale-contact
    fix in `BulletProjectile.OnCollisionEnter`).
- **Characters**
  - `ActorBrainBase` — damage/death flag, CombatLog reporting, death hook → ragdoll →
    interactable deregistration, `DestroyActorCore`, id/position/victimHook,
    delayed health regeneration (delay gate, heal after delay, cap, dead/full-health
    no-ops, non-positive rate), `MirrorHitPoints` (server-replicated HP: drops
    route through the damage pipeline, heals silent, dead-actor no-op, and a
    mirrored kill runs the ragdoll teardown + moves the corpse off the
    LocalPlayer layer so zombie vision cannot scan it).
  - `NetworkedZombiePrefabTests` — the zombie prefab's networking contract:
    `NetworkTransform`/`NetworkAnimator` stay **server-authoritative**
    (host-simulated AI), `NetworkAnimator` wired, `NetworkedHealth` +
    `NetworkedZombieController` + `NetworkedDamageRelay` present, and
    `Zombie.prefab` registered in the NetworkPrefabs list (unregistered
    prefabs break client-side spawning).
  - `PlayerData` / `ZombieData` — health-regen defaults positive, default obstacle
    mask constant, hand-attack defaults non-zero.
  - `AnimatorUtils` — parameter hashes, `DampFactor` exponential math, null-animator safety.
  - `CameraUtils` — mouse vs controller thresholds, yaw accumulation/wrapping,
    pitch clamping, sensitivity scaling. The controller **above-threshold
    rotation** case is a PlayMode test (`CameraUtilsPlayTests`) because
    controller look scales input by `Time.deltaTime`, which is 0 in EditMode.
  - `AIDetectionUtils` — explicit full-cone field-of-view angles (half-cone edges,
    invalid-range fallback to default cone), obstacle linecast, null origin;
    nearest-survivor selection over real colliders (`AIDetectionPlayTests`).
  - `ZombieBehavior` attack selection (`CanVictimBeBitten`) — free/plain/pinned
    victims, pin held by self (own bite continues) vs by another attacker
    (hand-attack fallback), `ZombieData` hand-attack defaults.
  - `ZombieHandAttackState` (PlayMode) — pinned victim swings instead of biting,
    exactly one damage event per swing at the hit frame, returns to Idle with
    cooldown armed, hand-trigger redirect, duplicate interactions suppressed,
    own-bite race regression.
  - `LayerUtils` — recursive layer assignment incl. inactive children, unknown-layer warning.
- **Game flow** — `GameStateManager` (PlayMode): death → GameOver via the
  `PlayerDiedChannel`, idempotent `SetGameOver`, cursor release, time freeze
  after the collapse window, spawner stop via `SpawningEnabledChannel`,
  game-over overlay canvas; `ZombieSpawner` spawning toggle.
  `MainMenuController` (PlayMode): overlay canvas build (idempotent),
  start-game event raised once with double-start guard, quit/transition event;
  **HOST/JOIN** buttons set `NetworkSession.desiredMode` with the same
  event/transition-guard contract. `PauseMenuController` (PlayMode): overlay
  build (Resume/Quit buttons, closed by default), open/close cursor contract,
  toggle, resume event, quit-to-menu event + double-quit guard (`_quitLoadsMenuScene`
  seam keeps the scene load out of tests).
- **UI** — `CharacterUIContext` factories, `CharacterUIElement`, `UpdateUI` notification.

## Conventions & hard-won lessons (read before writing tests)

1. **EditMode vs PlayMode choice.** In the Editor, `AddComponent` does **not** run
   `Awake`, and `AddComponent` of a *closed generic* MonoBehaviour (e.g.
   `Subject<string,int>`) returns **null** and logs
   *"Generic MonoBehaviours are not supported"*. Therefore:
   - Test generic bases via a **non-generic subclass** in the test assembly
     (`class TestFsm : StateMachine<PlayKey, PlayContext> { }`).
   - Anything relying on `Awake`/`Start`/`Update` or physics is a PlayMode test.
2. **Singleton tests must clean up synchronously.** Use `Object.DestroyImmediate` in
   PlayMode teardowns for anything involved with statics (`Instance`) — deferred
   `Destroy` leaks a fake-alive static across fixtures and instance IDs get reused,
   corrupting dictionary-keyed registries.
3. **`Time.deltaTime` is unreliable in EditMode** (it is 0 in a plain `[Test]` —
    editor frames are not ticked), and animator value reads
   (`GetFloat`/`GetLayerWeight`) are unreliable without a controller — assert the math
   (`DampFactor`) and no-throw paths in EditMode; anything deltaTime-dependent
   (e.g. controller look in `CameraUtils` → `CameraUtilsPlayTests`) or
   animator-value assertions go in PlayMode.
4. **PlayMode tests run in the currently open scene.** Never assume an empty scene:
   spawn physics objects in clear airspace and give each physics test its own "lane"
   (this suite uses y=500…900) so tests cannot shoot each other.
5. **Pooled projectiles: pose before activate.** Re-activating a pooled body at its
   dormant pose registers stale physics contacts (phantom hit on the previous target).
   `BulletProjectile.Launch` now corrects the pose *then* activates, and
   `OnCollisionEnter` rejects contacts behind the bullet's velocity vector (a valid
   hit for a forward-only projectile is always ahead of it). `PooledBullet_SecondFlight_
   ScoresDamageAgain` is the regression test for this.
6. **Private serialized fields** (`_quantity`, `_id`, `_bulletPrefab`,
   `currentStateEnum`) are set with `SerializedObject` + `ApplyModifiedProperties`.
7. **Error paths** use `LogAssert.Expect(LogType.Error, regex)` — unhandled error logs
   fail PlayMode tests.
8. **Static state** (`CombatLog`) persists across tests — never assert global
   emptiness; always search for your unique marker. SO channel/registry fixtures are
   created per-test via `ScriptableObject.CreateInstance` and destroyed in teardown.
9. **AI vision tests: keep fake victims undetectable.** `SearchForSurvivors`
   overwrites `_context.target` every 0.15 s and auto-attacks — leave the fake
   victim on the `Default` layer (outside the detection mask) and set
   `_context.target` manually. Anything that must survive scans has to be
   captured outside the target field (see `StartHandAttack` → `_context.interactable`).
10. **`NavMeshAgent.ResetPath()` errors off-mesh** ("can only be called on an
    active agent that has been placed on a NavMesh") and fails PlayMode tests
    as an unhandled log — guard with `agent.isOnNavMesh`.
11. **Controller-less `Animator` is safe for parameter writes** (int-hash
    `SetTrigger`/`SetFloat` are silent) — test rigs need only
    `Animator` + `NavMeshAgent` + the FSM components.
12. **Never launch an EditMode run while a PlayMode run is still winding down**
    (scene teardown) — the runner wedges at `status: "running"` forever with
    *"Test tree is not available for PostbuildCleanupTask"* in the console.
    Recover with `editor_stop` + re-run, and parse `test_status` with
    `ConvertFrom-Json`, not `-match` (escaped quotes false-fail).
13. **A `[UnityTest]` body may execute a full frame after `SetUp`.** Components
    added in `SetUp` get their `Start` on the next frame — which can run
    *before* the first coroutine step. Tests that must prevent a component's
    `Start` behaviour (e.g. `ZombieSpawner.SpawnInitialWave`) must recreate the
    component inside the test body with the guard state pre-set via
    `SerializedObject` (`ZombieSpawnerSpawnTests.RecreateSpawner`). Failing
    that, the wave fires first and "expect 0" assertions see the wave.
14. **Keep test assemblies out of player builds.** The PlayMode asmdef is
    all-platforms with the `UNITY_INCLUDE_TESTS` define constraint: it
    compiles in the Editor (tests run normally) and is skipped entirely by
    player builds. Marking it Editor-only instead makes the EditMode runner
    sweep up the PlayMode assembly's `[Test]`s (the EditMode run balloons and
    fails), and no constraint at all breaks the player compile
    (`UnityTest` unresolved — UTF player support is off by default).

## Coverage notes

Every gameplay/runtime type is covered by the suites above. Deliberately not
unit-tested (they are editor tooling / scene-composition surfaces better validated by
play sessions and the editor itself):

- `RenderingScalabilitySetup` (Editor menu tooling).
- `DebugHud` (IMGUI-free but pure rendering), `PlayerHud` (procedural canvas
  rendering — its logic inputs (`CombatLog` kind filtering, `ActorBrainBase.Damaged`,
  regen values) are unit-tested; the pixels are validated by play sessions),
  `PlayerCoreUI`/`AimTarget` (scene-bound UI wiring).
- `PlayerSpawner` (composition root — covered indirectly by play sessions; its wiring
  rules live in `docs/spawnable_player_requirements.md`).

If you change any of those, at minimum smoke-test the editor flows they drive.
