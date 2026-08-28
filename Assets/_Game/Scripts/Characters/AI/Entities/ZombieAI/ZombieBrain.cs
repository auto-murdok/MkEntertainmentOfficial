using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Assertions;

public class ZombieBrain : ActorBrainBase, IZombie, IDamageable
{
    private ZombieBehavior _behavior;
    private Animator _animator;

    public override Transform victimHook => _behavior.victimHook;
    public override bool isPreparing => _behavior != null && _behavior._context != null && _behavior._context.isPreparing;
    public bool isBitting => _behavior != null && _behavior._context != null && _behavior._context.isBitting;

    // Default Fallback Stats (used if ZombieData is not assigned on ZombieBehavior)
    private const float DefaultMaxHitPoints = 100f;
    private const float DefaultBiteDamage = 30f;
    private const float DefaultCorpseDestroyDelay = 5f;

    public float maxHitPoints => _behavior != null && _behavior.zombieData != null ? _behavior.zombieData.maxHitPoints : DefaultMaxHitPoints;
    public float biteDamage => _behavior != null && _behavior.zombieData != null ? _behavior.zombieData.biteDamage : DefaultBiteDamage;
    public float corpseDestroyDelay => _behavior != null && _behavior.zombieData != null ? _behavior.zombieData.corpseDestroyDelay : DefaultCorpseDestroyDelay;

    [SerializeField] private bool _debug = false;

    private void Awake()
    {
        _behavior = GetComponent<ZombieBehavior>();
        _animator = GetComponent<Animator>();

        Assert.IsNotNull(_behavior, $"{gameObject.name} needs a ZombieBehavior attached to it");
        Assert.IsNotNull(_animator, $"{gameObject.name} needs an Animator attached to it");

        _hitPoints = maxHitPoints;

        // Hand the entity-specific death routine to the shared Dead state via the
        // base's onDeath hook.
        Context = _behavior._context;
        SetupDeathHook();
    }

    public void StopBitting()
    {
        _behavior.SetIsBitting(false);
    }

    public override void OnExternalInteraction(IInteractable target)
    {
        // Ignore duplicate interactions while a bite is already in progress so the
        // Bite trigger is not re-fired (which would replay the bite animation).
        if (_behavior != null && _behavior._context.isBitting)
        {
            return;
        }

        _behavior.SetInteractable(target);
        _behavior.SetIsBitting(true);
        _animator.SetTrigger(AnimatorUtils.BiteHash);
        transform.LookAt(target.position);
    }

    public void TakeDamage()
    {
        ApplyDamage(biteDamage);

        if (_debug)
        {
            Debug.Log($"[{gameObject.name}] Remaining health = {_hitPoints}");
        }
    }

    protected override void OnRagdollEnabled()
    {
        base.OnRagdollEnabled();

        if (_debug)
        {
            Debug.Log($"[{gameObject.name}] RAGDOLL activated!");
        }

        Destroy(GetComponent<ZombieBehavior>());
        foreach (ZombieHand hand in GetComponentsInChildren<ZombieHand>())
        {
            Destroy(hand);
        }

        Destroy(gameObject, corpseDestroyDelay);
    }
}

public interface IZombie
{
    public void StopBitting();
}
