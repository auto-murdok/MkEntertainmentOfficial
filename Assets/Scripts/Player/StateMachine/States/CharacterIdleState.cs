using UnityEngine;

public class CharacterIdleState : State<CharacterState, CharacterStateContext>
{
    public void CheckTransitions(StateMachine<CharacterState, CharacterStateContext> stateMachine)
    {
        CharacterStateContext context = stateMachine._context;

        if (context.isBeingAttacked)
        {
            stateMachine.ChangeState(CharacterState.TakingBite);
        }

        if (context.isAiming || context.isReloading)
        {
            stateMachine.ChangeState(CharacterState.Aiming);
        }

        if (context.isAiming)
        {
            stateMachine.ChangeState(CharacterState.Aiming);
        }
        else if (context.movementInput != Vector2.zero)
        {
            stateMachine.ChangeState(CharacterState.Moving);
        }
    }

    public void EnterState(StateMachine<CharacterState, CharacterStateContext> stateMachine)
    {
    }

    public void ExitState(StateMachine<CharacterState, CharacterStateContext> stateMachine)
    {
    }

    public void UpdateState(StateMachine<CharacterState, CharacterStateContext> stateMachine)
    {
    }
}
