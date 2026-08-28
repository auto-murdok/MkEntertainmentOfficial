using System;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Assertions;

public class ZombieBehavior : StateMachine<ZombieStates, ZombieContext>, ICommandable
{
    [Header("Configuration")]
    [SerializeField] private ZombieData _zombieData;

    [Header("Detection Fallbacks (used if ZombieData is not assigned)")]
    [SerializeField] private Transform _visionHook;
    [SerializeField] private Transform _victimHook;
    [SerializeField] private LayerMask _detectionLayerMask;
    [SerializeField] private LayerMask _ignoreLayerMask;
    [SerializeField] private float _detectionMaxDistance = 12f;

    public const float DefaultBiteRange = 1.2f;
    private const int DefaultMinDetectionAngle = 60;
    private const int DefaultMaxDetectionAngle = 180;

    private ZombieSockets _sockets;

    public ZombieData zombieData => _zombieData;
    public Transform visionHook => _sockets != null ? _sockets.visionHook : (_visionHook != null ? _visionHook : transform);
    public Transform victimHook => _sockets != null ? _sockets.victimHook : (_victimHook != null ? _victimHook : transform);
    public float biteRange => _zombieData != null ? _zombieData.biteRange : DefaultBiteRange;

    private const float VisionScanInterval = 0.15f;
    private float _visionScanTimer;

    private void Awake()
    {
        _visionScanTimer = UnityEngine.Random.Range(0f, VisionScanInterval);

        states[ZombieStates.Idle] = new ZombieIdle();
        states[ZombieStates.Chasing] = new ZombieChasing();
        states[ZombieStates.Bitting] = new ZombieBitting();
        states[ZombieStates.CommandedMove] = new ZombieCommandedMoveState();
        states[ZombieStates.Dead] = new ActorDeadState<ZombieStates, ZombieContext>();

        // Components & Sockets
        _context.agent = GetComponent<NavMeshAgent>();
        _context.animator = GetComponent<Animator>();
        _context.brain = GetComponent<ZombieBrain>();
        _context.hands = GetComponentsInChildren<ZombieHand>(true);
        _sockets = GetComponentInChildren<ZombieSockets>();

        if (_sockets == null)
        {
            _sockets = gameObject.AddComponent<ZombieSockets>();
        }

        _context.sockets = _sockets;
        _context.visionHook = visionHook;

        ApplyZombieData(_zombieData);

        Assert.IsNotNull(_context.animator, $"{gameObject.name} needs an Animator attached to it");
        Assert.IsNotNull(_context.agent, $"{gameObject.name} needs a NavMeshAgent attached to it");

        OnCommonUpdate += RelieveMovement;
        OnCommonUpdate += SearchForSurvivors;

        // Global guard: force transition to the reusable Dead state on death.
        CheckGlobalTransition = (current) => _context.isAlive ? current : ZombieStates.Dead;
    }

    public void SetZombieData(ZombieData data)
    {
        _zombieData = data;
        ApplyZombieData(data);
    }

    private void ApplyZombieData(ZombieData data)
    {
        _context.data = data;

        if (data != null)
        {
            _context.detectionLayerMask = data.detectionLayerMask.value != 0 ? data.detectionLayerMask : _detectionLayerMask;
            _context.ignoreLayerMask = data.ignoreLayerMask;
            _context.biteDuration = data.biteDuration;

            if (_context.agent != null)
            {
                _context.agent.radius = data.defaultAgentRadius;
            }

            if (data.animatorOverride != null && _context.animator != null)
            {
                _context.animator.runtimeAnimatorController = data.animatorOverride;
            }
        }
        else
        {
            _context.detectionLayerMask = _detectionLayerMask;
            _context.ignoreLayerMask = _ignoreLayerMask;
        }

        // Ensure default detection mask points to LocalPlayer if not explicitly assigned
        if (_context.detectionLayerMask.value == 0)
        {
            int playerLayer = LayerMask.NameToLayer(LayerUtils.LocalPlayerLayerName);
            if (playerLayer >= 0)
            {
                _context.detectionLayerMask = 1 << playerLayer;
            }
        }
    }

    // Scans for a survivor inside the vision cone and updates the shared target.
    private void SearchForSurvivors(ZombieStates currentState)
    {
        _visionScanTimer -= Time.deltaTime;
        if (_visionScanTimer > 0f) return;
        _visionScanTimer = VisionScanInterval;

        float maxDist = _zombieData != null ? _zombieData.detectionMaxDistance : _detectionMaxDistance;
        int minAngle = _zombieData != null ? _zombieData.minDetectionAngle : DefaultMinDetectionAngle;
        int maxAngle = _zombieData != null ? _zombieData.maxDetectionAngle : DefaultMaxDetectionAngle;
        LayerMask detectMask = _context.detectionLayerMask;
        LayerMask ignoreMask = _context.ignoreLayerMask;

        ISurvivor survivor = AIDetectionUtils.DetectViaLineOfSight<ISurvivor>(
            visionHook,
            maxDist,
            detectMask,
            ignoreMask,
            minAngle,
            maxAngle
        );
        SetTarget(survivor);
    }

    // While not chasing, ease the animator's root motion back to zero.
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

    // ICommandable: lets any locomotion controller (e.g. click-to-move) drive
    // this entity through the shared move-to-target state instead of poking the
    // NavMeshAgent directly.
    public void SetMoveDestination(Vector3 destination)
    {
        _context.moveDestination = destination;
    }

    public void ClearMoveDestination()
    {
        _context.moveDestination = null;
    }

    public bool TryTriggerAttack()
    {
        if (_context.target == null) return false;

        IInteractable interactableTarget = _context.target as IInteractable ?? _context.interactable;
        ZombieBrain brain = _context.brain != null ? _context.brain : GetComponent<ZombieBrain>();

        if (interactableTarget != null && brain != null && InteractableManager.Instance != null)
        {
            InteractableManager.Instance.Interact(interactableTarget.id, brain.id);
            return true;
        }

        return false;
    }
}
