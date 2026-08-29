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
        if (_reloadingTime < 0f)
        {
            character.ChangeState(HandgunState.Ready);
        }
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
        // Refill from the reserve pool: take as much as the clip can hold and
        // the reserve can give (int.MaxValue reserve behaves as infinite).
        int missing = character._context.maxClipSize - character._context.clipSize;
        int taken = Mathf.Min(missing, character._context.reserveAmmo);
        character._context.clipSize += taken;
        character._context.reserveAmmo -= taken;
        character._context.isReloading = false;
        _reloadingTime = ReloadDuration;

        if (character._context.UIController != null)
        {
            character._context.UIController.NotifyObservers(
                CharacterUIElement.ShootUI,
                CharacterUIContext.CreateShootUI(character._context.clipSize, character._context.maxClipSize));
        }

        Handgun handgun = (Handgun)character;
        handgun.fireArmEvents.onReloadFinished?.Invoke();
    }

    public void UpdateState(StateMachine<HandgunState, HandgunContext> character)
    {
        _reloadingTime -= Time.deltaTime;
    }
}
