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
        if (character._context.isBiting)
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

            if (sqrDistance <= (biteRange * biteRange) && !context.isBiting && !context.recentlyBitten)
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
