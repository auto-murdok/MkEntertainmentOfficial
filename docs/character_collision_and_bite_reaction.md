# Character collision and bite reaction

Record of why the player could walk straight through a zombie, the collision
system that fixed it, the grapple problem that fix created (and its protocol),
and the victim-reaction timing that makes the paired bite read as one
animation. Verified in the real arena scene with the real prefabs (host
staging captures + full test suite), October 2026.

Companion record: `docs/zombie_bite_interaction_fixes.md` covers the earlier
bite-trigger investigation. The bite timeline itself (contact / release
anchors) is anchored to the paired clip in PR #75; this document covers the
work stacked on top of it.

---

## TL;DR

1. The layer collision matrix was never the problem — it is all-ones. Three
   missing pieces stacked: the player had **no motor and no body collider**
   (root motion wrote her transform unswept), the zombie had **no solid body
   collider** (only a 0.1 m hand trigger + ragdoll bones), and NavMeshAgent
   avoidance is steering, not physics.
2. Fix, following the standard Unity character-collision recipe: player gets
   a **CharacterController** fed from `OnAnimatorMove`; the zombie gets a
   **solid capsule + kinematic Rigidbody**; a soft **separation steering**
   pass keeps a chasing zombie out of the victim's capsule (her controller
   can only block *her* movement, not his).
3. The grapple then needed its own protocol: the paired clips pin the roots
   at **0.44 m**, inside the 0.60 m capsule sum. For the bite's duration the
   pair stops colliding — `Physics.IgnoreCollision` between her controller
   and **all** of the zombie's colliders (root capsule *and* ragdoll bones) —
   restored on exit once the roots are **0.75 m** apart, with a deferred
   restore (3 s cap) for the against-a-wall case, so collision never pops
   them apart.
4. The victim no longer stands idle "waiting to be bitten": her clip starts
   `CharacterTakeBiteState.ClipLeadSeconds` (**0.85 s, calculated** — see §4)
   ahead of the contact anchor, so her reaction plays through the grab and
   her final shove lands exactly on his recoil peak. The anchors themselves
   live in one shared class, `BiteTimeline`, read by both sides.

---

## 1. Diagnosis: three stacked causes

Observed: during a movement-bite staging capture, the player walked *through*
the zombie's body — minimum root separation reached ~0.19 m mid-stride with
no deflection.

Checked first, per the report: `ProjectSettings/DynamicsManager.asset`,
`m_LayerCollisionMatrix` — **all `f`s, every layer collides with every
layer** (LocalPlayer 6 × Zombie 7 included). The matrix was innocent.

The actual causes, all verified by prefab inspection:

| # | Cause | Evidence |
|---|-------|----------|
| 1 | Player root had no CharacterController, no Rigidbody, no body collider. `Animator.applyRootMotion = 1` wrote the transform directly — unswept, unstoppable. | `FemaleCharacter.prefab` pre-fix; her NavMeshAgent (r 0.3) existed but was never her motor. |
| 2 | Zombie root had no solid collider either: one 0.1 m trigger sphere (the hand) plus ragdoll bone colliders, which are kinematic while alive. | `Zombie.prefab` pre-fix. His hand trigger detects *her bones*; nothing stops a body. |
| 3 | NavMesh avoidance is RVO steering of an agent's own velocity. It never blocks a non-agent, and does nothing for a stopped/biting zombie. | His agent runs high-quality avoidance; she is not an agent-driven obstacle to it in the physics sense. |

## 2. The collision fix

Gold-standard basis (researched for this change): root-motion characters are
moved by feeding the Animator's delta through a CharacterController in
`OnAnimatorMove`; enemies that must be solid but animation/agent-driven carry
a capsule on a **kinematic** Rigidbody; overlaps between specific pairs are
managed with `Physics.IgnoreCollision`, never by fighting the solver.

- **`FemaleCharacter.prefab`** — added `CharacterController`: radius 0.3,
  height 1.8, center y 0.9, skinWidth 0.03, stepOffset 0.3, slopeLimit 45.
- **`Zombie.prefab`** — added root `CapsuleCollider` (r 0.3, h 1.8, center
  y 0.9, non-trigger) + `Rigidbody` (kinematic, no gravity).
- **`CharacterLocomotion.OnAnimatorMove`** — root motion now routes through
  `_characterController.Move(animator.deltaPosition)`; rotation keeps the
  built-in path (`transform.rotation *= animator.deltaRotation`). Falls back
  to a direct transform write when no controller exists. The controller is
  published on the state context (`CharacterStateContext.characterController`).
- **`ZombieBehavior.EnforceBodySeparation`** (on `OnCommonUpdate`) — a zombie
  not in Dead/Biting with a live target is pushed out of the **0.6 m**
  combined radii along the ground plane, via `agent.Move` when the agent is
  enabled and on a NavMesh, else a transform nudge. This covers the reverse
  direction her controller cannot: *him* walking into *her*.
- **The bite pin moves through the controller** (`CharacterTakeBiteState.
  SetPinnedPosition`): `controller.Move(target − position)`. Hard-won lesson:
  this project runs with `Physics.autoSyncTransforms = 0`, so a direct
  transform write does **not** rebase a CharacterController — the next
  root-motion step snaps the character back to the pre-write position (this
  silently broke the first post-fix capture: a staging teleport reverted).
  **Any future player teleport/respawn must disable the controller, write,
  re-enable.**

Verified: brain-dead zombie parked in her lane — she is blocked at
**0.599 m** root separation, slides around him, never enters him. Movement
bite unchanged otherwise (trigger self-fires, phases on the beat).

## 3. The grapple protocol

The collision fix exposed the opposite problem inside the bite. The paired
clips pin the roots at **0.44 m** (`VictimHook` local z 0.79 in
`Zombie.prefab`, pulled in by `PlayerBitePullInDistance = 0.35`) — inside the
0.60 m capsule sum. First attempt ignored only his root capsule: her pin
`Move()` stopped at **0.602 m** — held off by his solid **ragdoll bone
colliders** — and the embrace read visibly loose.

Protocol (in `ZombieBitingState`, restore assisted by `ZombieBehavior`):

- **At bite start:** `Physics.IgnoreCollision` between the victim's
  CharacterController and **every** collider on the zombie
  (`GetComponentsInChildren<Collider>(true)` — root capsule, bones,
  triggers). Scoped to exactly this pair; every other character keeps
  colliding with both.
- **At bite end:** if root distance ≥ **0.75 m** (`GrappleClearDistance` —
  past the reach of his bones, not just the body radii), restore immediately.
  Otherwise defer: `ZombieBehavior.RestoreCollisionWhenSeparated` restores
  when they separate, capped at **3 s**. A pending restore is cancelled if a
  new bite starts first.
- **Why not simply re-enable:** `CharacterController.enableOverlapRecovery`
  only resolves *static* overlaps; restoring collision while the pair still
  overlaps asks the solver to eject one of them — a visible pop. Restore
  only once separated.
- Plain test doubles are not `Component`s and skip the protocol entirely
  (the PlayMode bite tests drive the FSM without a scene).

Verified: movement bite now reaches the authored 0.44 m socket — closest
root separation **0.19–0.23 m** mid-grind as the two clips' own root motions
converge, exactly the pre-collision choreography; damage still lands on the
contact beat (30 HP, phases +31/+91 frames at 30 fps from grab start).

## 4. Victim reaction timing (the "waiting to be bitten" fix)

Her clip's frame 0 is already mid-impact, so (pre-fix) it started exactly at
the contact flip, 0.97 s into the grab — leaving her standing pinned and
idle for almost a second while he lunged. Starting it at the grab is equally
wrong the other way (impact pose ~1 s before his head arrives).

The rule now: her clip starts when the grab has been underway for
`BiteTimeline.ContactTime − ClipLeadSeconds`, with the contact flip kept as
a fallback for late entries. `ClipLeadSeconds` is a public static tuning
field; its shipped value is **calculated, not eyeballed**:

| Measurement (baked clips, full-res slope analysis) | Value |
|---|---|
| His head-snap contact anchor | 0.97 s |
| His hands first touch her (bone probe) | ~0.66 s |
| His recoil slope peak (steepest head-back) | 2.900 s |
| Her final shove slope peak, clip-local | 2.78 s (chest 2.797 / hands 2.763) |
| At a 1.00 s lead (start −0.03 s) her shove lands at | 2.75 s → **150 ms early** |
| **Correction: delay her start by 150 ms → lead** | **0.85 s** |

At 0.85 s: she starts at 0.12 s into the grab (reaction underway long before
his hands land at 0.66 s), her first drive lands at 1.95 s mid-grind — a
failed attempt while he is still latched, which is the clip's own two-effort
story — her final shove lands **on** his 2.90 s recoil peak, and at the
3.0 s release her clip is at local 2.88 s, just past the shove: she is freed
*by* the push. Comparison captures at 0.50 / 0.56 / 1.00 s leads were shot
before settling this; 1.00 read best to the eye and the calculation explains
why (it was only 150 ms off ideal).

The anchors were single-sourced for this: `BiteTimeline`
(`Scripts/Characters/AI/Entities/ZombieAI/BiteTimeline.cs`) holds
`ContactTime = 0.97` and `ReleaseTime = 3.0`, read by both
`ZombieBitingState` and `CharacterTakeBiteState`. `PlayerData.
takeBiteDuration` must equal `ReleaseTime` (documented on both sides).

## 5. Tuning knobs

| Knob | Where | Value | Meaning |
|---|---|---|---|
| `ClipLeadSeconds` | `CharacterTakeBiteState` (static field) | 0.85 s | How far ahead of the contact anchor her clip starts. |
| `GrappleClearDistance` | `ZombieBitingState` | 0.75 m | Root distance at which pair collision may be restored. |
| `BodySeparationRadius` | `ZombieBehavior` | 0.6 m | Combined body radii enforced by separation steering. |
| `PlayerBitePullInDistance` | `CharacterTakeBiteState` | 0.35 m | Socket → body gap closure for the pin (0.44 m pin distance). Raise for a tighter grapple. |
| Player controller | `FemaleCharacter.prefab` | r 0.3 / h 1.8 | CharacterController dimensions. |
| Zombie body | `Zombie.prefab` | r 0.3 / h 1.8 | Capsule on kinematic Rigidbody. |

## 6. Verification

- Full suite on the final tree (batchmode): **EditMode 164/164,
  PlayMode 137/137**. No test changes in this PR — the one directed
  test change for the clip-anchored timeline lives in PR #75.
- In-engine captures in the real arena staging (temporary drivers, removed
  after): passive block-and-slide (min separation 0.599 m), movement bite
  with tight grapple (0.19–0.23 m convergence), movement bite with final
  0.85 s lead (phases +31/+91 frames, damage on the beat).

## 7. Known limitations / follow-ups

- **Netcode authority:** remote peers also run `OnAnimatorMove`; the
  authoritative NetworkTransform overrides their local moves — the same
  pattern the old transform-writing root motion had. Authority-gating the
  motor (owner runs the controller, remotes purely visual) is a follow-up,
  not part of this change.
- **Teleports/respawns** must disable the CharacterController around direct
  transform writes (see §2) — no such system exists yet; the rule is
  recorded here for whoever adds one.
- **Foot locking** (her feet slide during clips, most visible while pinned):
  researched separately; the standard is baked per-clip planted curves
  (`LeftFootPlanted` / `RightFootPlanted`) driving Animation Rigging
  Two-Bone-IK locks, with feet pinned to their grab-start positions for the
  bite's duration. Planned as its own change; not in this PR.
