using System.Collections.Generic;
using UnityEngine;

public class InteractableManager : MonoBehaviour
{
    [SerializeField] Dictionary<int, IInteractable> _interactables = new Dictionary<int, IInteractable>();

    public static InteractableManager Instance;

    private void Awake()
    {
        Instance = this;
    }

    public void AddInteractable(IInteractable interactable)
    {
        if (interactable != null)
        {
            _interactables[interactable.id] = interactable;
        }
    }

    public void RemoveInteractable(IInteractable interactable)
    {
        if (interactable != null)
        {
            _interactables.Remove(interactable.id);
        }
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
