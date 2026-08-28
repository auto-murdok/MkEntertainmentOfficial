using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HandgunShootingState : State<HandgunState, HandgunContext>
{
    private float _rechamberingTime = 0.05f;

    public void CheckTransitions(StateMachine<HandgunState, HandgunContext> character)
    {
        // do nothing
    }

    public void EnterState(StateMachine<HandgunState, HandgunContext> character)
    {
        Handgun fireArm = (Handgun)character;
        character._context.animator.CrossFade("fakeGun_shoot", 0f);
        fireArm.ExecuteActualShoot();
        fireArm.fireArmEvents.onShoot?.Invoke();
        character._context.clipSize--;

        CharacterUIContext characterUIContext = new CharacterUIContext()
        {
            clipSize = character._context.clipSize,
            maxClipSize = character._context.maxClipSize,
        };
        character._context.UIController.NotifyObservers(CharacterUIElement.ShootUI, characterUIContext);
    }

    public void ExitState(StateMachine<HandgunState, HandgunContext> character)
    {
        character._context.isTriggerPressed = false;
        character._context.animator.CrossFade("Idle", 0f);
        _rechamberingTime = 0.2f;
    }

    public void UpdateState(StateMachine<HandgunState, HandgunContext> character)
    {
        _rechamberingTime -= Time.deltaTime;

        if (_rechamberingTime < 0f)
        {
            character.ChangeState(HandgunState.Ready);
        }
    }
}
