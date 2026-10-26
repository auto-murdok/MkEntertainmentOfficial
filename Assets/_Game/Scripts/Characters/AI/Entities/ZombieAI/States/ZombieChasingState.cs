using UnityEngine;

public class ZombieChasingState : State<ZombieStates, ZombieContext>
{
    private const float DestinationUpdateInterval = 0.15f;
    private const float DestinationMoveThresholdSqr = 0.25f; // 0.5m squared

    private ZombieHand[] _zombieHands;
    private float _destinationUpdateTimer;
    private Vector3 _lastDestination = Vector3.positiveInfinity;

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

        // A fresh movement episode: the velocity measurement restarts
        // (and with it the blend feed's startup window) on every chase
        // entry instead of inheriting a stale sample from the last one.
        character._context.hasMeasuredPosition = false;

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

        // The blend is fed every frame — including a zero feed — so a
        // stalled zombie's stride winds down instead of the damped
        // floats holding their last value. The pace it winds toward is
        // the speed-matched chase pace for the current walk clip.
        float authoredSpeed = (character as ZombieBehavior)?.CurrentWalkAuthoredSpeed ?? 1f;
        float targetSpeed = authoredSpeed * ZombieAnimatorUtils.ComputeSpeedMul(context.data, true, authoredSpeed);
        Vector2 movementBlend = AITransformUtils.GetAIMovementBlend(character.transform, context.agent, context, targetSpeed);
        ZombieAnimatorUtils.ApplyRootMotionMovement(context.animator, movementBlend);

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
