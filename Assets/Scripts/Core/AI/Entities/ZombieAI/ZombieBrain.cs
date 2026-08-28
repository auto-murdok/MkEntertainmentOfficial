using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Assertions;

public class ZombieBrain : MonoBehaviour, IZombie, IInteractable, IDamageable
{
    private ZombieBehavior _behavior;
    private NavMeshAgent _agent;
    private Animator _animator;

    public int id => gameObject.GetInstanceID();
    public Vector3 position => transform.position;
    public Transform victimHook => _behavior.victimHook;
    public bool isPreparing => _behavior != null && _behavior._context != null && _behavior._context.isPreparing;
    public bool isBitting => _behavior != null && _behavior._context != null && _behavior._context.isBitting;

    // Default Fallback Stats (used if ZombieData is not assigned on ZombieBehavior)
    private const float DefaultMaxHitPoints = 100f;
    private const float DefaultBiteDamage = 30f;
    private const float DefaultCorpseDestroyDelay = 5f;

    private float _hitPoints;
    public float remainingHitPoints => _hitPoints;

    public float maxHitPoints => _behavior != null && _behavior.zombieData != null ? _behavior.zombieData.maxHitPoints : DefaultMaxHitPoints;
    public float biteDamage => _behavior != null && _behavior.zombieData != null ? _behavior.zombieData.biteDamage : DefaultBiteDamage;
    public float corpseDestroyDelay => _behavior != null && _behavior.zombieData != null ? _behavior.zombieData.corpseDestroyDelay : DefaultCorpseDestroyDelay;

    private void Awake()
    {
        _behavior = GetComponent<ZombieBehavior>();
        _agent = GetComponent<NavMeshAgent>();
        _animator = GetComponent<Animator>();

        Assert.IsNotNull(_behavior, $"{gameObject.name} needs a ZombieBehavior attached to it");
        Assert.IsNotNull(_animator, $"{gameObject.name} needs an Animator attached to it");
        Assert.IsNotNull(_agent, $"{gameObject.name} needs a NavMeshAgent attached to it");

        _hitPoints = maxHitPoints;
    }

    private void Start()
    {
        RagdollUtils.DisableRagdoll(transform);
        if (InteractableManager.Instance != null)
        {
            InteractableManager.Instance.AddInteractable(this);
        }
    }

    private void OnDestroy()
    {
        if (InteractableManager.Instance != null)
        {
            InteractableManager.Instance.RemoveInteractable(this);
        }
    }

    public void StopBitting()
    {
        _behavior.SetIsBitting(false);
    }

    public void OnExternalInteraction(IInteractable target)
    {
        _behavior.SetInteractable(target);
        _behavior.SetIsBitting(true);
        _animator.SetTrigger(AnimatorUtils.BiteHash);
        transform.LookAt(target.position);
    }

    public void TakeDamage()
    {
        _hitPoints = biteDamage > _hitPoints ? 0f : _hitPoints - biteDamage;

        Debug.LogWarning($"[{gameObject.name}] Remaining health = {_hitPoints}");

        if (_hitPoints <= 0f)
        {
            RagdollUtils.EnableRagdoll(transform, OnEnableRagdoll);
        }
    }

    private void OnEnableRagdoll()
    {
        Debug.LogWarning($"[{gameObject.name}] RAGDOLL activated!");

        if (InteractableManager.Instance != null)
        {
            InteractableManager.Instance.RemoveInteractable(this);
        }

        Destroy(GetComponent<NavMeshAgent>());
        Destroy(GetComponent<Animator>());
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
