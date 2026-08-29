using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestWeapon : ContextConnection, IConnectionClient
{
    [SerializeField] private Transform bulletPoint;

    private void Start()
    {
        BaseStore.Instance.Connect(this);
    }

    public void OnDispatch(IConnectionClient executioner)
    {
        Debug.LogWarning("BANG!");
    }

    public ConnectedClientId GetId()
    {
        return ConnectedClientId.PrimaryWeapon;
    }
}
