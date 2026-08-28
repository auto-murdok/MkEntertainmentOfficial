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
            else
            {
                character.ChangeState(HandgunState.Reloading);
            }
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
