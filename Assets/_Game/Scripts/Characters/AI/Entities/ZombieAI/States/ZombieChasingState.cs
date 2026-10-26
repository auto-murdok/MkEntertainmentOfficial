using UnityEngine;

public class ZombieChasingState : State<ZombieStates, ZombieContext>
{
    private const float DestinationUpdateInterval = 0.15f;
    private const float DestinationMoveThresholdSqr = 0.25f; // 0.5m squared
    private const float ChaseAcceleration = 8f;
    private const float DefaultChaseSpeed = 3.5f;

    private ZombieHand[] _zombieHands;
    private float _destinationUpdateTimer;
    private Vector3 _lastDestination = Vector3.positiveInfinity;

    /// <summary>
    /// The speed the NavMeshAgent may actually chase at: the data chase
    /// speed, capped at what the active gait clip can honestly play
    /// (natural speed x the playback-rate ceiling). An unknown natural
    /// speed (variant not applied yet) leaves the data speed uncapped.
    /// </summary>
    public static float EffectiveChaseSpeed(float chaseSpeed, float naturalSpeed)
    {
        if (naturalSpeed <= 0f)
        {
            return chaseSpeed;
        }
        return Mathf.Min(chaseSpeed, naturalSpeed * ZombieAnimationVariant.MaxRate);
    }

    public void CheckTransitions(StateMachine<ZombieStates, ZombieContext> character)
    {
        if (character._context.isHandAttacking)
        {
            character.ChangeState(ZombieStates.HandAttacking);
        }
        else if (character._context.isBiting)
        {
            character.ChangeState(ZombieStates.Biting);
        }
        else if (character._context.moveDestination != null)
        {
            character.ChangeState(ZombieStates.CommandedMove);
        }
    }

    public void EnterState(StateMachine<ZombieStates, ZombieContext> character)
    {
        _destinationUpdateTimer = 0f;
        _lastDestination = Vector3.positiveInfinity;

        // The chase is agent-driven: the NavMeshAgent moves the body at the
        // (gait-capped) chase speed while root motion is suspended, so the
        // clip's travel no longer sets the chase pace. Facing stays with
        // AITransformUtils (updateRotation off), and the agent stops just
        // inside bite range, where the attack checks take over.
        ZombieContext context = character._context;
        if (context.agent != null)
        {
            float chaseSpeed = context.data != null ? context.data.chaseSpeed : DefaultChaseSpeed;
            context.agent.speed = EffectiveChaseSpeed(chaseSpeed, context.locomotionNaturalSpeed);
            context.agent.acceleration = ChaseAcceleration;
            context.agent.updateRotation = false;
            float biteRange = context.data != null ? context.data.biteRange : ZombieBehavior.DefaultBiteRange;
            context.agent.stoppingDistance = biteRange * 0.8f;
        }
        if (context.animator != null)
        {
            context.animator.applyRootMotion = false;
        }

        _zombieHands = character._context.hands != null ? character._context.hands : character.GetComponentsInChildren<ZombieHand>(true);
        for (int i = 0; i < _zombieHands.Length; i++)
        {
            if (_zombieHands[i] != null)
            {
                _zombieHands[i].Enable();
            }
        }
    }

    public void ExitState(StateMachine<ZombieStates, ZombieContext> character)
    {
        // Restore the root-motion locomotion model every other state uses:
        // agent parked (speed 0, no destination) and root motion back on.
        ZombieContext context = character._context;
        if (context.agent != null)
        {
            context.agent.speed = 0f;
            context.agent.stoppingDistance = 0f;
            if (context.agent.isActiveAndEnabled && context.agent.isOnNavMesh)
            {
                context.agent.ResetPath();
            }
        }
        if (context.animator != null)
        {
            context.animator.applyRootMotion = true;
        }

        if (_zombieHands != null)
        {
            for (int i = 0; i < _zombieHands.Length; i++)
            {
                if (_zombieHands[i] != null)
                {
                    _zombieHands[i].Disable();
                }
            }
        }
    }

    public void UpdateState(StateMachine<ZombieStates, ZombieContext> character)
    {
        ZombieContext context = character._context;
        Vector2 movementThreshold = AITransformUtils.GetAIMovementThreshold(character.transform, context.agent, context.animator);

        if (movementThreshold != Vector2.zero)
        {
            ZombieAnimatorUtils.ApplyRootMotionMovement(context.animator, movementThreshold);
        }

        if (context.target != null)
        {
            Vector3 targetPosition = context.target.TargetPosition;
            float sqrDistance = (character.transform.position - targetPosition).sqrMagnitude;
            float biteRange = context.data != null ? context.data.biteRange : ZombieBehavior.DefaultBiteRange;

            if (sqrDistance <= (biteRange * biteRange) && !context.isBiting && !context.isHandAttacking && !context.recentlyBitten)
            {
                if (character is ZombieBehavior behavior && behavior.TryTriggerAttack())
                {
                    return;
                }
            }

            _destinationUpdateTimer -= Time.deltaTime;
            bool shouldUpdateDestination = _destinationUpdateTimer <= 0f || (targetPosition - _lastDestination).sqrMagnitude > DestinationMoveThresholdSqr;

            if (shouldUpdateDestination && context.agent != null && context.agent.isActiveAndEnabled && context.agent.isOnNavMesh)
            {
                _destinationUpdateTimer = DestinationUpdateInterval;
                _lastDestination = targetPosition;
                context.agent.SetDestination(targetPosition);
            }
        }
    }
}
