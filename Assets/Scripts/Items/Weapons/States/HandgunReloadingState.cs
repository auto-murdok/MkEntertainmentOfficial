using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class HandgunReloadingState : State<HandgunState, HandgunContext>
{
    private float _reloadingTime = 2f;

    public void CheckTransitions(StateMachine<HandgunState, HandgunContext> character)
    {
        // do nothing
    }

    public void EnterState(StateMachine<HandgunState, HandgunContext> character)
    {
        // do nothing
        character._context.isTriggerPressed = false;
        character._context.isReloading = true;
        Handgun handgun = (Handgun)character;
        handgun.fireArmEvents.onReloadStarted?.Invoke();
    }

    public void ExitState(StateMachine<HandgunState, HandgunContext> character)
    {
        // do nothing
        character._context.clipSize = character._context.maxClipSize;
        character._context.isReloading = false;
        _reloadingTime = 2f;

        CharacterUIContext characterUIContext = new CharacterUIContext()
        {
            clipSize = character._context.clipSize,
            maxClipSize = character._context.maxClipSize,
        };
        character._context.UIController.NotifyObservers(CharacterUIElement.ShootUI, characterUIContext);

        Handgun handgun = (Handgun)character;
        handgun.fireArmEvents.onReloadFinished?.Invoke();
    }

    public void UpdateState(StateMachine<HandgunState, HandgunContext> character)
    {
        _reloadingTime -= Time.deltaTime;

        if (_reloadingTime < 0f)
        {
            character.ChangeState(HandgunState.Ready);
        }
    }
}
