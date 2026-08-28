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
    Vector3 ISurvivor.TargetPosition => transform.position;
    public int id => 1;
    public Vector3 position => transform.position;
    public Transform victimHook => transform;

    public bool isPreparing => false;

    private void OnValidate()
    {
        // Collider[] playerColliders = GetComponentsInChildren<Collider>();
        // foreach (Collider collider in playerColliders)
        // {
        //     if (collider.GetType() == typeof(CapsuleCollider)) {
        //         Debug.Log(collider.name + " " + typeof(CapsuleCollider));
        //         //((CapsuleCollider)collider).radius = ((CapsuleCollider)collider).radius / 2;
        //     } else if (collider.GetType() == typeof(BoxCollider)) {
        //         Debug.Log(collider.name + " " + typeof(BoxCollider));
        //     }
        // }
    }

    private void Awake()
    {
        _locomotion = GetComponent<CharacterLocomotion>();
        // _combat = GetComponent<CharacterCombat>();
        _playerInput = _subject.GetComponent<PlayerInput>();

        Assert.IsNotNull(_locomotion, "PLease attach a component of type CharacterLocomotion");
        // Assert.IsNotNull(_combat, "PLease attach a component of type CharacterCombat");
        Assert.IsNotNull(_playerInput, "Ensure a subject is properly hooked up");
    }

    public void Start()
    {
        InteractableManager.Instance.AddInteractable(this);
        LayerUtils.SetLayer(transform, "LocalPlayer");
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
                // _combat.setIsAiming(inputValue.isPressed);
                break;
            case InputHandlerActions.Shoot:
                _locomotion.HandleShoot();
                // _combat.HandleShoot();
                break;
            case InputHandlerActions.Reload:
                _locomotion.HandleShoot();
                // _combat.HandleReload();
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
        // Destroy(_combat);
        // Destroy(GetComponent<CharacterBrain>());
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
        //_locomotion.enabled = true;
        _locomotion.HandleRecoverControl();
    }

    public void OnExternalInteraction(IInteractable attacker)
    {
        _locomotion.HandleTakeDamage(attacker);

        transform.position = attacker.victimHook.position;
        transform.rotation = attacker.victimHook.rotation;
        _locomotion._context.animator.SetTrigger("TakeBite");

        float distance = Vector3.Distance(transform.position, attacker.position);
        Debug.LogWarning($"{name} distance to target is {distance}");
    }

    public void TakeDamage()
    {
        throw new System.NotImplementedException();
    }
}