using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestWeapon : ContextConection, IConnectionClient
{
    [SerializeField] private Transform bulletPoint;

    private void Start() {
        BaseStore.Instance.Connect(this);
    }

    public void OnDispatch(IConnectionClient from) {
        Debug.LogWarning("BANG!");
    }

    public ConnectedClientId GetId()
    {
        return ConnectedClientId.PrimaryWeapon;
    }
}
