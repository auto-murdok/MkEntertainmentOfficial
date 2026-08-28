using UnityEngine;

public class ZombieChasing : State<ZombieStates, ZombieContext>
{
    private ZombieHand[] zombieHands;

    public void CheckTransitions(StateMachine<ZombieStates, ZombieContext> character)
    {
        if (character._context.isBitting)
        {
            character.ChangeState(ZombieStates.Bitting);
        }
    }

    public void EnterState(StateMachine<ZombieStates, ZombieContext> character)
    {
        zombieHands = character.GetComponentsInChildren<ZombieHand>(true);
        foreach (ZombieHand hand in zombieHands)
        {
            hand.Enable();
        }
    }

    public void ExitState(StateMachine<ZombieStates, ZombieContext> character)
    {
        foreach (ZombieHand hand in zombieHands)
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

        if (AITransformUtils.HasReachedTarget(character.transform, context.agent))
        {
            context.agent.ResetPath();
            character.ChangeState(ZombieStates.Idle);
        }
    }
}