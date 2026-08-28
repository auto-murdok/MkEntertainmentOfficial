using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Assertions;

public class ZombieBrain : MonoBehaviour, IZombie, IInteractable, IDamageable
{
    private ZombieBehavior _behavior;
    private NavMeshAgent _agent;
    private Animator _animator;
    public int id => 2;
    public Vector3 position => transform.position;
    public Transform victimHook => _behavior.victimHook;
    public bool isPreparing => _behavior._context.isPreparing;

    // IDamageable
    private const float MaxHitPoints = 100f;
    private const float BiteDamage = 30f;
    private const float CorpseDestroyDelay = 5f;

    private float _hitPoints = MaxHitPoints;
    public float remainingHitPoints => _hitPoints;

    private void Awake()
    {
        _behavior = GetComponent<ZombieBehavior>();
        _agent = GetComponent<NavMeshAgent>();
        _animator = GetComponent<Animator>();

        Assert.IsNotNull(_behavior, $"{gameObject.name} needs an ZombieBehavior attached to it");
        Assert.IsNotNull(_animator, $"{gameObject.name} needs an Animator attached to it");
        Assert.IsNotNull(_agent, $"{gameObject.name} needs a NavMeshAgent attached to it");
    }

    private void Start()
    {
        RagdollUtils.DisableRagdoll(transform);
        InteractableManager.Instance.AddInteractable(this);
    }

    public void StopBitting()
    {
        _behavior.SetIsBitting(false);
    }

    public void OnExternalInteraction(IInteractable target)
    {
        _behavior.SetInteractable(target);
        _behavior.SetIsBitting(true);
        _animator.SetTrigger("Bite");
        transform.LookAt(target.position);
    }

    public void TakeDamage()
    {
        _hitPoints = BiteDamage > _hitPoints ? 0f : _hitPoints - BiteDamage;

        Debug.LogWarning($"Remaining health = {_hitPoints}");

        if (_hitPoints == 0f)
        {
            RagdollUtils.EnableRagdoll(transform, OnEnableRagdoll);
        }
    }

    private void OnEnableRagdoll()
    {
        Debug.LogWarning("RAGDOLL!");

        //Destroy(GetComponent<RigBuilder>());
        //Destroy(GetComponent<BoneRenderer>());
        Destroy(GetComponent<NavMeshAgent>());
        Destroy(GetComponent<Animator>());
        Destroy(GetComponent<ZombieBehavior>());
        Destroy(GetComponentInChildren<ZombieHand>());
        Destroy(gameObject, CorpseDestroyDelay);
    }
}

public interface IZombie
{
    public void StopBitting();
}
