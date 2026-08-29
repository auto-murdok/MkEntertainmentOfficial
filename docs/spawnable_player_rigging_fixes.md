# Spawnable Player — Rigging Investigation & Fixes (2026-08-28)

Record of why the rigged aim (Animation Rigging) did not work on the spawned
`SpawnableFemaleCharacter` while the arena-mounted `FemaleCharacter` worked,
and all fixes applied. Verified working end-to-end in Play Mode.

---

## TL;DR

The `SpawnableFemaleCharacter` prefab is a **byte-faithful copy** of
`FemaleCharacter.prefab` (verified by full serialized-property diff). It "did
not work" because of **scene references that Unity strips when a prefab asset
is saved** — the arena's in-scene character keeps them as scene overrides, the
prefab cannot. Additionally, `RigBuilder` **builds its animation graph during
`Instantiate`**, so any constraint data wired *after* instantiation is ignored
until the graph is rebuilt.

All scene references are now re-injected by `PlayerSpawner` (the composition
root), followed by a `RigBuilder.Clear() + Build()` rebuild.

---

## Root causes (in order of discovery)

### 1. Prefab assets cannot serialize scene references
Full serialized-property diff (arena instance vs `FemaleCharacter.prefab`)
showed the ONLY overrides were:

| Property | Arena instance | Prefab |
|---|---|---|
| `CharacterBrain._subject` | `InputHandler` (scene) | null |
| `CharacterLocomotion._aimTarget` | `AimTarget` (scene) | null |
| `MultiAimConstraint.m_SourceObjects` ×3 (BodyAim/Spine, HeadAim/Head, Aim/RightHand) | `AimTarget` (scene, w=1) | null |
| `Transform.m_LocalPosition` | scene placement | prefab default |

The three `MultiAimConstraint` source references are what drive the rigged
aim — with them null, `RigBuilder` builds a rig that aims at nothing.

### 2. RigBuilder bakes its graph during `Instantiate`
`RigBuilder.Awake()` builds the constraint jobs. `PlayerSpawner.Awake` wires
the constraint sources *after* `Instantiate(player)` returns — i.e. **after**
the graph was built with null sources. Symptom (classic, per Unity
Discussions): *"I am seeing my new source in editor, but it is completely
ignored by constraint."*

Fix: after wiring all constraints, rebuild the graph. The installed package
(Unity 6, no `RebuildRig()`) exposes:

```csharp
var rigBuilder = player.GetComponent<RigBuilder>();
rigBuilder.Clear();
rigBuilder.Build();
```

### 3. Mis-wired aim point (introduced during refactor, fixed)
`PlayerCoreUI._aimTarget` holds the **Crossair UI object** (it is the aim-UI
toggle), NOT the world aim point — same wiring in the arena. The spawner
initially fed this into `locomotion._aimTarget` and the rig constraints,
making the character aim at a canvas transform. The world-space aim point is
the **`AimTarget` child of `PlayerCoreComponents`** (has the `AimTarget`
raycast component).

### 4. `AimTarget._fallbackMouseWorldHook` stripped
`AimTarget.cs` casts a center-screen raycast; on a miss it falls back to
`_fallbackMouseWorldHook` (arena: `MainCamera/MousePosition` scene child;
prefab: null). Without it the aim target froze at origin whenever the ray
missed geometry (easy in the sparse SpawnablePlayer scene). Fixed by making
the field public and injecting it from the spawner.

---

## The composition root: `PlayerSpawner.Awake` (final wiring order)

```csharp
1. Instantiate(_inputHandlerPrefab)            // InputHandler + PlayerInput
2. Instantiate(_playerCorePrefab)              // PlayerUI, AimTarget cube,
                                               // PlayerAimCamera, PlayerFollowCamera,
                                               // PrefabManager, InteractableManager
3. Instantiate(_spawnablePlayer)               // SpawnableFemaleCharacter
4. brain._subject            = inputHandler    // input -> CharacterBrain observer
5. locomotion._aimTarget     = playerCore/"AimTarget" child (cube, NOT PlayerCoreUI._aimTarget)
6. AimTarget._fallbackMouseWorldHook = Camera.main/"MousePosition" child
7. foreach MultiAimConstraint: sourceObjects = [aimTarget, w=1]
8. rigBuilder.Clear(); rigBuilder.Build();     // rebuild graph AFTER sources exist
9. foreach CinemachineVirtualCamera: Follow = locomotion._cinemachineTarget
10. coreUI._subject = player's CharacterUIController  // UI observer
```

Scene contents required: **map + MainCamera (CinemachineBrain, with
`MousePosition` child) + baked NavMesh Surface + PlayerSpawner**. Everything
player-related is spawned at runtime.

---

## Supporting script changes

| File | Change | Why |
|---|---|---|
| `CharacterBrain.cs` | `_subject` public; `_playerInput` resolved lazily in `Start` (subject is wired after `Awake`); observer subscribe/unsubscribe via `_subscribed` flag (`Subject.AddObserver` does **not** dedupe) | spawn-time wiring tolerance |
| `PlayerCoreUI.cs` | `_subject` public; `Start`-based subscription with `_subscribed` flag | same |
| `AimTarget.cs` | `_fallbackMouseWorldHook` public | spawner injection |
| `PlayerSpawner.cs` | full composition-root rewrite (above) | single wiring point |

---

## Verification methodology (Play Mode, via unity-cli)

1. Constraint sources on the spawned instance → all `AimTarget (w1)`.
2. Force aim (`loco.setIsAiming(true)`) → `Rig.weight` 0 → 1.
3. Park the AimTarget cube far LEFT, read `spine.localEulerAngles`; park far
   RIGHT, read again → yaw follows the cube (**constraint driving**, not just
   the animator's aim layer, which masks a broken constraint).
4. Move camera to sky (raycast miss) → cube follows `MousePosition` fallback.
5. Console (`unity command console --level error`) → no new errors.

**Gotcha:** a spine rotation change alone does NOT prove the rig works — the
animator's aim animation layer also rotates the spine. Only the left/right
park test isolates the constraint.

---

## Differences that are OK (verified non-issues)

- `Rig.weight = 0` at idle — expected; rises only while aiming/reloading.
- `RigBuilder` layers, Rig layer mask, Animator settings, aim axes, constraint
  weights — identical between both prefabs and the arena instance.
- `PlayerCoreUI._aimTarget => Crossair` — intentional UI toggle wiring (arena
  parity), do not "fix" it to the cube.
