using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public abstract class BaseStore : MonoBehaviour
{
    public static event Action OnStoreUpdated;
    public static BaseStore Instance;

    public async static Task Initialize()
    {
        // Wait until a concrete store has registered itself as the singleton instance.
        while (Instance == null)
        {
            await Task.Yield();
        }
    }

    private Dictionary<ConnectedClientId, IConnectionClient> _connections = new Dictionary<ConnectedClientId, IConnectionClient>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
    }

    public Dictionary<ConnectedClientId, IConnectionClient> GetConnections()
    {
        return _connections;
    }

    public void Connect(IConnectionClient client)
    {
        _connections[client.GetId()] = client;
        OnStoreUpdated?.Invoke();
    }

    public void DispatchWithInitiator(ContextConnection initiator, ConnectedClientId target)
    {
        IConnectionClient executor = initiator.GetComponent<IConnectionClient>();
        _connections[target]?.OnDispatch(executor);
    }
}
