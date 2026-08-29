using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.Assertions;
using UnityEngine.InputSystem;

public class CharacterBrain : ActorBrainBase, ISurvivor, IObserver<InputHandlerActions, InputValue>
{
    [Header("Connection Settings")]
    public Subject<InputHandlerActions, InputValue> _subject;

    private CharacterLocomotion _locomotion;
    private PlayerInput _playerInput;
    private bool _subscribed;

    [Header("Stats")]
    [SerializeField] private float _maxHitPoints = 100f;

    Vector3 ISurvivor.TargetPosition => transform.position;
    public override Transform victimHook => transform;
    public override bool isPreparing => false;

    private void Awake()
    {
        _locomotion = GetComponent<CharacterLocomotion>();
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

    private void Start()
    {
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
        switch (action)
        {
            case InputHandlerActions.Move:
                _locomotion.setMovementInput(inputValue.Get<Vector2>());
                break;
            case InputHandlerActions.Look:
                string controlScheme = _playerInput != null ? _playerInput.currentControlScheme : string.Empty;
                _locomotion.setLookInput(inputValue.Get<Vector2>(), controlScheme);
                break;
            case InputHandlerActions.ToogleRun:
                _locomotion.setIsRunning(inputValue.isPressed);
                break;
            case InputHandlerActions.Aim:
                _locomotion.setIsAiming(inputValue.isPressed);
                break;
            case InputHandlerActions.Shoot:
                _locomotion.HandleShoot();
                break;
            case InputHandlerActions.Reload:
                _locomotion.HandleReload();
                break;
            case InputHandlerActions.ManualEnableRagdoll:
                RagdollUtils.EnableRagdoll(transform, OnRagdollEnabled);
                break;
        }
    }

    protected override void OnRagdollEnabled()
    {
        base.OnRagdollEnabled();

        Destroy(GetComponent<RigBuilder>());
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
