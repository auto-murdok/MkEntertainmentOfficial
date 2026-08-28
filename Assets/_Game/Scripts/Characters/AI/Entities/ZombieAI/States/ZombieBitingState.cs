using UnityEngine;

public class ZombieBitingState : State<ZombieStates, ZombieContext>
{
    private const float VerticalMovementPrepareThreshold = 0.15f;
    private const float DefaultBittingRadius = 0.1f;
    private const float DefaultRadius = 0.3f;
    private const float DefaultAttackCooldown = 1.2f;

    private enum BitePhase { Prepare, Release }

    private Vector3 _initialPosition;
    private Quaternion _initialRotation;
    private State<ZombieStates, ZombieContext> _subState;
    private BitePhase _phase;
    private float _biteTimer;

    public void CheckTransitions(StateMachine<ZombieStates, ZombieContext> character)
    {
        if (!character._context.isBitting)
        {
            character.ChangeState(ZombieStates.Idle);
        }
    }

    public void EnterState(StateMachine<ZombieStates, ZombieContext> character)
    {
        _initialPosition = character.transform.position;
        _initialRotation = character.transform.rotation;
        ZombieContext context = character._context;
        float bittingRadius = context.data != null ? context.data.bittingAgentRadius : DefaultBittingRadius;
        if (context.agent != null)
        {
            context.agent.radius = bittingRadius;
            context.agent.ResetPath();
        }

        // The C# FSM now owns the full bite lifecycle: the bite ends after its
        // configured duration rather than relying on the Animator's state-exit event.
        _biteTimer = context.biteDuration > 0f ? context.biteDuration : DefaultAttackCooldown;

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
        context.attackCooldownTimer = DefaultAttackCooldown;
        context.interactable = null;
    }

    public void UpdateState(StateMachine<ZombieStates, ZombieContext> character)
    {
        float verticalMovement = character._context.animator != null
            ? character._context.animator.GetFloat(AnimatorUtils.VerticalHash)
            : 0f;

        BitePhase desired = verticalMovement > VerticalMovementPrepareThreshold ? BitePhase.Prepare : BitePhase.Release;
        if (desired != _phase)
        {
            _subState.ExitState(character);
            _phase = desired;
            _subState = desired == BitePhase.Prepare
                ? (State<ZombieStates, ZombieContext>)new BitePrepareState()
                : new BiteReleaseState();
            _subState.EnterState(character);
        }

        // Pin the zombie to its initial grab pose while rearing up.
        if (_phase == BitePhase.Prepare)
        {
            character.transform.position = _initialPosition;
            character.transform.rotation = _initialRotation;
        }

        _subState.UpdateState(character);

        // End the bite from the C# side once the duration elapses.
        _biteTimer -= Time.deltaTime;
        if (_biteTimer <= 0f && character._context.isBitting)
        {
            character._context.isBitting = false;
        }
    }
}

public class ZombieBitting : ZombieBitingState {}
