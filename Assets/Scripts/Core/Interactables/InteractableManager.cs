using System.Collections;
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
        _interactables.Add(interactable.id, interactable);
    }

    public void Interact(IInteractable first, IInteractable second)
    {
        first.OnExternalInteraction(second);
        second.OnExternalInteraction(first);
    }

    public void Interact(int firstId, int secondId)
    {
        IInteractable first = _interactables[firstId];
        IInteractable second = _interactables[secondId];
        if (first != null && second != null)
        {
            first.OnExternalInteraction(second);
            second.OnExternalInteraction(first);
        }
    }
}
