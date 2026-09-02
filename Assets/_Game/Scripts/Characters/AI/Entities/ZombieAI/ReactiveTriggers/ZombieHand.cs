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
        if (_zombieBrain == null || _zombieBrain.isBiting) return;

        // Server-authoritative: in a networked session, only the host evaluates hand/bite triggers.
        var networkManager = Unity.Netcode.NetworkManager.Singleton;
        if (networkManager != null && networkManager.IsListening && !networkManager.IsServer)
        {
            return;
        }

        ISurvivor survivor = other.GetComponentInParent<ISurvivor>();
        InteractableRegistry registry = _zombieBrain != null ? _zombieBrain.registry : null;
        if (survivor is IInteractable interactableSurvivor && interactableSurvivor.id != _zombieBrain.id && registry != null)
        {
            registry.Interact(interactableSurvivor.id, _zombieBrain.id);
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
