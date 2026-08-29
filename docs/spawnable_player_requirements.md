# Spawnable Player — Scene Requirements

When instantiating the **FemaleCharacter** prefab
(`Assets/_Game/Prefabs/Characters/Survivor/FemaleCharacter.prefab`) into a
standalone scene (e.g. `SpawnablePlayer`), the prefab alone is **not** enough to
make the character functional. It carries serialized/gameplay dependencies on
other scene objects, and it needs a navigation surface.

## Hard requirement: baked NavMesh Surface terrain

`CharacterLocomotion` attaches a `NavMeshAgent` and the TakeBite state drives the
agent (`agent.radius` / `ResetPath`). With no valid NavMesh, the Editor logs:

```
Failed to create agent because there is no valid NavMesh
```

**Therefore every scene that spawns a player MUST contain a baked NavMesh.**
The simplest way is a **NavMesh Surface** on the terrain/ground (Unity AI
Navigation package — `NavMeshSurface` component) that is baked so the agent can
register. Without it, the character's NavMeshAgent fails at runtime.

## Other required scene objects (verified dependencies)

| GameObject | Why needed |
|---|---|
| **InputHandler** | `CharacterBrain._subject` (asserted in `Awake`, `CharacterBrain.cs:30`). Without it the character gets no input and the assertion throws. |
| **AimTarget** | `CharacterLocomotion._aimTarget`. Used for aiming/shooting direction. |
| **PrefabManager** | Runtime singleton. `CharacterLocomotion.EquipWeapon` spawns the weapon via `PrefabManager.Instance` at `Awake`. |
| **PlayerFollowCamera / PlayerAimCamera** | Cinemachine virtual cameras whose `Follow = 3rdPersonCameraHook` (child of the character). Needed for camera control. |
| **NavMesh Surface (terrain)** | Baked NavMesh so the `NavMeshAgent` is valid. |

## How it was discovered

Instantiating the prefab into `SpawnablePlayer` (active scene at the time) logged
both `Failed to create agent because there is no valid NavMesh` and
`AssertionException: Ensure a subject is properly hooked up` at
`CharacterBrain.Awake` — confirming the missing NavMesh and the missing
`InputHandler` subject.

## Checklist before a spawnable player is "live"

- [ ] FemaleCharacter prefab instantiated into the scene
- [ ] **Terrain/ground has a baked NavMesh Surface**
- [ ] InputHandler present and wired to `CharacterBrain._subject`
- [ ] AimTarget present
- [ ] PrefabManager present (so the weapon equips)
- [ ] PlayerFollowCamera & PlayerAimCamera present and following `3rdPersonCameraHook`
