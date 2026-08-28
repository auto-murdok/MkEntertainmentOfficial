using UnityEngine;

public class CharacterWalkingState : State<CharacterState, CharacterStateContext>
{
    public void CheckTransitions(StateMachine<CharacterState, CharacterStateContext> stateMachine)
    {
        CharacterStateContext context = stateMachine._context;

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
        else if (context.movementInput == Vector2.zero)
        {
            stateMachine.ChangeState(CharacterState.Idle);
        }
        else if (context.isRunning)
        {
            stateMachine.ChangeState(CharacterState.Sprinting);
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
        CharacterTransformUtils.HandleWalkingMovement(stateMachine._context);
        CharacterTransformUtils.HandleCharacterRotation(stateMachine.transform, stateMachine._context);
    }
}
