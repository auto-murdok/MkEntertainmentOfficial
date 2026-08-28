using UnityEngine;

public class CharacterReloadingState : State<CharacterState, CharacterStateContext>
{
    private const int HandgunAimLayerIndex = 1;
    private const float LayerWeightTarget = 1f;
    private const float LayerWeightSpeed = 20f;

    public void CheckTransitions(StateMachine<CharacterState, CharacterStateContext> stateMachine)
    {
        var next = CharacterStateResolver.Resolve(stateMachine._context);
        if (next.HasValue) stateMachine.ChangeState(next.Value);
    }

    public void EnterState(StateMachine<CharacterState, CharacterStateContext> stateMachine)
    {
        // Immediately cancel any sprint flag and stop root motion
        stateMachine._context.isRunning = false;
        AnimatorUtils.SetMovementRootMotion(stateMachine._context.animator, Vector2.zero, 0f);

        // Turn off Aim UI crosshair if it was active
        if (stateMachine._context.UIController != null)
        {
            stateMachine._context.UIController.NotifyObservers(
                CharacterUIElement.AimUI,
                CharacterUIContext.CreateAimUI(false));
        }
    }

    public void ExitState(StateMachine<CharacterState, CharacterStateContext> stateMachine)
    {
    }

    public void UpdateState(StateMachine<CharacterState, CharacterStateContext> stateMachine)
    {
        // Strictly immobilize the player during reload
        AnimatorUtils.SetMovementRootMotion(stateMachine._context.animator, Vector2.zero, 0.1f);
        
        // Raise Handgun layer so reload animation plays
        AnimatorUtils.SetLayerWeight(stateMachine._context.animator, HandgunAimLayerIndex, LayerWeightTarget, LayerWeightSpeed);
        
        // Lower Rig/IK so it does not distort the magazine swap arm animation
        RigUtils.HandleDecreaseRigWeight(stateMachine._context.rig);

        // Allow looking/turning towards camera aim
        CharacterTransformUtils.HandleCharacterRotation(stateMachine.transform, stateMachine._context);
    }
}
