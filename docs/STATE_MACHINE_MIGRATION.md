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
  `context.onDeath`, invoked by the shared Dead state. Behavior preserved.

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
- `CharacterLocomotion` reads it into the context. Note: `agent.speed` is intentionally
  NOT applied — player movement is root-motion driven (see Audit Log below).

### 1.6 Packaging
- Shared states live in `Core/CharacterStateMachine/States/`; entity-specific in
  their own folders. (Optional `.asmdef` for the FSM core deferred.)

---

## Phase 2 — Full consolidation (collapse the Animator bridge) — DONE
- `ZombieBiteBehaviour` / `TakeBiteBehavior` (`StateMachineBehaviour`) logic was moved
  into the C# FSM. The bite lifecycle is now owned entirely by `ZombieBitingState`
  (C# sub-FSM timer) and `CharacterTakeBiteState` (C# timer); the deprecated behaviours
  were deleted via the `Cleanup/Remove Bite Bridges` editor menu (`BiteBridgeCleanup`).
- With the bridge gone, the dual `StopBitting()` / `RecoverControl()` sync is no longer
  needed; `RecoverControl` was removed (the bite self-terminates via its timer).

---

## Verification
- `unity status` → `unity command editor_play`; instantiate prefab variants.
- Validate transitions via `OnStateChanged` debug logging (`unity command log_editor`).
- Regression: player shoot/reload/bite, zombie chase/bite, death → ragdoll.

---

## Player Character Audit & Simplification Log

Purpose: record *why* each simplification was made so future audits don't re-flag these
as bugs or re-derive the rationale. Research basis: context7 (`/websites/unity3d_manual`)
+ firecrawl (Unity discussions / gamedev.tv / Opsive) — per the AGENTS.md Research Standard.

### Current architecture (as built)
- `StateMachine<TKey,TContext>` (Core) — generic FSM; one deferred transition per frame
  (`ChangeState` is buffered, applied at end of `Update`).
- `ActorBlackboard` (Core) — shared context; `onDeath` callback drives ragdoll/teardown.
- `ActorDeadState<TKey,TContext>` (Core) — reusable terminal state for any actor.
- `ActorBrainBase` (Characters) — shared brain lifecycle for Player + Zombie:
  `IInteractable` registration, ragdoll enable/disable, `onDeath` hook, `ApplyDamage`,
  and `IDamageable.TakeDamage(float)`.
- `CharacterStateResolver` (Player) — single source of truth for flag-derived player states.
- `IDamageable.TakeDamage(float amount)` — attacker-driven damage (gold standard).

### Decision 1 — Centralize player transitions (CharacterStateResolver)
- Problem: `Idle`/`Walking`/`Sprinting`/`Aiming`/`Reloading` each repeated the same priority
  chain, and it had already drifted (`Sprinting` had an extra `!isRunning` branch the others
  lacked).
- Fix: `CharacterStateResolver.Resolve(context)` returns the target state from the context
  flags; each state delegates `CheckTransitions` to it. `TakeBite`/`Dead` keep their own
  transitions (not pure flag derivations).
- Why: removes ~120 lines of duplicated, inconsistent logic; one place to change priority.

### Decision 2 — ActorBrainBase consolidation (Player + Zombie)
- Problem: `CharacterBrain` and `ZombieBrain` duplicated `InteractableManager` registration,
  the `onDeath` hook, ragdoll teardown, and HP-reduction logic.
- Fix: extract `ActorBrainBase`; both derive from it; teardown overrides call
  `base.OnRagdollEnabled()` then destroy entity-specific components.
- Why: DRY; both stay architecturally identical to the shared FSM "gold standard".

### Decision 3 — Attacker-driven damage flow (IDamageable.TakeDamage(float))
- Problem (latent bug): `TakeDamage()` was parameterless; the only caller was
  `BulletProjectile`, and **biting dealt no damage at all** — it only played the
  animation/lock.
- Fix: `IDamageable.TakeDamage(float amount)`; `ActorBrainBase` implements it via
  `ApplyDamage(amount)`; the zombie applies `damageable.TakeDamage(biteDamage)` to its victim;
  `BulletProjectile` carries a `_damage` value. `ISurvivor.TakeDamage(float)` aligned.
- Why: gold-standard pattern — damage is always attacker-supplied, never hardcoded in the
  victim.

### Decision 4 — Locomotion glue cleanup (root motion is intentional)
- Finding: `CharacterLocomotion` set `agent.speed = moveSpeed`, but movement is root-motion
  driven, so `speed` was never used. Also `RecoverControl`/`HandleRecoverControl` had no caller
  after the bite bridge was removed.
- Fix: removed the dead `agent.speed` assignment and the dead `RecoverControl` path (the bite
  self-terminates via its C# timer). Documented that the player's `NavMeshAgent` exists only
  for bite-pin radius management (`agent.radius` / `ResetPath`), not locomotion.
- Why (research): Unity docs + Opsive confirm root motion is the gold standard for a
  directly-controlled, animated character; `NavMeshAgent` is for AI/pathfinding. So the player
  keeps root motion and keeps the agent only for the bite pin.

### Intentional choices (do NOT "fix" in future audits)
- Player movement = root motion (not CharacterController / agent-driven). Correct by design.
- Player `NavMeshAgent` is present but used only for bite radius pinning.
- Bite lifecycle is owned by the C# FSM (no Animator `StateMachineBehaviour` bridge).
- `ActorBrainBase.ApplyDamage` is the single HP/death path; `isAlive = false` triggers the
  shared `ActorDeadState` → `onDeath` → ragdoll.
- `CharacterStateResolver` is the only place player transition priority lives.
