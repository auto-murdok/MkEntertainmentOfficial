using UnityEngine;

public class ZombieBitingState : State<ZombieStates, ZombieContext>
{
    private const float DefaultBitingRadius = 0.1f;
    private const float DefaultRadius = 0.3f;
    private const float DefaultAttackCooldown = 1.2f;
    // The bite timeline lives in BiteTimeline (the paired animation's
    // shared clock): contact at BiteTimeline.ContactTime, release at
    // BiteTimeline.ReleaseTime - the same moment PlayerData's
    // takeBiteDuration frees her, and the two must agree. There is
    // deliberately no per-zombie duration: one animation means one
    // timeline for every archetype. A future archetype that needs a
    // faster bite scales the clip and both anchors together.
    private const float DefaultBiteDamage = 30f;

    private enum BitePhase { Prepare, Release }

    private Vector3 _initialPosition;
    private State<ZombieStates, ZombieContext> _subState;
    private BitePhase _phase;
    private float _biteTimer;
    private float _releaseThreshold;
    private bool _hasLandedBite;
    private Collider[] _grappleColliders;
    private CharacterController _grappleVictim;
    private bool _grapplePairIgnored;

    // Distance at which the pair counts as separated for restoring
    // collision: past the reach of his ragdoll bones (they extend in
    // front of his root), not just the two body radii.
    private const float GrappleClearDistance = 0.75f;

    public void CheckTransitions(StateMachine<ZombieStates, ZombieContext> character)
    {
        if (!character._context.isBiting)
        {
            character.ChangeState(ZombieStates.Idle);
        }
    }

    public void EnterState(StateMachine<ZombieStates, ZombieContext> character)
    {
        _initialPosition = character.transform.position;
        ZombieContext context = character._context;
        float bittingRadius = context.data != null ? context.data.bittingAgentRadius : DefaultBitingRadius;
        if (context.agent != null)
        {
            context.agent.radius = bittingRadius;
            // Guarded like ZombieHandAttackState: ResetPath on an agent that has
            // not been placed on a NavMesh logs an error.
            if (context.agent.isOnNavMesh)
            {
                context.agent.ResetPath();
            }
        }

        // The C# FSM owns the full bite lifecycle: it runs the clip-anchored
        // timeline (contact, then release at her push) rather than relying on
        // the Animator's state-exit event. The prepare / release phases are
        // derived from this timer exactly once, so isPreparing is stable
        // (no per-frame flicker that used to thrash the victim's lock).
        _biteTimer = BiteTimeline.ReleaseTime;
        _releaseThreshold = BiteTimeline.ReleaseTime - BiteTimeline.ContactTime;
        _hasLandedBite = false;

        context.recentlyBitten = true;
        _phase = BitePhase.Prepare;
        _subState = new BitePrepareState();
        _subState.EnterState(character);

        BeginGrappleCollisionIgnore(character);
    }

    public void ExitState(StateMachine<ZombieStates, ZombieContext> character)
    {
        ZombieContext context = character._context;
        float defaultRadius = context.data != null ? context.data.defaultAgentRadius : DefaultRadius;
        if (context.agent != null)
        {
            context.agent.radius = defaultRadius;
        }

        _subState?.ExitState(character);
        _subState = null;

        context.isPreparing = false;
        EndGrappleCollisionRestore(character);
        // TUNING: delay before the zombie may bite again after a push-off. See also
        // ZombieData.biteRange (separation distance that clears recentlyBitten).
        context.attackCooldownTimer = DefaultAttackCooldown;
        context.interactable = null;
    }

    public void UpdateState(StateMachine<ZombieStates, ZombieContext> character)
    {
        // Derive the phase from the timer with a single transition (no animator
        // vertical-threshold flicker).
        BitePhase desired = _biteTimer > _releaseThreshold ? BitePhase.Prepare : BitePhase.Release;
        if (desired != _phase)
        {
            _subState.ExitState(character);
            _phase = desired;
            _subState = desired == BitePhase.Prepare
                ? (State<ZombieStates, ZombieContext>)new BitePrepareState()
                : new BiteReleaseState();
            _subState.EnterState(character);

            // The prepare -> release transition IS the animation's contact
            // moment (the head snap at BiteTimeline.ContactTime):
            // the bite lands here, not at grab start. Exactly one damage
            // event per bite; a zombie killed mid-grab never reaches this
            // and deals no bite damage.
            if (desired == BitePhase.Release && !_hasLandedBite)
            {
                _hasLandedBite = true;
                LandBiteDamage(character);
            }
        }

        // Pin the zombie's position to its initial grab pose during the prepare
        // (grab) phase only; the release phase is the push-off, driven by the
        // animation root motion. Rotation is not pinned: it eases toward the
        // victim across the grab so the bite lines up without a facing snap.
        if (_phase == BitePhase.Prepare)
        {
            character.transform.position = _initialPosition;
            EaseFacingTowardVictim(character);
        }

        _subState.UpdateState(character);

        // End the bite from the C# side once the duration elapses.
        _biteTimer -= Time.deltaTime;
        if (_biteTimer <= 0f && character._context.isBiting)
        {
            character._context.isBiting = false;
        }
    }

    // Grapple protocol: the paired clips pin the two roots at 0.44 m
    // (VictimHook at 0.79 pulled in by PlayerBitePullInDistance) - closer
    // than the sum of their body capsules, and closer than his ragdoll
    // bone colliders reach. So for the bite's duration this pair stops
    // colliding: her controller ignores his body capsule AND his bones.
    // (Scoped to the root capsule alone, her pin was held at the 0.60 m
    // capsule sum and the grapple read loose.) Every other character
    // keeps colliding with both of them. Restored on exit once they have
    // separated (see ZombieBehavior's deferred restore for the
    // against-a-wall case). Plain test doubles are not Components and
    // skip the protocol entirely.
    private void BeginGrappleCollisionIgnore(StateMachine<ZombieStates, ZombieContext> character)
    {
        ZombieContext context = character._context;
        _grappleColliders = character.transform.GetComponentsInChildren<Collider>(true);
        _grappleVictim = ResolveVictimController(context);
        _grapplePairIgnored = false;
        if (_grappleColliders.Length == 0 || _grappleVictim == null)
        {
            _grappleColliders = null;
            return;
        }

        if (character is ZombieBehavior behavior)
        {
            behavior.CancelPendingCollisionRestore();
        }

        foreach (Collider collider in _grappleColliders)
        {
            Physics.IgnoreCollision(collider, _grappleVictim, true);
        }
        _grapplePairIgnored = true;
    }

    private void EndGrappleCollisionRestore(StateMachine<ZombieStates, ZombieContext> character)
    {
        if (!_grapplePairIgnored)
        {
            return;
        }
        _grapplePairIgnored = false;

        if (_grappleColliders != null && _grappleVictim != null)
        {
            Vector3 a = character.transform.position;
            Vector3 b = _grappleVictim.transform.position;
            a.y = 0f;
            b.y = 0f;
            if (Vector3.Distance(a, b) >= GrappleClearDistance)
            {
                RestoreGrappleCollision();
            }
            else if (character is ZombieBehavior behavior)
            {
                behavior.RestoreCollisionWhenSeparated(_grappleColliders, _grappleVictim, GrappleClearDistance);
            }
            else
            {
                RestoreGrappleCollision();
            }
        }

        _grappleColliders = null;
        _grappleVictim = null;
    }

    private void RestoreGrappleCollision()
    {
        foreach (Collider collider in _grappleColliders)
        {
            if (collider != null)
            {
                Physics.IgnoreCollision(collider, _grappleVictim, false);
            }
        }
    }

    private static CharacterController ResolveVictimController(ZombieContext context)
    {
        if (context.interactable is Component victimComponent)
        {
            CharacterController controller = victimComponent.GetComponent<CharacterController>();
            return controller != null
                ? controller
                : victimComponent.GetComponentInChildren<CharacterController>();
        }

        return null;
    }

    // TUNING: facing ease rate (per second) while the grab lines up on the
    // victim; frame-rate independent via AnimatorUtils.DampFactor.
    private const float FacingEaseSpeed = 6f;

    private void EaseFacingTowardVictim(StateMachine<ZombieStates, ZombieContext> character)
    {
        IInteractable victim = character._context.interactable;
        if (victim == null)
        {
            return;
        }

        Vector3 toVictim = victim.position - character.transform.position;
        toVictim.y = 0f;
        if (toVictim.sqrMagnitude < 0.0001f)
        {
            return;
        }

        Quaternion desired = Quaternion.LookRotation(toVictim.normalized);
        character.transform.rotation = Quaternion.Slerp(
            character.transform.rotation, desired, AnimatorUtils.DampFactor(FacingEaseSpeed, Time.deltaTime));
    }

    // Attacker-driven damage, scored once at the contact moment (same pattern
    // as ZombieHandAttackState.ApplyDamage). No range check: the victim is
    // pinned to the bite socket by construction for the whole grab.
    private void LandBiteDamage(StateMachine<ZombieStates, ZombieContext> character)
    {
        ZombieContext context = character._context;
        IInteractable victim = context.interactable != null ? context.interactable : context.target as IInteractable;
        if (victim is not IDamageable damageable)
        {
            return;
        }

        float damage = context.data != null ? context.data.biteDamage : DefaultBiteDamage;
        using (CombatLog.BeginSource("ZombieBite"))
        {
            damageable.TakeDamage(damage);
        }
    }
}

public class ZombieBiting : ZombieBitingState {}
