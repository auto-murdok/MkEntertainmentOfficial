# Zombie Hand Attack — Multi-Attacker Design & Learnings (2026-08-29)

Implements the standing **right-hand swing** (`RHandAttack` → `zombie_right_hand_ttack`)
for a zombie attacking a player who is **already pinned in another zombie's bite grab**.
Verified end-to-end in Play Mode (forced live scenario + full test suite).

---

## TL;DR

- A bite is an **exclusive grab attack**; a swipe is not. Attack selection is
  driven by the **victim's availability**, queried through a new tiny interface
  `IBiteTarget` — the AI never references player types.
- The subtle bug found live: the victim marks itself `isBeingAttacked`
  **synchronously inside the bite interaction**, *before* the attacking
  zombie's own side of the same interaction is notified. Without an identity
  check, the bite initiator sees "victim pinned by someone" and diverts itself
  into a hand swing mid-bite. Fix: `IBiteTarget.currentBiter` lets a zombie
  recognize a pin held by **itself** as its own bite in progress.
- `ZombieBehavior.TryTriggerAttack` was still calling `InteractableManager.Instance`
  — a type that **no longer exists** in the project (removed by the SO
  architecture migration; see `AGENTS.md` "ScriptableObject architecture" —
  note `zombie_bite_interaction_fixes.md` predates it and still describes the
  deleted singleton). The bite path now goes through
  `brain.registry.Interact(...)` like `ZombieHand`.
- The `RHandAttack` animator transition shipped with `hasExitTime = 1`
  (exit time 0.94) — on a looping blend tree this delays the swing by up to a
  full locomotion cycle. Fixed to `hasExitTime = 0` via the live-Editor
  AnimatorController API (never hand-edit the controller YAML).

---

## Attack-selection rule (the one decision point)

`ZombieBehavior.CanVictimBeBitten(IInteractable victim, IInteractable attacker)`:

| Victim state | Result |
|---|---|
| `null` or no `IBiteTarget` | biteable (bite) |
| `canBeBitten == true` | biteable (bite) |
| pinned, `currentBiter == attacker` | biteable — **this is our own bite** |
| pinned by anyone else / unknown | **hand attack** |

Call sites:
- `ZombieBehavior.TryTriggerAttack` (Idle/Chasing trigger path) — passes `brain`.
- `ZombieBrain.OnExternalInteraction` (hand-trigger `Interact` path) — passes `this`.

If the victim cannot be bitten, `ZombieBehavior.StartHandAttack(victim)` captures
the victim into `_context.interactable` (survivor scans may clear `_context.target`
mid-swing) and raises `isHandAttacking`; Idle/Chasing `CheckTransitions` route it
into `ZombieStates.HandAttacking` on the same frame.

## Why the victim reports `canBeBitten == false` mid-interaction

The bite interaction order (`InteractableRegistry.NotifyExternalInteraction`) is:

```
registry.Interact(victim.id, zombie.id)
  1. victim.OnExternalInteraction(zombie)
       → CharacterLocomotion.TriggerTakeBite(zombie)
       → _context.attacker = zombie; _context.isBeingAttacked = true   ← synchronous
  2. zombie.OnExternalInteraction(victim)
       → CanVictimBeBitten(victim, zombie)  ← victim now reports pinned!
```

Without step 2 knowing who holds the pin, the bite initiator would always
misread its own bite. `IBiteTarget.currentBiter` (backed by
`CharacterStateContext.attacker`) resolves it. This also makes same-frame
double-triggers deterministic: whichever `Interact` lands first pins the
victim to itself; the other zombie's notification then redirects to a swing.

## State design (`ZombieHandAttackState`)

Mirrors `ZombieBitingState` conventions, deliberately simpler:

- **C# FSM timer owns the lifecycle** (default 1.2 s, `ZombieData.handAttackDuration`)
  — no reliance on animator exit events (same pattern as the bite).
- **Hit lands at 40%** of the swing (`HitFraction`), scored **exactly once**
  per swing (`_hasHit` burns the hit even if the victim left range or died —
  same one-damage-per-action guard as the pooled bullets).
- No pinning, no `isPreparing`, no agent-radius shrink; `agent.ResetPath()`
  **only when `agent.isOnNavMesh`** (unguarded `ResetPath` logs an error off-mesh
  — this failed the PlayMode tests immediately).
- Exit: arms `attackCooldownTimer = 1.5f`, clears `interactable` and
  `isHandAttacking`, transitions to Idle.
- Cooldown semantics (unchanged bite behavior, now shared): the timer ticks in
  `ZombieIdleState.UpdateState` only; re-attack happens from Idle after the
  cooldown. Chasing re-triggers only on fresh engagements (distance gate), same
  as bites always did. Guards `!isHandAttacking` were added to the Idle/Chasing
  attack gates and `ZombieHand.OnTriggerStay` redirects (not re-bites) while
  swinging via the `isBiting || isHandAttacking` guard in `OnExternalInteraction`.

## Tunables (per zombie type, `ZombieData`)

| Field | Default | Meaning |
|---|---|---|
| `handAttackDamage` | 15 | swing damage (vs bite 30) |
| `handAttackRange` | 1.6 | reach at the hit frame (> biteRange 1.2) |
| `handAttackDuration` | 1.2 | swing length in seconds |

State-side TUNING constants: `HitFraction = 0.4`, `HandAttackCooldown = 1.5`.

## Test learnings (new, beyond docs/testing.md)

1. **Avoid auto-detection races in PlayMode AI tests.** A victim left on the
   `LocalPlayer` layer inside the zombie's cone gets auto-targeted by the
   vision scan (`SearchForSurvivors` runs every 0.15 s and *overwrites*
   `_context.target`), so the FSM can attack before the test drives it. Keep
   the fake victim on `Default` and set `_context.target` manually.
2. **`SearchForSurvivors` clears the target every scan.** Anything that must
   survive scans has to be captured elsewhere — hence `StartHandAttack`
   storing the victim in `_context.interactable`, and the state resolving
   `interactable ?? target as IInteractable` at hit time.
3. **Inject `InteractableRegistry` via `UnityEditor.SerializedObject`** on the
   private base-class field `_registry` (`ApplyModifiedPropertiesWithoutUndo`
   before the first frame, so `ActorBrainBase.Start` registers cleanly).
   Fully qualify `UnityEditor.SerializedObject` in PlayMode tests (repo convention).
4. **Controller-less `Animator` is safe for parameter *writes*** (`SetTrigger`
   with an int hash is silent) — mini zombie rigs in tests need only
   `Animator` + `NavMeshAgent` + `ZombieBehavior` + `ZombieBrain`.
5. **Unguarded `NavMeshAgent.ResetPath()` errors off-mesh** ("can only be called
   on an active agent that has been placed on a NavMesh") and fails PlayMode
   tests as an unhandled log. Guard with `isOnNavMesh` (existing
   `ZombieChasingState` convention).
6. **Test-runner wedge:** launching an EditMode run while a PlayMode run's
   scene teardown is still in flight wedges the runner forever
   (`status: "running"`, console: *"Test tree is not available for
   PostbuildCleanupTask"*). Recovery: `unity command editor_stop`, clear
   console, re-run. Also: regex-checking `test_status` JSON through
   PowerShell `-match` false-fails on escaped quotes — parse with
   `ConvertFrom-Json` or check `recompile_status` output verbatim.
7. **Concurrent agents share one Editor:** expect foreign files to appear
   mid-session (here `MainMenuController.cs` broke compilation twice); block
   only on the user's word, and re-verify `recompile_status` before trusting
   old failure output.

## Key files

- `Assets/_Game/Scripts/Characters/Player/Interfaces/IBiteTarget.cs` (new)
- `Assets/_Game/Scripts/Characters/AI/Entities/ZombieAI/States/ZombieHandAttackState.cs` (new)
- `Assets/_Game/Scripts/Characters/AI/Entities/ZombieAI/ZombieBehavior.cs` (selection + `StartHandAttack`)
- `Assets/_Game/Scripts/Characters/AI/Entities/ZombieAI/ZombieBrain.cs` (redirect + duplicate guard)
- `Assets/_Game/Scripts/Characters/AI/Entities/ZombieAI/States/ZombieIdleState.cs` / `ZombieChasingState.cs` (routing + gates)
- `Assets/_Game/Scripts/Characters/Player/CharacterBrain.cs` (`canBeBitten`, `currentBiter`)
- `Assets/_Game/Scripts/Characters/Player/StateMachine/CharacterLocomotion.cs` (`currentAttacker`)
- `Assets/_Game/Animations/Characters/Zombie/AC_Zombie.controller` (`RHandAttack` trigger, transition fixed)
- Tests: `Tests/EditMode/Characters/ZombieAttackSelectionTests.cs`,
  `Tests/PlayMode/ZombieHandAttackPlayTests.cs` (+ doubles in both `TestDoubles` files)

## Verification

- Suite: **82 EditMode + 88 PlayMode, all green** (9 new tests, including the
  own-bite race regression `PinHeldByThisZombie_OwnBiteContinues`).
- Live Play Mode (forced scenario, two zombies + player):
  zombie A `isBiting=True` (player pinned, 30 dmg), zombie B
  `isHandAttacking=True` (swing, 15 dmg per swing), push-off released the
  player, bite→cooldown→re-engage loop and re-swings cycled with no errors.
