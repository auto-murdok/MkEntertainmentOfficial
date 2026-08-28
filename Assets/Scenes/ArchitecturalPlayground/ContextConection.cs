using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

public class ContextConection : MonoBehaviour
{
    private void OnValidate() {
        Assert.IsNotNull(GetComponent<IConnectionClient>(), $"{gameObject.name} ContextConection does not implement IConnectionClient");
    }

    private void PrintToConsole() {
        // Debug.Log($"Connected to store: {gameObject.name}");
    }

    public void Dispatch(ConnectedClientId targetId) {
        BaseStore.Instance.DispatchWithInitiator(this, targetId);
    }

    private void OnEnable() {
        BaseStore.OnStoreUpdated += PrintToConsole;
    }

    private void OnDisable() {
        BaseStore.OnStoreUpdated -= PrintToConsole;
    }
}

public interface IConnectionClient {
    public ConnectedClientId GetId();
    public void OnDispatch(IConnectionClient executioner);
}

public enum ConnectedClientId {
    Player,
    PrimaryWeapon
}

