using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ZombieHand : MonoBehaviour
{
    private ZombieBrain _zombieBrain;
    private Collider _handCollider;

    private void Awake()
    {
        _zombieBrain = GetComponentInParent<ZombieBrain>();
        _handCollider = GetComponentInParent<Collider>();
    }

    private void OnTriggerEnter(Collider other)
    {
        IInteractable survivor = other.GetComponentInParent<IInteractable>();
        if (survivor != null)
        {
            InteractableManager.Instance.Interact(survivor.id, _zombieBrain.id);
            Disable();
        }
    }

    public void Enable()
    {
        _handCollider.enabled = true;
    }

    public void Disable()
    {
        _handCollider.enabled = false;
    }
}
