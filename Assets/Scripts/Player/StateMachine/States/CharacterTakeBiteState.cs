using System;
using UnityEngine;

public class CharacterTakeBiteState : State<CharacterState, CharacterStateContext>
{
    public void CheckTransitions(StateMachine<CharacterState, CharacterStateContext> character)
    {
        if (!character._context.isBeingAttacked)
        {
            character.ChangeState(CharacterState.Idle);
        }
    }

    public void EnterState(StateMachine<CharacterState, CharacterStateContext> character)
    {
        CharacterStateContext context = character._context;
        context.agent.radius = 0.1f;
    }

    public void ExitState(StateMachine<CharacterState, CharacterStateContext> character)
    {
        CharacterStateContext context = character._context;
        context.agent.radius = 0.3f;
    }

    public void UpdateState(StateMachine<CharacterState, CharacterStateContext> character)
    {
        // float horizontalInput = Math.Abs(character._context.animator.GetFloat("Horizontal"));
        // float verticalInput = Math.Abs(character._context.animator.GetFloat("Vertical"));
        // if (verticalInput > 0.15f || horizontalInput > 0.15f)
        // {
        //     character.transform.rotation = character._context.attacker.victimHook.rotation;
        //     character.transform.position = character._context.attacker.victimHook.position;

        //     Debug.LogWarning($"{character.gameObject.name} is preparing...");
        // }

        if (character._context.attacker.isPreparing)
        {
            character.transform.rotation = character._context.attacker.victimHook.rotation;
            character.transform.position = character._context.attacker.victimHook.position;

            Debug.LogWarning($"{character.gameObject.name} is preparing...");
        }
    }
}
