using UnityEngine;

public class CharacterWalkingState : State<CharacterState, CharacterStateContext>
{
    public void CheckTransitions(StateMachine<CharacterState, CharacterStateContext> stateMachine)
    {
        if (stateMachine._context.isBeingAttacked)
        {
            stateMachine.ChangeState(CharacterState.TakingBite);
        }
        else if (stateMachine._context.movementInput == Vector2.zero || stateMachine._context.isAiming)
        {
            stateMachine.ChangeState(CharacterState.Idle);
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
        CharacterTransformUtils.HandleCharacterMovement(stateMachine._context);
        CharacterTransformUtils.HandleCharacterRotation(stateMachine.transform, stateMachine._context);
    }
}
