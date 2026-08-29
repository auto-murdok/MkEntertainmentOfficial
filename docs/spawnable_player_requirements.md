# Spawnable Player — Scene Requirements

The player is **never** placed in a scene. `PlayerSpawner` (composition root,
see `Assets/_Game/Scripts/Composition/PlayerSpawner.cs`) spawns and
wires every player-related object at runtime:

- `Assets/_Game/Prefabs/Characters/Survivor/SpawnableFemaleCharacter.prefab`
- `Assets/_Game/Prefabs/InputHandler.prefab`
- `Assets/_Game/Prefabs/UI/PlayerCoreComponents.prefab` (UI, AimTarget cube, both Cinemachine cameras)

A scene must therefore contain **only**: map, MainCamera (CinemachineBrain +
`MousePosition` child), a baked NavMesh, and a `PlayerSpawner`.

## Hard requirement: baked NavMesh Surface terrain

`CharacterLocomotion` attaches a `NavMeshAgent` and the TakeBite state drives
the agent (`agent.radius` / `ResetPath`). With no valid NavMesh, the Editor
logs `Failed to create agent because there is no valid NavMesh`. Every scene
that spawns a player must have a **baked NavMesh Surface** on its terrain
(AI Navigation package).

## Why the spawner must wire everything (prefab scene-ref stripping)

Unity strips **prefab → scene object** references when a prefab asset is
saved. The SpawnableFemaleCharacter / PlayerCoreComponents prefabs therefore
ship with these refs null, and `PlayerSpawner.Awake` re-injects them on the
instances:

| Reference | Injected into |
|---|---|
| `InputHandler` subject | `CharacterBrain._subject` |
| `AimTarget` cube | `CharacterLocomotion._aimTarget` + all 3 `MultiAimConstraint.sourceObjects` |
| `Camera.main/MousePosition` | `AimTarget._fallbackMouseWorldHook` |
| Player's camera hook | both Cinemachine cameras' `Follow` |
| Player's `CharacterUIController` | `PlayerCoreUI._subject` |

**Critical:** `RigBuilder` builds its animation graph during `Instantiate` —
after changing constraint sources at runtime you must call
`rigBuilder.Clear(); rigBuilder.Build();` or the constraints ignore them.
Full investigation record: `docs/spawnable_player_rigging_fixes.md`.

## Checklist before a spawnable player is "live"

- [ ] `PlayerSpawner` in scene with the three prefab refs assigned
- [ ] Terrain/ground has a **baked NavMesh Surface**
- [ ] MainCamera present with `CinemachineBrain` and a `MousePosition` child
- [ ] No player-related objects placed in the scene (they would duplicate the spawned ones)

## How this was discovered

Spawning into an early `SpawnablePlayer` logged
`Failed to create agent because there is no valid NavMesh` and
`AssertionException: Ensure a subject is properly hooked up` — leading to the
dependency audit, the composition-root refactor, and the rigging fixes.
