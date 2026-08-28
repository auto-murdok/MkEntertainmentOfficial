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
        _zombieHands = character.GetComponentsInChildren<ZombieHand>(true);
        foreach (ZombieHand hand in _zombieHands)
        {
            hand.Enable();
        }
    }

    public void ExitState(StateMachine<ZombieStates, ZombieContext> character)
    {
        foreach (ZombieHand hand in _zombieHands)
        {
            hand.Disable();
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
            context.agent.SetDestination(context.target.TargetPosition);
        }
    }
}
