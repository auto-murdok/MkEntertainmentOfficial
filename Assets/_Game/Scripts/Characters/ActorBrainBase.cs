using System;
using UnityEngine;
using UnityEngine.AI;

// Shared brain lifecycle for any actor driven by an ActorBlackboard-backed FSM
// (Player and Zombie). Consolidates the duplication that lived in every brain:
// InteractableRegistry registration, ragdoll enable/disable, the context.onDeath
// hook, and the HP-reduction -> isAlive death signal. Entity-specific concerns
// (victimHook, bite interaction, health source, corpse teardown) stay in the
// derived class via the abstract/virtual hooks below.
public abstract class ActorBrainBase : MonoBehaviour, IInteractable, IDamageable
{
    protected ActorBlackboard Context;

    // SO-architecture: every actor prefab references the shared registry asset,
    // so entities never reach out to a scene object or static singleton.
    [SerializeField] private InteractableRegistry _registry;
    public InteractableRegistry registry => _registry;

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

    // Time (Time.time) of the last accepted hit. Drives delayed health
    // regeneration: every new hit restarts the regen delay.
    protected float _lastDamageTime = float.NegativeInfinity;

    // Raised once when hit points reach zero (true death — the manual-ragdoll
    // debug action does NOT raise it). The game-flow layer listens on the
    // player instance; entity code never subscribes to its own death.
    public event Action Died;

    protected void ApplyDamage(float amount)
    {
        // Dead actors ignore damage: bullets and swings hitting ragdolls must
        // not decrement HP or spam the combat log.
        if (Context == null || !Context.isAlive)
        {
            return;
        }

        _hitPoints = Mathf.Max(0f, _hitPoints - amount);
        _lastDamageTime = Time.time;
        CombatLog.ReportDamage(amount, _hitPoints, gameObject);
        if (_hitPoints <= 0f && Context.isAlive)
        {
            Context.isAlive = false;
            Died?.Invoke();
        }
    }

    // IDamageable: attacker-supplied damage, centralized through ApplyDamage.
    public void TakeDamage(float amount) => ApplyDamage(amount);

    // Passive health regeneration — call once per Update from the derived brain
    // with the entity's data config. Heals at `rate` HP/second once `regenDelay`
    // seconds have passed since the last hit, capped at max. Dead actors and
    // full-health actors never regenerate.
    protected void RegenerateHitPoints(float rate, float maxHitPoints, float regenDelay)
    {
        if (rate <= 0f || _hitPoints >= maxHitPoints) return;
        if (Context == null || !Context.isAlive) return;
        if (Time.time - _lastDamageTime < regenDelay) return;

        _hitPoints = Mathf.Min(maxHitPoints, _hitPoints + rate * Time.deltaTime);
    }

    // --- Ragdoll / death lifecycle ---
    protected void SetupDeathHook() => Context.onDeath = HandleDeath;
    private void HandleDeath() => RagdollUtils.EnableRagdoll(transform, OnRagdollEnabled);

    protected virtual void OnRagdollEnabled()
    {
        _registry?.Unregister(this);
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
        if (_registry != null)
        {
            _registry.Register(this);
        }
        else
        {
            Debug.LogError($"[{name}] No InteractableRegistry asset assigned — actor cannot be bitten/targeted. " +
                           "Assign the shared InteractableRegistry asset in the actor prefab (inspector field on the brain).");
        }
        OnActorStart();
    }

    private void OnDestroy()
    {
        _registry?.Unregister(this);
        OnActorDestroy();
    }
}
