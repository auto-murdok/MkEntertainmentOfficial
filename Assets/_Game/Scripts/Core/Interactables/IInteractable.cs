using UnityEngine;

public interface IInteractable
{
    public int id { get; }
    public Vector3 position { get; }
    public Transform victimHook { get; }
    public void OnExternalInteraction(IInteractable interactable);
    public bool isPreparing { get; }
}
