# State Machine Migration Plan

**Goal:** Make gameplay code and prefabs easily reusable by migrating ad-hoc
mode/phase logic into the existing generic `StateMachine<TStateKey, TContext>`
framework (`Core/CharacterStateMachine/`).

**Design principles**
- Extend, never rebuild, the generic framework.
- Reusability comes from (a) a **shared `ActorBlackboard`** so one state can serve
  any entity, and (b) **ScriptableObject config** so prefabs become data-driven
  variants (mirroring how `ZombieData` already works).
- Performance rules from AGENTS.md are preserved: no per-frame allocations in
  hot paths, pre-hashed animator params, `NonAlloc` queries.

---

## Phase 1 — Incremental (additive, Animator bridge untouched)

### 1.1 Shared actor abstraction
- `Core/CharacterStateMachine/ActorBlackboard.cs` — common fields:
  `animator`, `agent`, `isAlive`, `isRagdoll`, `onDeathCleanup`, `moveDestination`.
- `StateMachine` gains a `CheckGlobalTransition` hook so a single global guard
  (e.g. death) applies to *every* state without editing each state file.
- `CharacterStateContext` and `ZombieContext` now derive from `ActorBlackboard`.

### 1.2 Death / Ragdoll lifecycle (biggest reuse win)
- New shared `ActorDeadState<TKey,TContext>` in `Core/CharacterStateMachine/States/`.
- `Dead` added to `CharacterState` and `ZombieStates`; both FSMs register it and
  set `CheckGlobalTransition = ctx => ctx.isAlive ? current : Dead`.
- Brains' `TakeDamage` now sets `context.isAlive = false` (instead of directly
  enabling ragdoll). The entity-specific cleanup (`OnEnableRagdoll`) is moved into
  `context.onDeathCleanup`, invoked by the shared Dead state. Behavior preserved.

### 1.3 Zombie bite sub-states
- `ZombieBitingState` is restructured into discrete `BitePrepareState` /
  `BiteReleaseState` phases driven by the same `isPreparing` / animator signals,
  so the bite lifecycle is explicit and reusable. Animator bridge (`ZombieBiteBehaviour`)
  is left intact for Phase 1.

### 1.4 Unify click-to-move AI
- `Core/ICommandable.cs` interface (`SetMoveDestination` / `ClearMoveDestination`).
- New shared `ActorMoveToTargetState<TKey,TContext>` (reusable movement driver).
- `CommandedMove` added to `ZombieStates`; `ZombieBehavior` implements
  `ICommandable` and registers the move state. `AICharacterController` now routes
  through `ICommandable` when present, falling back to its own agent otherwise —
  one control path for any AI entity.

### 1.5 Player data-driven
- `PlayerData : ScriptableObject` (mirrors `ZombieData`) holding locomotion params.
- `CharacterLocomotion` reads it into the context and applies `agent.speed`,
  enabling `PlayerCore` prefab variants per data asset.

### 1.6 Packaging
- Shared states live in `Core/CharacterStateMachine/States/`; entity-specific in
  their own folders. (Optional `.asmdef` for the FSM core deferred.)

---

## Phase 2 — Full consolidation (collapse the Animator bridge)
- Move `ZombieBiteBehaviour` / `TakeBiteBehavior` (`StateMachineBehaviour`) logic
  into the C# FSM. Drive bite lifecycle from C# sub-FSM timers/callbacks; remove
  the dual `StopBitting()` / `RecoverControl()` sync.
- Update prefabs via `unity-cli` (never hand-edit YAML per AGENTS.md).

---

## Verification
- `unity status` → `unity command editor_play`; instantiate prefab variants.
- Validate transitions via `OnStateChanged` debug logging (`unity command log_editor`).
- Regression: player shoot/reload/bite, zombie chase/bite, death → ragdoll.
