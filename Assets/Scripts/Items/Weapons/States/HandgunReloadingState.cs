using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class HandgunReloadingState : State<HandgunState, HandgunContext>
{
    // Time spent in the reloading state before returning to ready.
    private const float ReloadDuration = 2f;

    private float _reloadingTime = ReloadDuration;

    public void CheckTransitions(StateMachine<HandgunState, HandgunContext> character)
    {
        // do nothing
    }

    public void EnterState(StateMachine<HandgunState, HandgunContext> character)
    {
        character._context.isTriggerPressed = false;
        character._context.isReloading = true;
        Handgun handgun = (Handgun)character;
        handgun.fireArmEvents.onReloadStarted?.Invoke();
    }

    public void ExitState(StateMachine<HandgunState, HandgunContext> character)
    {
        character._context.clipSize = character._context.maxClipSize;
        character._context.isReloading = false;
        _reloadingTime = ReloadDuration;

        character._context.UIController.NotifyObservers(
            CharacterUIElement.ShootUI,
            CreateShootUIContext(character._context));

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

    /// <summary>
    /// Builds the UI context reflecting the current clip state.
    /// </summary>
    private static CharacterUIContext CreateShootUIContext(HandgunContext context)
    {
        return new CharacterUIContext()
        {
            clipSize = context.clipSize,
            maxClipSize = context.maxClipSize,
        };
    }
}
