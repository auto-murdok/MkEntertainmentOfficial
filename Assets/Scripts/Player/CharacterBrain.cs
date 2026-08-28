using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Animations.Rigging;
using UnityEngine.Assertions;
using UnityEngine.InputSystem;

public class CharacterBrain : MonoBehaviour, ISurvivor, IInteractable, IObserver<InputHandlerActions, InputValue>
{
    private const string LocalPlayerLayerName = "LocalPlayer";
    private const string TakeBiteTriggerName = "TakeBite";

    [Header("Connection Settings")]
    [SerializeField] private Subject<InputHandlerActions, InputValue> _subject;

    private CharacterLocomotion _locomotion;
    private PlayerInput _playerInput;

    Vector3 ISurvivor.TargetPosition => transform.position;
    public int id => 1;
    public Vector3 position => transform.position;
    public Transform victimHook => transform;

    public bool isPreparing => false;

    private void OnValidate()
    {
        // Reserved for inspecting child colliders (capsule/box) during editor validation.
    }

    private void Awake()
    {
        _locomotion = GetComponent<CharacterLocomotion>();
        _playerInput = _subject.GetComponent<PlayerInput>();

        Assert.IsNotNull(_locomotion, "PLease attach a component of type CharacterLocomotion");
        Assert.IsNotNull(_playerInput, "Ensure a subject is properly hooked up");
    }

    public void Start()
    {
        InteractableManager.Instance.AddInteractable(this);
        LayerUtils.SetLayer(transform, LocalPlayerLayerName);
        RagdollUtils.DisableRagdoll(transform);
    }

    public void OnNotify(InputHandlerActions action, InputValue inputValue)
    {
        switch (action)
        {
            case InputHandlerActions.Move:
                _locomotion.setMovementInput(inputValue.Get<Vector2>());
                break;
            case InputHandlerActions.Look:
                _locomotion.setLookInput(inputValue.Get<Vector2>(), _playerInput.currentControlScheme);
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
        Debug.LogWarning("RAGDOLL!");

        Destroy(GetComponent<RigBuilder>());
        Destroy(GetComponent<BoneRenderer>());
        Destroy(GetComponent<NavMeshAgent>());
        Destroy(GetComponent<Animator>());
        Destroy(GetComponent<CharacterUIController>());
        Destroy(_locomotion);
    }

    private void OnEnable()
    {
        _subject.AddObserver(this);
    }

    private void OnDisable()
    {
        _subject.RemoveObserver(this);
    }

    public void RecoverControl()
    {
        _locomotion.HandleRecoverControl();
    }

    public void OnExternalInteraction(IInteractable attacker)
    {
        _locomotion.HandleTakeDamage(attacker);

        transform.position = attacker.victimHook.position;
        transform.rotation = attacker.victimHook.rotation;
        _locomotion._context.animator.SetTrigger(TakeBiteTriggerName);

        float distance = Vector3.Distance(transform.position, attacker.position);
        Debug.LogWarning($"{name} distance to target is {distance}");
    }

    public void TakeDamage()
    {
        throw new System.NotImplementedException();
    }
}
