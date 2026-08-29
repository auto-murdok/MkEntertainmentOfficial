using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HandgunReadyState : State<HandgunState, HandgunContext>
{
    public void CheckTransitions(StateMachine<HandgunState, HandgunContext> character)
    {
        if (character._context.isTriggerPressed)
        {
            if (character._context.clipSize > 0)
            {
                character.ChangeState(HandgunState.Shooting);
            }
            else if (character._context.reserveAmmo > 0)
            {
                character.ChangeState(HandgunState.Reloading);
            }
            // else: completely out of ammo — stay Ready (dry weapon). A later
            // pickup (AddReserveAmmo) re-arms the auto-reload on the next check.
        }
        else if (character._context.isReloading)
        {
            character.ChangeState(HandgunState.Reloading);
        }
    }

    public void EnterState(StateMachine<HandgunState, HandgunContext> character)
    {
    }

    public void ExitState(StateMachine<HandgunState, HandgunContext> character)
    {
    }

    public void UpdateState(StateMachine<HandgunState, HandgunContext> character)
    {
    }
}
