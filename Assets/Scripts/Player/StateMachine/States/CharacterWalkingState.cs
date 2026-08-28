using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class CharacterWalkingState : State<CharacterState, CharacterStateContext>
{
    public void CheckTransitions(StateMachine<CharacterState, CharacterStateContext> character)
    {
        if (character._context.isBeingAttacked)
        {
            character.ChangeState(CharacterState.TakingBite);
        }

        if (character._context.movementInput == Vector2.zero || character._context.isAiming)
        {
            character.ChangeState(CharacterState.Idle);
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
        CharacterTransformUtils.HandleCharacterMovement(character._context);
        CharacterTransformUtils.HandleCharacterRotation(character.transform, character._context);
    }
}
