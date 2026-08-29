using UnityEngine;
using UnityEngine.AI;

// Shared brain lifecycle for any actor driven by an ActorBlackboard-backed FSM
// (Player and Zombie). Consolidates the duplication that lived in every brain:
// InteractableManager registration, ragdoll enable/disable, the context.onDeath
// hook, and the HP-reduction -> isAlive death signal. Entity-specific concerns
// (victimHook, bite interaction, health source, corpse teardown) stay in the
// derived class via the abstract/virtual hooks below.
public abstract class ActorBrainBase : MonoBehaviour, IInteractable, IDamageable
{
    protected ActorBlackboard Context;

    // --- IInteractable: generic members shared by every actor ---
    public int id => gameObject.GetInstanceID();
    public Vector3 position => transform.position;
    public abstract Transform victimHook { get; }
    public abstract bool isPreparing { get; }
    public abstract void OnExternalInteraction(IInteractable interactable);

    // --- Health helpers ---
    // Death simply raises the shared FSM flag; the reusable Dead state then fires
    // Context.onDeath (wired up via SetupDeathHook) to ragdoll + teardown.
    protected float _hitPoints;
    public float remainingHitPoints => _hitPoints;
    protected void ApplyDamage(float amount)
    {
        _hitPoints = Mathf.Max(0f, _hitPoints - amount);
        if (_hitPoints <= 0f) Context.isAlive = false;
    }

    // IDamageable: attacker-supplied damage, centralized through ApplyDamage.
    public void TakeDamage(float amount) => ApplyDamage(amount);

    // --- Ragdoll / death lifecycle ---
    protected void SetupDeathHook() => Context.onDeath = HandleDeath;
    private void HandleDeath() => RagdollUtils.EnableRagdoll(transform, OnRagdollEnabled);

    protected virtual void OnRagdollEnabled()
    {
        InteractableManager.Instance?.RemoveInteractable(this);
        DestroyActorCore(); // NavMeshAgent + Animator are torn down for every actor
    }

    protected void DestroyActorCore()
    {
        var agent = GetComponent<NavMeshAgent>();
        if (agent != null) Destroy(agent);
        var anim = GetComponent<Animator>();
        if (anim != null) Destroy(anim);
    }

    // Entity-specific extras (layer setup, extra teardown, etc.)
    protected virtual void OnActorStart() { }
    protected virtual void OnActorDestroy() { }

    // virtual so derived brains that define their own Start can (and must) call
    // base.Start() — a plain private Start here would be hidden by any derived
    // Start and silently skip registration (Unity calls only the most-derived
    // magic method in the hierarchy).
    protected virtual void Start()
    {
        RagdollUtils.DisableRagdoll(transform);
        if (InteractableManager.Instance != null)
        {
            InteractableManager.Instance.AddInteractable(this);
        }
        else
        {
            Debug.LogError($"[{name}] No InteractableManager in the scene — actor cannot be bitten/targeted. " +
                           "Add an InteractableManager to the scene (it lives on the PlayerCoreComponents prefab).");
        }
        OnActorStart();
    }

    private void OnDestroy()
    {
        if (InteractableManager.Instance != null)
        {
            InteractableManager.Instance.RemoveInteractable(this);
        }
        OnActorDestroy();
    }
}
