# Grounded motor and Corpse layer

Records the fix for the post-#76 levitation bug and the movement-math
rules that keep it fixed. Research of record:
`unity-charactercontroller-levitation` (root cause + grounding model),
`unity-movement-motors` (motor comparison), and
`grounding-fix-integration-conflicts-performance` (integration audit
against this codebase, 2026-10-26).

## The bug

After #76 the player's motor was `controller.Move(animator.deltaPosition)`
and nothing else: no gravity, no vertical velocity, no ground check
anywhere in the player code. `CharacterController` step-up is independent
of `slopeLimit`, so walking into a dead zombie's ragdoll pile (bones on
the Default layer, collision matrix all-on, ~0.1–0.3 m high) stepped her
up onto it — and with no channel owning Y, nothing ever brought her back
down. She kept the pile's height for the rest of the run. Before #76 the
clips' authored root Y reasserted her height every frame; the controller
removed that safety net. The same hole affected ledges and descending
stairs.

Two corpse-side facts compounded it: on death nothing disabled the
zombie's root capsule, and the ragdoll enable flipped its rigidbody
dynamic — the corpse kept a live 1.8 m capsule body at the death spot.

## The grounded motor (CharacterLocomotion.OnAnimatorMove)

The canonical grounded-motor shape (Unity's 3D Game Kit motor): the
clip owns the ground plane, a persistent vertical channel owns Y.

- Horizontal: `animator.deltaPosition` with Y discarded (locomotion
  mode only). The delta is never projected onto the ground plane —
  raw projection scales stride by cos θ (−13 % at 30°, −29 % at 45°),
  which reads as foot slide; the controller's own sweep resolves
  height along slopes and steps instead.
- Vertical: persistent `_verticalVelocity`, integrated with
  `Time.deltaTime`. Grounded: **assigned** −2 m/s (never accumulated,
  never 0 — 0 flickers against PhysX depenetration). Airborne:
  accumulate gravity −20 m/s², clamped at −50 m/s terminal.
- One `Move` per frame. Grounded is read from that Move's
  `CollisionFlags.Below`, after the Move.
- Ground probe: a single-hit downward `SphereCast` (r 0.24, origin
  +0.35 m, length 0.85 m) run **before** the Move, because `isGrounded`
  reports only the last Move and carries no distance. Its mask excludes
  LocalPlayer, Zombie and Corpse, so it always measures the floor
  itself. Cost: one query per frame for the local player only.
- Ground snap: when she was grounded last frame and the probe finds
  the floor dropped by a step — gap in (skinWidth, 0.5 m], normal
  within `slopeLimit` — the drop is folded into the same Move's Y.
  This is what keeps her glued down stairs; gravity alone only
  re-catches her after each tread.

## Motor modes (CharacterStateContext.motorMode)

Exactly one owner of displacement per frame. States write the mode on
the shared blackboard; the motor consumes it.

| Mode | Who sets it | Motor behaviour |
| --- | --- | --- |
| Locomotion | default | clip plane + vertical channel, as above |
| ScriptedPin | CharacterTakeBiteState while the attacker is preparing | stands down completely — no Move, no gravity, no probe, no snap. The grapple pin's own Move (FSM update) is the frame's only motor action, so the two can never compose sink or a snap pop mid-grapple |
| AuthoredY | CharacterTakeBiteState once the grab phase ends | clip root motion verbatim, authored Y included (the push-off), no vertical channel |
| Disabled | — (death also early-outs on `isAlive`) | never moves her |

On bite exit the state returns the mode to Locomotion and requests a
motor reset; `PlayerRigging` requests the same reset after spawn
placement. A reset assigns the stick speed, clears the grounded flag,
and disarms the snap for 2 frames — stale fall speed or grounding must
never cross a scripted interval or a discontinuous move (this project
does not auto-sync transforms; teleports go: controller off → write →
on, then reset the motor).

Remote copies never run the motor: when the NetworkAnimator is spawned
and not owned, `OnAnimatorMove` returns and NetworkTransform drives the
transform. The probe and the whole channel are owner-local; nothing
about the motor replicates.

## Corpse layer (ProjectSettings: TagManager + DynamicsManager)

Layer 8 is `Corpse`. The collision matrix changes exactly two pairs:
Corpse × LocalPlayer **off**, Corpse × Zombie **off**. Corpse still
collides with the world and with itself, so piles settle and stack
visibly; no character stands on, is blocked by, or probes onto a corpse.

Death teardown (`ActorBrainBase`, every actor, every peer — it runs
from the replicated death state and from the mirrored-death path):

1. Whole hierarchy re-layered to Corpse (was: Default). This preserves
   the old corpse-stealth property — AI vision scans LocalPlayer, and
   a Corpse is not on it.
2. The motor's own body stops colliding: the zombie's root capsule is
   disabled and its root rigidbody set kinematic (the root stays where
   the actor died; the bones are the corpse now); the player's
   CharacterController is disabled.
3. Once every bone rigidbody sleeps — or after 4 s, whichever first —
   all bones go kinematic. A field of corpses then costs no physics
   and cannot be re-awakened into jitter. (Zombie corpses are still
   destroyed on the existing `corpseDestroyDelay`.)

## Tuning knobs

All in `CharacterLocomotion`: gravity −20 m/s² (band −15…−25), stick
−2 m/s, terminal −50 m/s, snap max 0.5 m (do not raise it casually: it
is also the tallest ledge she will step down without falling; the math
assumes sprint ≤ ~8 m/s and stair drops ≤ ~0.35 m per frame),
probe r 0.24 / origin +0.35 / length 0.85.

## Known limits

- Walking through a corpse pile is now possible by design (the pile is
  visual). Feet can clip the top bones while crossing; the alternative
  (solid corpses) is what caused the levitation.
- The snap assumes the Animator stays in Normal update mode. Moving
  the motor to AnimatePhysics/FixedUpdate retimes root-motion
  sampling and is a separate, deliberate project — not a tweak.
- Motor authority gating covers the player. Zombie simulation remains
  host-authoritative as before; zombies gained no per-frame queries
  from this change (their Y is the NavMesh's job).
