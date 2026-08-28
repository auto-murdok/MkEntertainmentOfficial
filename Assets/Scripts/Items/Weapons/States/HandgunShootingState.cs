using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HandgunShootingState : State<HandgunState, HandgunContext>
{
    // Rechambering delay before the state can complete. Reset on every Enter
    // so the cadence is identical for each shot.
    private const float RechamberingStartTime = 0.05f;
    private const string ShootAnimationName = "fakeGun_shoot";
    private const string IdleAnimationName = "Idle";

    private float _rechamberingTime;

    public void CheckTransitions(StateMachine<HandgunState, HandgunContext> character)
    {
        if (_rechamberingTime < 0f)
        {
            character.ChangeState(HandgunState.Ready);
        }
    }

    public void EnterState(StateMachine<HandgunState, HandgunContext> character)
    {
        _rechamberingTime = RechamberingStartTime;
        Handgun fireArm = (Handgun)character;
        character._context.animator.CrossFade(ShootAnimationName, 0f);
        fireArm.ExecuteActualShoot();
        fireArm.fireArmEvents.onShoot?.Invoke();
        character._context.clipSize--;

        if (character._context.UIController != null)
        {
            character._context.UIController.NotifyObservers(
                CharacterUIElement.ShootUI,
                CharacterUIContext.CreateShootUI(character._context.clipSize, character._context.maxClipSize));
        }
    }

    public void ExitState(StateMachine<HandgunState, HandgunContext> character)
    {
        character._context.isTriggerPressed = false;
        character._context.animator.CrossFade(IdleAnimationName, 0f);
    }

    public void UpdateState(StateMachine<HandgunState, HandgunContext> character)
    {
        _rechamberingTime -= Time.deltaTime;
    }
}
