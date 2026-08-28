using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Animations.Rigging;
using UnityEngine.Assertions;
using UnityEngine.InputSystem;

public class CharacterBrain : MonoBehaviour, ISurvivor, IInteractable, IObserver<InputHandlerActions, InputValue>
{

    [Header("Connection Settings")]
    [SerializeField] private Subject<InputHandlerActions, InputValue> _subject;

    private CharacterLocomotion _locomotion;
    private PlayerInput _playerInput;

    [Header("Stats")]
    [SerializeField] private float _maxHitPoints = 100f;
    private float _hitPoints;

    Vector3 ISurvivor.TargetPosition => transform.position;
    public int id => gameObject.GetInstanceID();
    public Vector3 position => transform.position;
    public Transform victimHook => transform;

    public bool isPreparing => false;

    private void Awake()
    {
        _locomotion = GetComponent<CharacterLocomotion>();
        if (_subject != null)
        {
            _playerInput = _subject.GetComponent<PlayerInput>();
        }

        Assert.IsNotNull(_locomotion, "Please attach a component of type CharacterLocomotion");
        if (_playerInput == null)
        {
            // Spawned players receive their subject afterwards via SetInputSubject.
            Debug.LogWarning($"[{name}] No input subject wired yet. Call SetInputSubject after spawning.");
        }

        _hitPoints = _maxHitPoints;

        // Hand the entity-specific death routine to the shared Dead state.
        _locomotion._context.onDeath = () => RagdollUtils.EnableRagdoll(transform, OnEnableRagdoll);
    }

    public void Start()
    {
        if (InteractableManager.Instance != null)
        {
            InteractableManager.Instance.AddInteractable(this);
        }
        LayerUtils.SetLayer(transform, LayerUtils.LocalPlayerLayerName);
        RagdollUtils.DisableRagdoll(transform);
    }

    /// <summary>
    /// Late wiring entry point used by spawners: assigns the input subject after
    /// instantiation and subscribes this brain as an observer.
    /// </summary>
    public void SetInputSubject(Subject<InputHandlerActions, InputValue> subject)
    {
        _subject = subject;
        _playerInput = _subject != null ? _subject.GetComponent<PlayerInput>() : null;

        if (_subject != null)
        {
            // OnEnable may already have run before wiring (spawn path), so subscribe here
            // and keep OnEnable/OnDisable idempotent for scene-placed players.
            _subject.RemoveObserver(this);
            _subject.AddObserver(this);
        }
    }

    private void OnDestroy()
    {
        if (InteractableManager.Instance != null)
        {
            InteractableManager.Instance.RemoveInteractable(this);
        }
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
                RagdollUtils.EnableRagdoll(transform, OnEnableRagdoll);
                break;
        }
    }

    private void OnEnableRagdoll()
    {
        if (InteractableManager.Instance != null)
        {
            InteractableManager.Instance.RemoveInteractable(this);
        }

        Destroy(GetComponent<RigBuilder>());
        Destroy(GetComponent<BoneRenderer>());
        Destroy(GetComponent<NavMeshAgent>());
        Destroy(GetComponent<Animator>());
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

    public void OnExternalInteraction(IInteractable attacker)
    {
        // Ignore duplicate interactions while a take-bite is already in progress so
        // the TakeBite trigger is not re-fired (which would replay the animation).
        if (_locomotion != null && _locomotion._context.isBeingAttacked)
        {
            return;
        }

        _locomotion.HandleTakeDamage(attacker);

        transform.position = attacker.victimHook.position;
        transform.rotation = attacker.victimHook.rotation;
        _locomotion._context.animator.SetTrigger(AnimatorUtils.TakeBiteHash);
    }

    public void TakeDamage()
    {
        _hitPoints = Mathf.Max(0f, _hitPoints - 25f);
        if (_hitPoints <= 0f)
        {
            // Let the shared Dead state (driven by the locomotion FSM) handle the
            // ragdoll + teardown via context.onDeath.
            _locomotion._context.isAlive = false;
        }
    }
}
