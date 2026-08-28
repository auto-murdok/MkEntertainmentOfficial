using UnityEngine;

public class ZombieChasing : State<ZombieStates, ZombieContext>
{
    private ZombieHand[] _zombieHands;

    public void CheckTransitions(StateMachine<ZombieStates, ZombieContext> character)
    {
        if (character._context.isBitting)
        {
            character.ChangeState(ZombieStates.Bitting);
        }
    }

    public void EnterState(StateMachine<ZombieStates, ZombieContext> character)
    {
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
            float distance = Vector3.Distance(character.transform.position, context.target.TargetPosition);
            float biteRange = context.data != null ? context.data.biteRange : ZombieBehavior.DefaultBiteRange;

            if (distance <= biteRange && !context.isBitting)
            {
                if (character is ZombieBehavior behavior && behavior.TryTriggerAttack())
                {
                    return;
                }
            }

            if (context.agent != null && context.agent.isActiveAndEnabled && context.agent.isOnNavMesh)
            {
                context.agent.SetDestination(context.target.TargetPosition);
            }
        }
    }
}
