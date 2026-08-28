using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ObjectsStore : BaseStore
{
    [SerializeField] private int _currentConnections;

    private void Update()
    {
        _currentConnections = GetConnections().Count;
    }
}
