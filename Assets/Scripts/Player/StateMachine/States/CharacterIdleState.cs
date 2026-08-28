using UnityEngine;

public class CharacterIdleState : State<CharacterState, CharacterStateContext>
{
    public void CheckTransitions(StateMachine<CharacterState, CharacterStateContext> stateMachine)
    {
        CharacterStateContext context = stateMachine._context;

        // Priority chain: being attacked wins over aiming/moving. The base
        // StateMachine applies at most one transition per frame (first request
        // wins), so ordering the checks here defines the priority.
        if (context.isBeingAttacked)
        {
            stateMachine.ChangeState(CharacterState.TakingBite);
        }
        else if (context.isReloading)
        {
            stateMachine.ChangeState(CharacterState.Reloading);
        }
        else if (context.isAiming)
        {
            stateMachine.ChangeState(CharacterState.Aiming);
        }
        else if (context.movementInput != Vector2.zero)
        {
            if (context.isRunning && !context.isAiming)
            {
                stateMachine.ChangeState(CharacterState.Sprinting);
            }
            else
            {
                stateMachine.ChangeState(CharacterState.Walking);
            }
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
