# Zombie locomotion: velocity-fed blend + speed matching

Why zombies "walked weird", measured in the live arena (Walker, 8 m
chase, current build before this change):

- The zombie actually moved at **~0.5 m/s** while its `ZombieData`
  chase speed said 3.5 m/s. Nothing read the data speeds at all — the
  pace was an accident of clip speed × blend-tree extrapolation.
- The blend tree's walk clip sits at (0, 0.5), but the chase fed a unit
  direction (magnitude 1.0), so every gait played at **2× extrapolation**
  from its own authored speed (base walk 0.31 m/s, creep 1.03, limp
  0.93, stiff 0.63) — no two zombies paced alike.
- The blend floats were damped at **0.5 s**, taking ~2.75 s to reach
  full stride, and a binary facing gate (dot > 0.5, else zero) made
  turning zombies glide at whatever stride the damp was holding.
- `agent.velocity` and `agent.desiredVelocity` read **0.00 for the
  entire chase**: the NavMeshAgent's speed is 0 by design (root motion
  is the only mover), so the agent can never feed the blend.

## The design

**Speed matching.** A `SpeedMul` float parameter on AC_Zombie is bound
as the locomotion state's playback speed. Each frame the FSM sets it to
`clamp(intended ÷ currentClipAuthored, 0.6, 1.6)` — intended from
`ZombieData` (chase in Chasing, walk otherwise), authored from the walk
clip actually in the tree (`ZombieAnimationVariant.CurrentWalkAuthoredSpeed`,
seed-derived on every peer, so all peers agree). The clip's stride and
its root displacement scale together, so feet stay planted at any rate
in the window.

**Velocity-fed blend.** The chase blend input is built from the body's
**displacement-measured** velocity (tracked on `ZombieContext`, 0.1 s
smoothing, stale measurements discarded):

- direction: the measured velocity's direction (steering direction
  while stationary, so the first step can begin);
- magnitude: `0.5 × sqrt(clamp01(measured ÷ targetPace))` — 0.5 is the
  walk clip's home position in the tree, so a zombie at pace plays its
  clip at weight 1 with no extrapolation. The square root matters: a
  linear feed is a neutral loop (measured in-engine: ~6 s to converge
  to pace), while the sqrt gives gain below pace and damping above it,
  converging in a few steps. A startup stride (0.6) covers the first
  0.5 s of a movement episode — a stationary body measures zero, so
  the curve alone could never start it — and a strain floor (0.25)
  keeps a crowd-held zombie shuffling weakly instead of frozen, ready
  to re-accelerate the moment it breaks free;
- facing: the old binary gate is now a cosine falloff,
  `× clamp01(dot(forward, steering))` — stride fades as heading error
  grows, so turns happen mostly in place and never snap at a threshold.

**Responsiveness.** The blend damp is 0.12 s everywhere the zombie
blend is fed (chase util and the commanded-move/patrol state), and the
chase feeds the blend every frame — a zero feed included — instead of
only when the input was non-zero.

## Tuning knobs

| Knob | Where | Value |
|---|---|---|
| SpeedMul window | `ZombieAnimatorUtils.MinSpeedMul/MaxSpeedMul` | 0.6 – 1.6 |
| Walk home magnitude | `ZombieAnimatorUtils.LocomotionHomeMagnitude` | 0.5 (pinned to the controller by test) |
| Blend damp | `ZombieAnimatorUtils.MovementSmoothingTime`, `ActorMoveToTargetState.RootMotionSmoothing` | 0.12 s |
| Velocity smoothing | `AITransformUtils.VelocitySmoothingSeconds` | 0.1 s |
| Turn rate | `AITransformUtils.MaxRotationDegreesPerSecond` | 180°/s (unchanged) |

## Limits

- The data paces (Walker chase 3.5, Runner chase 5.5) sit far above
  every clip's authored speed, so the 1.6× clamp binds: a Walker's
  effective chase pace is ~0.5–1.65 m/s depending on its variant gait.
  Raising zombie pace for real means authoring faster gaits or raising
  the clamp and accepting strobing — a game-feel call, not a bug fix.
  This change makes the pace *consistent and foot-true*, and makes the
  data speed the actual input to it within the sane window.
- The commanded-move (patrol) feed caps magnitude at the home position
  and shares the damp, but keeps steering-direction feeding; the
  measured-velocity magnitude feed is chase-only.
- Zombies still run zero bespoke physics queries; the measurement is
  one vector subtraction per chasing zombie per frame.
