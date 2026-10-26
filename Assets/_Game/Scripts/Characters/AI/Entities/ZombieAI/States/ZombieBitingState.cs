using UnityEngine;

public class ZombieBitingState : State<ZombieStates, ZombieContext>
{
    private const float DefaultBitingRadius = 0.1f;
    private const float DefaultRadius = 0.3f;
    private const float DefaultAttackCooldown = 1.2f;
    // TUNING: fraction of the total bite (ZombieData.biteDuration) spent in the push-off /
    // release phase. The remaining (1 - ReleaseFraction) is the grab (victim locked). Lower
    // = longer grab / shorter push-off. Drives isPreparing with a single transition (no flicker).
    private const float ReleaseFraction = 0.35f; // last 35% of the bite is the push-off
    private const float DefaultBiteDamage = 30f;

    private enum BitePhase { Prepare, Release }

    private Vector3 _initialPosition;
    private Quaternion _initialRotation;
    private State<ZombieStates, ZombieContext> _subState;
    private BitePhase _phase;
    private float _biteTimer;
    private float _releaseThreshold;
    private bool _hasLandedBite;

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
        _initialRotation = character.transform.rotation;
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

        // The C# FSM owns the full bite lifecycle: the bite ends after its configured
        // duration rather than relying on the Animator's state-exit event. The prepare /
        // release phases are derived from this timer exactly once, so isPreparing is
        // stable (no per-frame flicker that used to thrash the victim's lock).
        _biteTimer = context.biteDuration > 0f ? context.biteDuration : DefaultAttackCooldown;
        _releaseThreshold = _biteTimer * ReleaseFraction;
        _hasLandedBite = false;

        context.recentlyBitten = true;
        _phase = BitePhase.Prepare;
        _subState = new BitePrepareState();
        _subState.EnterState(character);
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
            // moment (the head snap at ~65% of the bite): the bite lands here,
            // not at grab start. Exactly one damage event per bite; a zombie
            // killed mid-grab never reaches this and deals no bite damage.
            if (desired == BitePhase.Release && !_hasLandedBite)
            {
                _hasLandedBite = true;
                LandBiteDamage(character);
            }
        }

        // Pin the zombie to its initial grab pose during the prepare (grab) phase only;
        // the release phase is the push-off, driven by the animation root motion.
        if (_phase == BitePhase.Prepare)
        {
            character.transform.position = _initialPosition;
            character.transform.rotation = _initialRotation;
        }

        _subState.UpdateState(character);

        // End the bite from the C# side once the duration elapses.
        _biteTimer -= Time.deltaTime;
        if (_biteTimer <= 0f && character._context.isBiting)
        {
            character._context.isBiting = false;
        }
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
