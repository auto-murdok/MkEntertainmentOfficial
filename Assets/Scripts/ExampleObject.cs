using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ExampleObject : MonoBehaviour, IObserver<CubeActions, int>
{
    [SerializeField] Subject<CubeActions, int> _playerSubject;

    public void OnNotify(CubeActions cubeAction, int value)
    {
        Debug.Log("Notified: " + cubeAction);
    }

    // when gameobject is enabled
    private void OnEnable() {
        // add itself to the subject's list of observers
        _playerSubject.AddObserver(this);
    }

    private void OnDisable() {
        _playerSubject.RemoveObserver(this);
    }
}
