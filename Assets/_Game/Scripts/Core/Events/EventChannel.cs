using System;
using UnityEngine;

/// <summary>Base void event channel — use for signal-only events.</summary>
public class EventChannel : ScriptableObject
{
    public event Action OnRaised;
    public void Raise() => OnRaised?.Invoke();
}

/// <summary>Generic typed event channel — avoids per-type copy-paste (BoolEventChannel etc.).</summary>
public class EventChannel<T> : ScriptableObject
{
    public event Action<T> OnRaised;
    public void Raise(T value) => OnRaised?.Invoke(value);
}
