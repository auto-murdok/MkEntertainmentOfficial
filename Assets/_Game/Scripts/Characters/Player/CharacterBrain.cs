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

    protected override void Start()
    {
        // Register with the InteractableRegistry, disable ragdoll, set layers, etc.
        base.Start();

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
