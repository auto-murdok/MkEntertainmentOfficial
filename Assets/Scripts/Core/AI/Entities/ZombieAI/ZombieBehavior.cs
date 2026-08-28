using System;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Assertions;

public class ZombieBehavior : StateMachine<ZombieStates, ZombieContext>
{
    [Header("Detection settings")]
    [SerializeField] private Transform _visionHook;
    [SerializeField] private Transform _victimHook;
    [SerializeField] private LayerMask _detectionLayerMask;
    [SerializeField] private LayerMask _ignoreLayerMask;
    [SerializeField] private float _detectionMaxDistance = 5f;
    public Transform victimHook => _victimHook;

    private void Awake()
    {
        states[ZombieStates.Idle] = new ZombieIdle();
        states[ZombieStates.Chasing] = new ZombieChasing();
        states[ZombieStates.Prepare] = new ZombiePrepareForSyncedAttack();
        states[ZombieStates.Bitting] = new ZombieBitting();

        // context
        _context.agent = GetComponent<NavMeshAgent>();
        _context.animator = GetComponent<Animator>();

        Assert.IsNotNull(_context.animator, $"{gameObject.name} needs an Animator attached to it");
        Assert.IsNotNull(_context.agent, $"{gameObject.name} needs a NavMeshAgent attached to it");
        Assert.IsNotNull(_visionHook, $"{gameObject.name} needs a visionHook attached to it");

        OnCommonUpdate += RelieveMovement;
        OnCommonUpdate += SearchForSurvivors;
    }

    private void SearchForSurvivors(ZombieStates currentState)
    {
        ISurvivor survivor = AIDetectionUtils.DetectViaLineOfSight<ISurvivor>(
            _visionHook,
            _detectionMaxDistance,
            _detectionLayerMask,
            _ignoreLayerMask,
            100,
            180
        );
        SetTarget(survivor);
    }

    private void RelieveMovement(ZombieStates currentState)
    {
        if (currentState != ZombieStates.Chasing)
        {
            ZombieAnimatorUtils.DisableRootMotionMovement(_context.animator, 0.25f);
        }
    }

    public void SetTarget(ISurvivor survivor)
    {
        _context.target = survivor;
    }

    public void SetIsBitting(bool isBitting)
    {
        _context.isBitting = isBitting;
    }

    public void SetInteractable(IInteractable interactable)
    {
        _context.interactable = interactable;
    }
}
