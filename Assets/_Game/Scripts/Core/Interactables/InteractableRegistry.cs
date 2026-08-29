using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ScriptableObject registry (RuntimeSet pattern) for every interactable actor
/// alive in the scene. Entities hold a reference to the same registry asset in
/// their prefabs, so no scene object or static singleton is ever reached out to
/// — the gold-standard SO-architecture replacement for InteractableManager.
/// Runtime contents are never serialized and are cleared when the asset
/// unloads (scene change / quit), keeping every scene load a clean slate.
/// </summary>
[CreateAssetMenu(fileName = "InteractableRegistry", menuName = "Game/Interactable Registry")]
public class InteractableRegistry : ScriptableObject
{
    private readonly Dictionary<int, IInteractable> _interactables = new Dictionary<int, IInteractable>();

    private void OnEnable()
    {
        // Editor domain reloads can leave stale runtime entries behind.
        _interactables.Clear();
    }

    private void OnDisable()
    {
        // The registry is runtime-only state: a fresh scene must never inherit
        // the previous scene's corpses.
        _interactables.Clear();
    }

    public void Register(IInteractable interactable)
    {
        if (interactable != null)
        {
            _interactables[interactable.id] = interactable;
        }
    }

    public void Unregister(IInteractable interactable)
    {
        if (interactable != null)
        {
            _interactables.Remove(interactable.id);
        }
    }

    public bool TryGet(int id, out IInteractable interactable)
    {
        return _interactables.TryGetValue(id, out interactable);
    }

    public void Interact(IInteractable first, IInteractable second)
    {
        NotifyExternalInteraction(first, second);
    }

    public void Interact(int firstId, int secondId)
    {
        _interactables.TryGetValue(firstId, out IInteractable first);
        _interactables.TryGetValue(secondId, out IInteractable second);

        if (first != null && second != null)
        {
            NotifyExternalInteraction(first, second);
        }
    }

    // Both sides of the interaction are notified so each can react to the other.
    private void NotifyExternalInteraction(IInteractable first, IInteractable second)
    {
        first.OnExternalInteraction(second);
        second.OnExternalInteraction(first);
    }
}
