using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Assertions;

public class ZombieBrain : ActorBrainBase, IZombie
{
    private ZombieBehavior _behavior;
    private Animator _animator;
    private NetworkedZombieController _zombieNet;

    public override Transform victimHook => _behavior.victimHook;
    public override bool isPreparing
    {
        get
        {
            // Remote copies have their FSM disabled — the grab/prepare flag
            // arrives via the server-write NetworkVariable instead.
            if (_zombieNet != null && _zombieNet.SimulatesRemotely)
            {
                return _zombieNet.MirroredIsPreparing;
            }
            return _behavior != null && _behavior._context != null && _behavior._context.isPreparing;
        }
    }
    public bool isBiting => _behavior != null && _behavior._context != null && _behavior._context.isBiting;
    public bool isHandAttacking => _behavior != null && _behavior._context != null && _behavior._context.isHandAttacking;

    // Default Fallback Stats (used if ZombieData is not assigned on ZombieBehavior)
    private const float DefaultMaxHitPoints = 100f;
    private const float DefaultBiteDamage = 30f;
    private const float DefaultCorpseDestroyDelay = 10f;

    public float maxHitPoints => _behavior != null && _behavior.zombieData != null ? _behavior.zombieData.maxHitPoints : DefaultMaxHitPoints;
    public ZombieData zombieData => _behavior != null ? _behavior.zombieData : null;
    public float biteDamage => _behavior != null && _behavior.zombieData != null ? _behavior.zombieData.biteDamage : DefaultBiteDamage;
    public float corpseDestroyDelay => _behavior != null && _behavior.zombieData != null ? _behavior.zombieData.corpseDestroyDelay : DefaultCorpseDestroyDelay;

    [SerializeField] private bool _debug = false;

    private void Awake()
    {
        _behavior = GetComponent<ZombieBehavior>();
        _animator = GetComponent<Animator>();
        _zombieNet = GetComponent<NetworkedZombieController>();

        Assert.IsNotNull(_behavior, $"{gameObject.name} needs a ZombieBehavior attached to it");
        Assert.IsNotNull(_animator, $"{gameObject.name} needs an Animator attached to it");

        _hitPoints = maxHitPoints;

        // Hand the entity-specific death routine to the shared Dead state via the
        // base's onDeath hook.
        Context = _behavior._context;
        SetupDeathHook();
    }

    public void StopBiting()
    {
        _behavior.SetIsBiting(false);
    }

    public override void OnExternalInteraction(IInteractable target)
    {
        // Ignore duplicate interactions while an attack is already in progress so
        // the animation triggers are not re-fired (which would replay the attack).
        if (_behavior != null && (_behavior._context.isBiting || _behavior._context.isHandAttacking))
        {
            return;
        }

        // Victim is pinned by another zombie's bite grab: the hand trigger still
        // reached it, so answer with the standing right-hand swing instead of a
        // (visually empty) bite grab. A pin held by this zombie itself is its
        // own bite in progress and proceeds normally.
        if (!ZombieBehavior.CanVictimBeBitten(target, this))
        {
            _behavior.StartHandAttack(target);
            transform.LookAt(target.position);
            return;
        }

        _behavior.SetInteractable(target);
        _behavior.SetIsBiting(true);
        _behavior._context.SetAnimatorTrigger(AnimatorUtils.BiteHash);
        transform.LookAt(target.position);

        // Gold-standard, attacker-driven damage: the zombie applies its own bite
        // damage to the victim. The isBiting guard prevents re-firing per bite.
        if (target is IDamageable damageable)
        {
            using (CombatLog.BeginSource("ZombieBite"))
            {
                damageable.TakeDamage(biteDamage);
            }
        }
    }

    protected override void OnRagdollEnabled()
    {
        base.OnRagdollEnabled();

        if (_debug)
        {
            Debug.Log($"[{gameObject.name}] RAGDOLL activated!");
        }

        DropAmmo();

        Destroy(GetComponent<ZombieBehavior>());
        foreach (ZombieHand hand in GetComponentsInChildren<ZombieHand>())
        {
            Destroy(hand);
        }

        Destroy(gameObject, corpseDestroyDelay);
    }

    // Ammunition economy: dead zombies drop an ammo pickup so the finite
    // reserve on the player's weapon is renewable. Data-driven via ZombieData;
    // a null prefab means the archetype drops nothing.
    private void DropAmmo()
    {
        GameObject dropPrefab = zombieData != null ? zombieData.ammoDropPrefab : null;
        if (dropPrefab == null) return;

        Instantiate(dropPrefab, transform.position, Quaternion.identity);
    }
}

public interface IZombie
{
    public void StopBiting();
}
