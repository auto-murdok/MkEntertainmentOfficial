using UnityEngine;

public class CharacterAimState : State<CharacterState, CharacterStateContext>
{
    public void CheckTransitions(StateMachine<CharacterState, CharacterStateContext> character)
    {
        if (character._context.isBeingAttacked)
        {
            character.ChangeState(CharacterState.TakingBite);
        }
        else if (!character._context.isAiming && !character._context.isReloading)
        {
            character.ChangeState(CharacterState.Idle);
        }
    }

    public void EnterState(StateMachine<CharacterState, CharacterStateContext> character)
    {
        CharacterUIContext characterUIContext = new CharacterUIContext()
        {
            displayCrossair = character._context.isAiming
        };
        // character._context.UIController.NotifyObservers(CharacterUIElement.AimUI, characterUIContext);
    }

    public void ExitState(StateMachine<CharacterState, CharacterStateContext> character)
    {
        // do nothing
    }

    public void UpdateState(StateMachine<CharacterState, CharacterStateContext> character)
    {
        CharacterTransformUtils.HandleCharacterRotation(character.transform, character._context);
        AnimatorUtils.SetLayerWeight(character._context.animator, 1, 1f, 20f);
        RigUtils.HandleIncreaseRigWeight(character._context.rig);
    }
}