using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Animations.Rigging;
using UnityEngine.InputSystem;

public class CharacterLocomotion : StateMachine<CharacterState, CharacterStateContext>
{
    private const string EquippedWeaponPrefabName = "FakeGun";
    private const string KeyboardAndMouseScheme = "Keyboard&Mouse";

    [Header("Inverse Kinematics")]
    [SerializeField] private Rig _characterRig;
    [SerializeField] private MultiAimConstraint characterBodyAim;
    private RigRecoil _bodyAimRecoil;

    [Header("Cinemachine")]
    [Tooltip("The follow target set in the Cinemachine Virtual Camera that the camera will follow")]
    public Transform _cinemachineTarget;

    [Tooltip("How far in degrees can you move the camera up")]
    [SerializeField] private float _topClamp = 70.0f;

    [Tooltip("How far in degrees can you move the camera down")]
    [SerializeField] private float _bottomClamp = -30.0f;

    [SerializeField] private float _mouseLookSensitivity = 1f;
    [SerializeField] private float _gamepadLookSensitivity = 1f;
    private CameraUtils _cameraUtils;

    [Header("Item hooks")]
    [SerializeField] private Transform _rightHandWeaponHolder;
    // SO-architecture: the player prefab references the shared item catalog
    // asset directly — no scene singleton lookup.
    [SerializeField] private ItemCatalog _itemCatalog;
    private Weapon _equippedWeapon;

    [Header("Combat hooks")]
    public Transform _aimTarget;

    [Header("Data")]
    [SerializeField] private PlayerData _playerData;

    // Networked animation sync (docs/networking_notes.md): triggers must be
    // routed through the NetworkAnimator on the owning peer to replicate.
    private Unity.Netcode.Components.NetworkAnimator _networkAnimator;

    private CinemachineContext _cinemachineProps;

    private void Awake()
    {
        Cursor.lockState = CursorLockMode.Locked;

        _networkAnimator = GetComponent<Unity.Netcode.Components.NetworkAnimator>();
        PopulateStates();
        InitializeCinemachineContext();
        InitializeAnimatorAndAgent();
        InitializeCameraUtils();
        _bodyAimRecoil = new RigRecoil(characterBodyAim);
        EquipWeapon(EquippedWeaponPrefabName);
        RegisterCommonUpdateEffects();
        RegisterGlobalTransitions();
    }

    private void PopulateStates()
    {
        states[CharacterState.Idle] = new CharacterIdleState();
        states[CharacterState.Walking] = new CharacterWalkingState();
        states[CharacterState.Sprinting] = new CharacterSprintingState();
        states[CharacterState.Aiming] = new CharacterAimState();
        states[CharacterState.Reloading] = new CharacterReloadingState();
        states[CharacterState.TakingBite] = new CharacterTakeBiteState();
        states[CharacterState.Dead] = new ActorDeadState<CharacterState, CharacterStateContext>();
    }

    // Global guard: the moment the shared blackboard reports death, force a
    // transition to the reusable Dead state regardless of the active state.
    private void RegisterGlobalTransitions()
    {
        CheckGlobalTransition = (current) => _context.isAlive ? current : CharacterState.Dead;

        // The ScriptableObject is the only source of truth for player config:
        // fail loudly instead of silently skipping when it is missing.
        if (_playerData == null)
        {
            Debug.LogError($"[{name}] PlayerData is not assigned on CharacterLocomotion. " +
                           "Assign a PlayerData asset (e.g. Assets/_Game/Data/Players/PlayerData_Default.asset).");
            return;
        }

        _context.data = _playerData;
    }

    private void InitializeCinemachineContext()
    {
        _cinemachineProps = new CinemachineContext
        {
            targetYaw = _cinemachineTarget.transform.rotation.y,
            topClamp = _topClamp,
            bottomClamp = _bottomClamp,
        };
    }

    private void InitializeAnimatorAndAgent()
    {
        _context.animator = GetComponent<Animator>();
        // The NavMeshAgent is intentionally NOT used for player locomotion (movement is
        // root-motion driven, which is the gold standard for a directly-controlled,
        // animated character). It exists only to pin the actor's radius during the
        // bite (CharacterTakeBiteState sets agent.radius + ResetPath); agent.speed is unused.
        _context.agent = GetComponent<NavMeshAgent>();
        _context.rig = _characterRig;
        _context.mainCameraTarget = _cinemachineTarget;
        _context.UIController = GetComponent<CharacterUIController>();
    }

    private void InitializeCameraUtils()
    {
        _cameraUtils = new CameraUtils();
    }

    private void RegisterCommonUpdateEffects()
    {
        OnCommonUpdate += RelieveAimEffect;
        OnCommonUpdate += RelieveMovementEffect;
        OnCommonUpdate += RelieveRecoilEffect;
    }

    private void RelieveRecoilEffect(CharacterState currentStateEnum)
    {
        _bodyAimRecoil.RelieveRecoil();
    }

    private void RelieveAimEffect(CharacterState currentStateEnum)
    {
        if (currentStateEnum != CharacterState.Aiming && currentStateEnum != CharacterState.Reloading)
        {
            AnimatorUtils.SetLayerWeight(_context.animator, AnimatorUtils.AimLayerIndex, 0f, 10f);
            RigUtils.HandleDecreaseRigWeight(_context.rig);
        }
    }

    private void RelieveMovementEffect(CharacterState currentStateEnum)
    {
        if (currentStateEnum != CharacterState.Walking && currentStateEnum != CharacterState.Sprinting)
        {
            AnimatorUtils.SetMovementRootMotion(_context.animator, Vector2.zero, 0.15f);
        }
    }

    private void EquipWeapon(string weaponName)
    {
        if (_itemCatalog == null)
        {
            Debug.LogWarning($"[{name}] No ItemCatalog asset assigned — no weapon equipped. " +
                             "Assign the shared ItemCatalog asset on CharacterLocomotion (player prefab).");
            return;
        }

        Item prefab = _itemCatalog.GetItemPrefab(weaponName);
        if (prefab == null || _rightHandWeaponHolder == null)
        {
            Debug.LogWarning($"[{name}] Weapon '{weaponName}' not equipped (prefab missing from ItemCatalog or right-hand holder unset).");
            return;
        }

        _equippedWeapon = (Weapon)Instantiate(prefab, _rightHandWeaponHolder);

        FirearmEvents fireArmEvents = new FirearmEvents
        {
            onShoot = onWeaponShoot,
            onReloadStarted = onWeaponReloadStarted,
            onReloadFinished = onWeaponReloadFinished,
        };

        _equippedWeapon.RegisterEvents(fireArmEvents);
        // Inject scene/UI references on the instance (composition-root rule —
        // the weapon never discovers scene objects itself).
        _equippedWeapon.InjectUIController(_context.UIController);
    }

    private void LateUpdate()
    {
        _cameraUtils.HandleCameraRotation(_context, _cinemachineProps);
    }

    public void setMovementInput(Vector2 movementInput)
    {
        _context.movementInput = movementInput;
    }

    public void setLookInput(Vector2 lookInput, string currentControlScheme)
    {
        _context.isCurrentDeviceMouse = currentControlScheme == KeyboardAndMouseScheme;
        _cinemachineProps.lookSensivity = _context.isCurrentDeviceMouse ? _mouseLookSensitivity : _gamepadLookSensitivity;
        _context.lookInput = lookInput;
    }

    public void setIsRunning(bool isRunning)
    {
        // Disallow enabling sprint while reloading
        _context.isRunning = isRunning && !_context.isReloading;
    }

    public void setIsAiming(bool isAiming)
    {
        _context.isAiming = isAiming;
    }

    public void HandleShoot()
    {
        if (_equippedWeapon != null && _aimTarget != null)
        {
            _equippedWeapon.TriggerShoot(_aimTarget.position);
        }
    }

    public void HandleReload()
    {
        if (_equippedWeapon != null)
        {
            _equippedWeapon.TriggerReload();
        }
    }

    public void TriggerTakeBite(IInteractable attacker)
    {
        _context.attacker = attacker;
        _context.isBeingAttacked = true;
        SetAnimatorTrigger(AnimatorUtils.TakeBiteHash);
    }

    // Animator triggers are not synced by value like bools/floats/integers:
    // on a networked owner they must be raised through NetworkAnimator.SetTrigger
    // to replicate (and NetworkAnimator.SetTrigger is an error on non-owners,
    // so single-player/un-owned paths fall back to the raw animator).
    public void SetAnimatorTrigger(int triggerHash)
    {
        NetworkAnimatorUtils.TrySetTrigger(_context.animator, _networkAnimator,
            _networkAnimator != null && _networkAnimator.IsOwner, triggerHash);
    }

    public bool isBeingAttacked => _context.isBeingAttacked;
    public IInteractable currentAttacker => _context.attacker;
    public bool isAiming => _context.isAiming;

    private void onWeaponShoot()
    {
        if (_equippedWeapon != null)
        {
            _bodyAimRecoil.ApplyRecoil(_equippedWeapon.recoilForce);
        }
    }

    private void onWeaponReloadStarted()
    {
        _context.isRunning = false;
        // The weapon FSM keeps ticking on a ragdolled corpse after death,
        // where the Animator is already destroyed — guard every access.
        if (_context.animator != null)
        {
            _context.animator.SetBool(AnimatorUtils.IsReloadingHash, true);
        }
        _context.isReloading = true;
    }

    private void onWeaponReloadFinished()
    {
        if (_context.animator != null)
        {
            _context.animator.SetBool(AnimatorUtils.IsReloadingHash, false);
        }
        _context.isReloading = false;
    }
}
