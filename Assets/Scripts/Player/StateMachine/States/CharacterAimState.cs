using UnityEngine;

public class CharacterAimState : State<CharacterState, CharacterStateContext>
{
    private const int AimAnimatorLayerIndex = 1;
    private const float AimLayerWeightTarget = 1f;
    private const float AimLayerWeightSpeed = 20f;

    public void CheckTransitions(StateMachine<CharacterState, CharacterStateContext> stateMachine)
    {
        if (stateMachine._context.isBeingAttacked)
        {
            stateMachine.ChangeState(CharacterState.TakingBite);
        }
        else if (!stateMachine._context.isAiming && !stateMachine._context.isReloading)
        {
            stateMachine.ChangeState(CharacterState.Idle);
        }
    }

    public void EnterState(StateMachine<CharacterState, CharacterStateContext> stateMachine)
    {
        if (stateMachine._context.UIController != null)
        {
            stateMachine._context.UIController.NotifyObservers(
                CharacterUIElement.AimUI,
                CharacterUIContext.CreateAimUI(true));
        }
    }

    public void ExitState(StateMachine<CharacterState, CharacterStateContext> stateMachine)
    {
        if (stateMachine._context.UIController != null)
        {
            stateMachine._context.UIController.NotifyObservers(
                CharacterUIElement.AimUI,
                CharacterUIContext.CreateAimUI(false));
        }
    }

    public void UpdateState(StateMachine<CharacterState, CharacterStateContext> stateMachine)
    {
        CharacterTransformUtils.HandleCharacterRotation(stateMachine.transform, stateMachine._context);
        AnimatorUtils.SetLayerWeight(stateMachine._context.animator, AimAnimatorLayerIndex, AimLayerWeightTarget, AimLayerWeightSpeed);
        RigUtils.HandleIncreaseRigWeight(stateMachine._context.rig);
    }
}
