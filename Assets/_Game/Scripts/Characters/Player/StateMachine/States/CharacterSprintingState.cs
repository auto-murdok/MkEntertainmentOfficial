using UnityEngine;

public class CharacterSprintingState : State<CharacterState, CharacterStateContext>
{
    public void CheckTransitions(StateMachine<CharacterState, CharacterStateContext> stateMachine)
    {
        var next = CharacterStateResolver.Resolve(stateMachine._context);
        if (next.HasValue) stateMachine.ChangeState(next.Value);
    }

    public void EnterState(StateMachine<CharacterState, CharacterStateContext> stateMachine)
    {
    }

    public void ExitState(StateMachine<CharacterState, CharacterStateContext> stateMachine)
    {
    }

    public void UpdateState(StateMachine<CharacterState, CharacterStateContext> stateMachine)
    {
        CharacterTransformUtils.HandleSprintingMovement(stateMachine._context);
        CharacterTransformUtils.HandleCharacterRotation(stateMachine.transform, stateMachine._context);
    }
}
