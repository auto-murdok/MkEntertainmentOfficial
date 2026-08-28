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
        if (context.agent != null)
        {
            context.agent.radius = AttackedAgentRadius;
            context.agent.ResetPath();
        }
    }

    public void ExitState(StateMachine<CharacterState, CharacterStateContext> stateMachine)
    {
        CharacterStateContext context = stateMachine._context;
        if (context.agent != null)
        {
            context.agent.radius = DefaultAgentRadius;
        }
    }

    public void UpdateState(StateMachine<CharacterState, CharacterStateContext> stateMachine)
    {
        // If the attacker is gone (e.g. killed mid-bite), bail out safely.
        if (stateMachine._context.attacker == null)
        {
            stateMachine.ChangeState(CharacterState.Idle);
            return;
        }

        // Only lock position and rotation while the attacker is in the initial grab/prepare phase.
        // Once isPreparing is false (bite & push-off phase), release the transform to let the
        // pushback animation/root motion execute naturally.
        if (stateMachine._context.attacker.isPreparing)
        {
            stateMachine.transform.position = stateMachine._context.attacker.victimHook.position;
            stateMachine.transform.rotation = stateMachine._context.attacker.victimHook.rotation;
        }
    }
}
