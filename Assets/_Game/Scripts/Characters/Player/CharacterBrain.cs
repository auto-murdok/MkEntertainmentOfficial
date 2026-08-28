using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.Assertions;
using UnityEngine.InputSystem;

public class CharacterBrain : ActorBrainBase, ISurvivor, IObserver<InputHandlerActions, InputValue>
{
    [Header("Connection Settings")]
    [SerializeField] private Subject<InputHandlerActions, InputValue> _subject;

    private CharacterLocomotion _locomotion;
    private PlayerInput _playerInput;

    [Header("Stats")]
    [SerializeField] private float _maxHitPoints = 100f;
    [SerializeField] private float _biteDamage = 25f;

    Vector3 ISurvivor.TargetPosition => transform.position;
    public override Transform victimHook => transform;
    public override bool isPreparing => false;

    private void Awake()
    {
        _locomotion = GetComponent<CharacterLocomotion>();
        if (_subject != null)
        {
            _playerInput = _subject.GetComponent<PlayerInput>();
        }

        Assert.IsNotNull(_locomotion, "Please attach a component of type CharacterLocomotion");
        Assert.IsNotNull(_playerInput, "Ensure a subject is properly hooked up");

        _hitPoints = _maxHitPoints;

        // Hand the entity-specific death routine to the shared Dead state via the
        // base's onDeath hook.
        Context = _locomotion._context;
        SetupDeathHook();
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
        if (_subject != null)
        {
            _subject.AddObserver(this);
        }
    }

    private void OnDisable()
    {
        if (_subject != null)
        {
            _subject.RemoveObserver(this);
        }
    }

    public void RecoverControl()
    {
        _locomotion.HandleRecoverControl();
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

    public void TakeDamage()
    {
        ApplyDamage(_biteDamage);
    }
}
