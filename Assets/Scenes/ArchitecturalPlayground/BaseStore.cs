using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public abstract class BaseStore : MonoBehaviour
{
    public static event Action OnStoreUpdated;
    public static BaseStore Instance;
    public async static Task Initialize() {
        while (Instance == null) {
            await Task.Yield();
        }
    }

    private Dictionary<ConnectedClientId, IConnectionClient> _contextConections = new Dictionary<ConnectedClientId, IConnectionClient>();

    private void Awake() {
        if (Instance == null) {
            Instance = this;
        }
    }

    public Dictionary<ConnectedClientId, IConnectionClient> GetConnections() {
        return _contextConections;
    }

    public void Connect(IConnectionClient conection) {
        _contextConections[conection.GetId()] = conection;
        OnStoreUpdated?.Invoke();
    }

    public void DispatchWithInitiator(ContextConection from, ConnectedClientId target) {
        IConnectionClient executioner = from.GetComponent<IConnectionClient>();
        _contextConections[target]?.OnDispatch(executioner);
    }
}
