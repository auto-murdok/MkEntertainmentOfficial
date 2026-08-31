using UnityEngine;

/// <summary>Legacy alias — kept for asset compatibility. New code can use <see cref="EventChannel{T}"/> with T=bool.</summary>
[CreateAssetMenu(fileName = "BoolEventChannel", menuName = "Game/Events/Bool Event Channel")]
public class BoolEventChannel : EventChannel<bool> { }
