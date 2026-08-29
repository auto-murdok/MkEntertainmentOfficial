# Zombie Bite — Investigation & Fixes (2026-08-29)

Record of why the spawned player never triggered a zombie bite, the full
investigation, and all fixes applied. Verified working end-to-end in Play Mode
(manual verification by user + live state probes).

---

## TL;DR

Two independent bugs stacked, both of which **silently disabled every bite
path**:

1. `PlayerCoreComponents.prefab` carried **two duplicate `InteractableManager`
   components** on the same child GameObject. The singleton guard in
   `InteractableManager.Awake` called `Destroy(gameObject)` on the duplicate —
   destroying the *entire* GameObject — so the manager deleted itself on spawn
   and `Instance` was always null.
2. `CharacterBrain` defined its own private `Start()`, which **hid**
   `ActorBrainBase.Start()` where InteractableManager registration happens.
   Unity only invokes the most-derived magic method, so the player was never
   registered with the manager even after fix #1.

With `Instance == null`, all three contact points quietly early-outed:
`ZombieHand.OnTriggerStay`, `ZombieBehavior.TryTriggerAttack`, and the player's
`AddInteractable` in `ActorBrainBase.Start`.

---

## Bite trigger chain (how it is supposed to work)

```
ZombieHand.OnTriggerStay ─┐
                          ├─► InteractableManager.Interact(playerId, zombieId)
ZombieBehavior            │
  .TryTriggerAttack ──────┘        (both sides registered in a
                                    Dictionary<int, IInteractable>)
                                          │
                    NotifyExternalInteraction → both directions:
                      • ZombieBrain.OnExternalInteraction
                          → SetIsBiting(true), Animator "Bite" trigger,
                            attacker-driven TakeDamage(biteDamage)
                      • CharacterBrain.OnExternalInteraction
                          → CharacterLocomotion.TriggerTakeBite
                          → CharacterTakeBiteState (pin to victimHook,
                            release on push-off, timer ends the bite)
```

Key files:

- `Assets/_Game/Scripts/Core/Interactables/InteractableManager.cs`
- `Assets/_Game/Scripts/Characters/ActorBrainBase.cs` (registration)
- `Assets/_Game/Scripts/Characters/AI/Entities/ZombieAI/ReactiveTriggers/ZombieHand.cs`
- `Assets/_Game/Scripts/Characters/AI/Entities/ZombieAI/ZombieBehavior.cs` (`TryTriggerAttack`)
- `Assets/_Game/Scripts/Characters/AI/Entities/ZombieAI/ZombieBrain.cs` (`OnExternalInteraction`)
- `Assets/_Game/Scripts/Characters/Player/CharacterBrain.cs` (`OnExternalInteraction`)
- `Assets/_Game/Scripts/Characters/Player/StateMachine/States/CharacterTakeBiteState.cs`

---

## Investigation (live probe results, Play Mode)

Runtime state was probed via `unity-cli eval_file` reflection scripts:

| Probe | Result | Meaning |
|---|---|---|
| `InteractableManager.Instance` | **null** | No manager alive in scene |
| `FindObjectsOfTypeAll<InteractableManager>` | only the 2 **prefab-asset** copies | instantiated one destroyed itself |
| `PlayerCoreComponents.prefab` YAML | 2 × `InteractableManager` (`MonoBehaviour` fileIDs `4067555581381029520`, `7183414644842363922`) on child `InteractableManager` | duplicate component is in the asset |
| Player layer / detection mask | `LocalPlayer` (6), `detectMask=64`, bit set | detection was **fine** |
| Physics matrix zombie↔LocalPlayer | not ignored | triggers were **fine** |
| Zombie hands | trigger colliders, enabled while chasing | **fine** |
| Registered interactables (before fixes) | zombies only | player never registered |

### Failure cascade

1. First `InteractableManager.Awake` sets `Instance = this`.
2. The duplicate's `Awake` hits the guard → `Destroy(gameObject)` → the whole
   `InteractableManager` child dies; both `OnDestroy`s null `Instance`.
3. `ActorBrainBase.Start`: `if (InteractableManager.Instance != null)` → false
   → registration **silently skipped** (no log).
4. `ZombieHand.OnTriggerStay` and `ZombieBehavior.TryTriggerAttack` both guard
   on `InteractableManager.Instance != null` → never call `Interact`.

Rule of thumb confirmed here: **a singleton's duplicate guard must never
destroy its GameObject** — siblings on the same object die with it, and the
`OnDestroy` of the original instance then nulls the singleton.

### Second bug found by fix #1 (magic-method hiding)

After the prefab fix the manager survived (3 zombies registered) but the
**player was still missing** from the dictionary. `CharacterBrain.Start()`
(private, line 39) hides `ActorBrainBase.Start()` (private, registration +
ragdoll-disable + `OnActorStart` layer setup). Unity calls only the
most-derived `Start` in the hierarchy, so base registration never ran for the
player. Zombies were unaffected because `ZombieBrain` defines no `Start`.

---

## Fixes applied

### 1. Removed the duplicate component (data fix)
Via live-Editor `eval_file` on the prefab asset:

```csharp
var comps = prefabRoot.transform.Find("InteractableManager")
                              .GetComponents<InteractableManager>();
for (int i = 1; i < comps.Length; i++)
    Object.DestroyImmediate(comps[i], true);   // keep exactly one
AssetDatabase.SaveAssets();
```

Result: 2 → 1 components on the prefab child.

### 2. Hardened the singleton guard (code fix)
`InteractableManager.Awake` — destroy the duplicate **component**, never the
GameObject:

```csharp
if (Instance != null && Instance != this)
{
    Destroy(this);   // was: Destroy(gameObject)
    return;
}
```

### 3. Made `ActorBrainBase.Start` virtual + override (code fix)

```csharp
// ActorBrainBase
protected virtual void Start() { /* ragdoll off, AddInteractable, OnActorStart */ }

// CharacterBrain
protected override void Start()
{
    base.Start();          // registration now actually runs for the player
    ...existing subject wiring + Subscribe...
}
```

### 4. Loud failure instead of silent skip (code fix)
`ActorBrainBase.Start` now logs an error when registration is impossible:

```csharp
else
{
    Debug.LogError($"[{name}] No InteractableManager in the scene — actor cannot be bitten/targeted. ...");
}
```

This exact bug was invisible for weeks because every guard was a silent
early-out.

---

## Verification

- Recompilation clean; no new console errors.
- Play Mode probes: `InteractableManager.Instance != null: True`,
  `player registered: True`, zombies spawn and register.
- Forced `Interact(player.id, zombie.id)` with a warped-in zombie → zombie
  enters `Biting`, player enters `TakingBite` (`isBeingAttacked`).
- Final gameplay verification (zombie approaches, grabs, bites, push-off,
  re-attack loop) done manually by the user.

## Follow-up notes (not bugs)

- Zombies idle until the player is within `ZombieData.detectionMaxDistance`
  (5 m) — by design (`detectionLayerMask` correctly contains `LocalPlayer`).
- `StateMachine<TStateKey,TContext>.Start` is a plain private magic method;
  no derived class hides it today (`ZombieBehavior`, `CharacterLocomotion`,
  `Handgun` define none), but the same hiding trap applies — avoid defining
  `Start` in subclasses without making the base one virtual.
