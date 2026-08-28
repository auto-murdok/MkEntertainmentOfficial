using System;
using UnityEngine;

public class CharacterTakeBiteState : State<CharacterState, CharacterStateContext>
{
    private const float AttackedAgentRadius = 0.1f;
    private const float DefaultAgentRadius = 0.3f;

    public void CheckTransitions(StateMachine<CharacterState, CharacterStateContext> stateMachine)
    {
        if (!stateMachine._context.isBeingAttacked)
        {
            stateMachine.ChangeState(CharacterState.Idle);
        }
    }

    public void EnterState(StateMachine<CharacterState, CharacterStateContext> stateMachine)
    {
        CharacterStateContext context = stateMachine._context;
        context.agent.radius = AttackedAgentRadius;
    }

    public void ExitState(StateMachine<CharacterState, CharacterStateContext> stateMachine)
    {
        CharacterStateContext context = stateMachine._context;
        context.agent.radius = DefaultAgentRadius;
    }

    public void UpdateState(StateMachine<CharacterState, CharacterStateContext> stateMachine)
    {
        if (stateMachine._context.attacker.isPreparing)
        {
            stateMachine.transform.rotation = stateMachine._context.attacker.victimHook.rotation;
            stateMachine.transform.position = stateMachine._context.attacker.victimHook.position;

            Debug.LogWarning($"{stateMachine.gameObject.name} is preparing...");
        }
    }
}
