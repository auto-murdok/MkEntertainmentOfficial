using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ConnectedSphere : ContextConnection, IConnectionClient
{
    [SerializeField] private string _objective;

    private void Start()
    {
        BaseStore.Instance.Connect(this);
    }

    public ConnectedClientId GetId()
    {
        return ConnectedClientId.Player;
    }

    public void OnDispatch(IConnectionClient executioner)
    {
        // do nothing
    }

    private void Update()
    {
        // Left mouse button triggers a dispatch to the primary weapon.
        if (Input.GetMouseButtonDown(0))
        {
            Dispatch(ConnectedClientId.PrimaryWeapon);
        }
    }
}
