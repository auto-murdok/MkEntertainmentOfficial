using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HandgunShootingState : State<HandgunState, HandgunContext>
{
    // Rechambering delay comes from the weapon's configured fire rate
    // (HandgunContext.fireRate), not a hardcoded constant.
    private static readonly int ShootAnimationHash = Animator.StringToHash("fakeGun_shoot");
    private static readonly int IdleAnimationHash = Animator.StringToHash("Idle");

    private float _rechamberingTime;

    public void CheckTransitions(StateMachine<HandgunState, HandgunContext> character)
    {
        if (_rechamberingTime < 0f)
        {
            if (character._context.isReloading)
            {
                character.ChangeState(HandgunState.Reloading);
            }
            else
            {
                character.ChangeState(HandgunState.Ready);
            }
        }
    }

    public void EnterState(StateMachine<HandgunState, HandgunContext> character)
    {
        _rechamberingTime = character._context.fireRate;
        Handgun fireArm = (Handgun)character;
        character._context.animator.CrossFade(ShootAnimationHash, 0f);

        // Recoil (and its onShoot event) only fires when a projectile was
        // actually launched — never on a dry fire.
        bool fired = fireArm.ExecuteActualShoot();
        if (fired)
        {
            fireArm.fireArmEvents.onShoot?.Invoke();
        }

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
        character._context.animator.CrossFade(IdleAnimationHash, 0f);
    }

    public void UpdateState(StateMachine<HandgunState, HandgunContext> character)
    {
        _rechamberingTime -= Time.deltaTime;
    }
}
