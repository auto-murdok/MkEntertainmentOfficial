using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class InputHandler : Subject<InputHandlerActions, InputValue>
{
    private void OnValidate()
    {
        // do nothing
    }

    public void OnMove(InputValue value)
    {
        NotifyObservers(InputHandlerActions.Move, value);
    }

    public void OnRun(InputValue value)
    {
        NotifyObservers(InputHandlerActions.ToogleRun, value);
    }

    public void OnLook(InputValue value)
    {
        NotifyObservers(InputHandlerActions.Look, value);
    }

    public void OnAim(InputValue value)
    {
        NotifyObservers(InputHandlerActions.Aim, value);
    }

    public void OnShoot(InputValue value)
    {
        NotifyObservers(InputHandlerActions.Shoot, value);
    }

    public void OnReload(InputValue value)
    {
        NotifyObservers(InputHandlerActions.Reload, value);
    }

    public void OnManualEnableRagdoll(InputValue value)
    {
        NotifyObservers(InputHandlerActions.ManualEnableRagdoll, value);
    }
}
