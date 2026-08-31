using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.Assertions;
using UnityEngine.InputSystem;

public class CharacterBrain : ActorBrainBase, ISurvivor, IBiteTarget, IObserver<InputHandlerActions, InputValue>
{
    [Header("Connection Settings")]
    public Subject<InputHandlerActions, InputValue> _subject;

    private CharacterLocomotion _locomotion;
    private PlayerInput _playerInput;
    private IPlayerBiteRelay _biteRelay;
    private UnityEngine.InputSystem.InputAction _runAction;
    private UnityEngine.InputSystem.InputAction _aimAction;
    private bool _subscribed;

    [Header("Stats")]
    [SerializeField] private float _maxHitPoints = 100f;

    public float maxHitPoints => _maxHitPoints;

    Vector3 ISurvivor.TargetPosition => transform.position;
    public override Transform victimHook => transform;
    public override bool isPreparing => false;

    // A bite is an exclusive grab: while already pinned by one zombie's bite,
    // other attackers must fall back to non-grab attacks (right-hand swing).
    // Remote copies read the owner-mirrored bite state — the take-bite FSM
    // runs on the victim's owner, not here.
    public bool canBeBitten
    {
        get
        {
            if (_biteRelay != null && !_biteRelay.SimulatesLocally)
            {
                return !_biteRelay.MirroredIsBitten;
            }
            return _locomotion == null || !_locomotion.isBeingAttacked;
        }
    }

    public IInteractable currentBiter
    {
        get
        {
            if (_biteRelay != null && !_biteRelay.SimulatesLocally)
            {
                return _biteRelay.MirroredBiter;
            }
            return _locomotion != null ? _locomotion.currentAttacker : null;
        }
    }

    private void Awake()
    {
        _locomotion = GetComponent<CharacterLocomotion>();
        // Networking seam (null in the single-player arena): remote copies
        // relay bite interactions to the victim's owner instead of simulating.
        _biteRelay = GetComponent<IPlayerBiteRelay>();
        // The subject is wired by the spawner after instantiation, so it may
        // legitimately be null here; validation happens in Start.
        _playerInput = _subject != null ? _subject.GetComponent<PlayerInput>() : null;

        Assert.IsNotNull(_locomotion, "Please attach a component of type CharacterLocomotion");

        _hitPoints = _maxHitPoints;

        // Hand the entity-specific death routine to the shared Dead state via the
        // base's onDeath hook.
        Context = _locomotion._context;
        SetupDeathHook();
    }

    private void Update()
    {
        // Passive health regeneration (PlayerData config via the locomotion
        // context). The shared brain helper guards dead/full-health actors and
        // the since-last-hit delay.
        PlayerData data = _locomotion != null ? _locomotion._context.data : null;
        if (data != null)
        {
            RegenerateHitPoints(data.healthRegenRate, _maxHitPoints, data.healthRegenDelay);
        }

        ReconcileHeldActions();
    }

    // Sprint and aim are HELD states, but they were set from one-shot input
    // events — a missed release event (overlay, focus change, hitch) left the
    // flag latched and the player sprinted forever. Re-evaluate both from the
    // live action state every frame: a disabled PlayerInput (pause/game-over
    // gate) reports not-pressed, so overlays also clear them automatically.
    private void ReconcileHeldActions()
    {
        if (_locomotion == null || _playerInput == null || !_playerInput.enabled)
        {
            return;
        }

        if (_runAction == null)
        {
            _runAction = _playerInput.actions != null ? _playerInput.actions.FindAction("Run", false) : null;
            _aimAction = _playerInput.actions != null ? _playerInput.actions.FindAction("Aim", false) : null;
        }

        if (_runAction != null)
        {
            _locomotion.setIsRunning(_runAction.IsPressed());
        }
        if (_aimAction != null)
        {
            _locomotion.setIsAiming(_aimAction.IsPressed());
        }
    }

    protected override void Start()
    {
        // Register with the InteractableRegistry, disable ragdoll, set layers, etc.
        base.Start();
        LocalPlayerRegistry.Register(this);

        // Remote networked players have no local input subject — their inputs
        // happen on the owning peer and only their transform replicates here.
        Unity.Netcode.NetworkObject networkObject = GetComponent<Unity.Netcode.NetworkObject>();
        bool isRemotePlayer = networkObject != null && networkObject.IsSpawned && !networkObject.IsOwner;
        if (isRemotePlayer)
        {
            return;
        }

        // Re-resolve here: the spawner wires _subject after Awake has run.
        if (_playerInput == null && _subject != null)
        {
            _playerInput = _subject.GetComponent<PlayerInput>();
        }
        Assert.IsNotNull(_playerInput, "Ensure a subject is properly hooked up");
        Subscribe();
    }

    protected override void OnActorStart()
    {
        LayerUtils.SetLayer(transform, LayerUtils.LocalPlayerLayerName);
    }

    public void OnNotify(InputHandlerActions action, InputValue inputValue)
    {
        // Dead players: the ragdoll teardown destroyed the locomotion FSM —
        // stale gameplay input (or input re-enabled after death) must not NRE.
        if (_locomotion == null)
        {
            return;
        }

        switch (action)
        {
            case InputHandlerActions.Move:
                _locomotion.setMovementInput(inputValue.Get<Vector2>());
                break;
            case InputHandlerActions.Look:
                string controlScheme = _playerInput != null ? _playerInput.currentControlScheme : string.Empty;
                _locomotion.setLookInput(inputValue.Get<Vector2>(), controlScheme);
                break;
            case InputHandlerActions.ToggleRun:
                _locomotion.setIsRunning(inputValue.isPressed);
                break;
            case InputHandlerActions.Aim:
                _locomotion.setIsAiming(inputValue.isPressed);
                break;
            case InputHandlerActions.Shoot:
                // Input System button actions notify once per phase (started /
                // performed / canceled). Gate on isPressed so a single click
                // can never fire the weapon twice.
                if (inputValue.isPressed)
                {
                    _locomotion.HandleShoot();
                }
                break;
            case InputHandlerActions.Reload:
                if (inputValue.isPressed)
                {
                    _locomotion.HandleReload();
                }
                break;
            case InputHandlerActions.ManualEnableRagdoll:
                RagdollUtils.EnableRagdoll(transform, OnRagdollEnabled);
                break;
        }
    }

    protected override void OnActorDestroy()
    {
        LocalPlayerRegistry.Unregister(this);
    }

    protected override void OnRagdollEnabled()
    {
        // RigBuilder depends on the Animator — it must be destroyed BEFORE the
        // base teardown removes the Animator, or Unity refuses ("Can't remove
        // Animator because RigBuilder depends on it").
        Destroy(GetComponent<RigBuilder>());

        base.OnRagdollEnabled();

        // A dead body must not keep processing gameplay input (a corpse that
        // still moves/aims would fight the game-over overlay).
        Unsubscribe();

        Destroy(GetComponent<BoneRenderer>());
        Destroy(GetComponent<CharacterUIController>());
        Destroy(_locomotion);
    }

    private void OnEnable()
    {
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Subscribe()
    {
        if (_subject != null && !_subscribed)
        {
            _subject.AddObserver(this);
            _subscribed = true;
        }
    }

    private void Unsubscribe()
    {
        if (_subject != null && _subscribed)
        {
            _subject.RemoveObserver(this);
            _subscribed = false;
        }
    }

    public override void OnExternalInteraction(IInteractable attacker)
    {
        // Networking: the bite interaction lands wherever the zombie AI runs
        // (the host), but the victim-side take-bite FSM belongs to the
        // victim's owner. Remote copies relay instead of simulating — running
        // the FSM here would fight the owner-authoritative transform.
        if (_biteRelay != null && !_biteRelay.SimulatesLocally)
        {
            _biteRelay.RelayBiteFromServer(attacker);
            return;
        }

        // Ignore duplicate interactions while a take-bite is already in progress so
        // the TakeBite trigger is not re-fired (which would replay the animation).
        if (_locomotion != null && _locomotion.isBeingAttacked)
        {
            return;
        }

        _locomotion.TriggerTakeBite(attacker);

        transform.position = attacker.victimHook.position;
        transform.rotation = attacker.victimHook.rotation;
    }

}
