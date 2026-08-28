using UnityEngine;

public class ZombieHand : MonoBehaviour
{
    private ZombieBrain _zombieBrain;
    private Collider _handCollider;

    private void Awake()
    {
        _zombieBrain = GetComponentInParent<ZombieBrain>();
        _handCollider = GetComponent<Collider>();
        if (_handCollider == null)
        {
            _handCollider = GetComponentInParent<Collider>();
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (_zombieBrain == null || _zombieBrain.isBitting) return;

        IInteractable survivor = other.GetComponentInParent<IInteractable>();
        if (survivor != null && InteractableManager.Instance != null)
        {
            InteractableManager.Instance.Interact(survivor.id, _zombieBrain.id);
            Disable();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        OnTriggerStay(other);
    }

    public void Enable()
    {
        if (_handCollider != null)
        {
            _handCollider.enabled = true;
        }
    }

    public void Disable()
    {
        if (_handCollider != null)
        {
            _handCollider.enabled = false;
        }
    }
}
