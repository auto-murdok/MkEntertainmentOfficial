using System;
using UnityEngine;

/// <summary>
/// Typed ScriptableObject event channel (Unity "SO event channel" pattern):
/// producers call Raise, consumers subscribe to OnRaised — neither side knows
/// the other, and designers can rewire flows by swapping assets.
/// Carries no payload.
/// </summary>
[CreateAssetMenu(fileName = "VoidEventChannel", menuName = "Game/Events/Void Event Channel")]
public class VoidEventChannel : ScriptableObject
{
    public event Action OnRaised;

    public void Raise() => OnRaised?.Invoke();
}
