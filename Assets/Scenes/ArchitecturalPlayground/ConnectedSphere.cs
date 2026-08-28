using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ConnectedSphere : ContextConection, IConnectionClient
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
        if (Input.GetMouseButtonDown(0))
        {
            Dispatch(ConnectedClientId.PrimaryWeapon);
        }
    }
}
