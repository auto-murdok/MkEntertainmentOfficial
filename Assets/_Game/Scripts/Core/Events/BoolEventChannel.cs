using System;
using UnityEngine;

/// <summary>
/// Typed ScriptableObject event channel carrying a bool payload (e.g. the
/// game-flow "spawning enabled" toggle broadcast to every spawning system).
/// Producers call Raise(value), consumers subscribe to OnRaised — neither side
/// knows the other.
/// </summary>
[CreateAssetMenu(fileName = "BoolEventChannel", menuName = "Game/Events/Bool Event Channel")]
public class BoolEventChannel : ScriptableObject
{
    public event Action<bool> OnRaised;

    public void Raise(bool value) => OnRaised?.Invoke(value);
}
