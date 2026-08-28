using UnityEngine;

public class CharacterIdleState : State<CharacterState, CharacterStateContext>
{
    public void CheckTransitions(StateMachine<CharacterState, CharacterStateContext> character)
    {
        CharacterStateContext context = character._context;

        if (character._context.isBeingAttacked)
        {
            character.ChangeState(CharacterState.TakingBite);
        }

        if (character._context.isAiming || character._context.isReloading)
        {
            character.ChangeState(CharacterState.Aiming);
        }

        if (context.isAiming)
        {
            character.ChangeState(CharacterState.Aiming);
        }
        else if (context.movementInput != Vector2.zero)
        {
            character.ChangeState(CharacterState.Moving);
        }
    }

    public void EnterState(StateMachine<CharacterState, CharacterStateContext> character)
    {

    }

    public void ExitState(StateMachine<CharacterState, CharacterStateContext> character)
    {

    }

    public void UpdateState(StateMachine<CharacterState, CharacterStateContext> character)
    {

    }
}
