using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Animations.Rigging;
using UnityEngine.InputSystem;

public class CharacterLocomotion : StateMachine<CharacterState, CharacterStateContext>
{
    private const string EquippedWeaponPrefabName = "FakeGun";
    private const string KeyboardAndMouseScheme = "Keyboard&Mouse";
    private const string ReloadingAnimatorParameter = "isReloading";
    private const int AimAnimatorLayerIndex = 1;

    [Header("Inverse Kinematics")]
    [SerializeField] private Rig _characterRig;
    [SerializeField] private MultiAimConstraint characterBodyAim;
    private RigRecoil _bodyAimRecoil;

    [Header("Cinemachine")]
    [Tooltip("The follow target set in the Cinemachine Virtual Camera that the camera will follow")]
    [SerializeField] private Transform _cinemachineTarget;

    [Tooltip("How far in degrees can you move the camera up")]
    [SerializeField] private float _topClamp = 70.0f;

    [Tooltip("How far in degrees can you move the camera down")]
    [SerializeField] private float _bottomClamp = -30.0f;

    [SerializeField] private float _mouseLookSensitivity = 1f;
    [SerializeField] private float _gamepadLookSensitivity = 1f;
    private CameraUtils _cameraUtils;

    [Header("Item hooks")]
    [SerializeField] private Transform _rightHandWeaponHolder;
    private Weapon _equippedWeapon;

    [Header("Combat hooks")]
    [SerializeField] private Transform _aimTarget;

    private CinemachineContext _cinemachineProps;

    private void Awake()
    {
        Cursor.lockState = CursorLockMode.Locked;

        PopulateStates();
        InitializeCinemachineContext();
        InitializeAnimatorAndAgent();
        InitializeCameraUtils();
        _bodyAimRecoil = new RigRecoil(characterBodyAim);
        EquipWeapon(EquippedWeaponPrefabName);
        RegisterCommonUpdateEffects();
        OnStateChanged += state => Debug.Log($"[{gameObject.name}] -> {state}");
    }

    private void PopulateStates()
    {
        states[CharacterState.Idle] = new CharacterIdleState();
        states[CharacterState.Moving] = new CharacterWalkingState();
        states[CharacterState.Aiming] = new CharacterAimState();
        states[CharacterState.TakingBite] = new CharacterTakeBiteState();
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
        _context.agent = GetComponent<NavMeshAgent>();
        _context.rig = _characterRig;
        _context.mainCameraTarget = _cinemachineTarget;
    }

    private void InitializeCameraUtils()
    {
        _cameraUtils = new CameraUtils();
    }

    private void RegisterCommonUpdateEffects()
    {
        OnCommonUpdate += RelieveAimEffect;
        OnCommonUpdate += RelieveMovementEffect;
        OnCommonUpdate += (CharacterState currentStateEnum) => _bodyAimRecoil.RelieveRecoil();
    }

    private void RelieveAimEffect(CharacterState currentStateEnum)
    {
        if (currentStateEnum != CharacterState.Aiming)
        {
            AnimatorUtils.SetLayerWeight(_context.animator, AimAnimatorLayerIndex, 0f, 10f);
            RigUtils.HandleDecreaseRigWeight(_context.rig);
        }
    }

    private void RelieveMovementEffect(CharacterState currentStateEnum)
    {
        if (currentStateEnum != CharacterState.Moving)
        {
            AnimatorUtils.SetMovementRootMotion(_context.animator, Vector2.zero, 0.15f);
            _context.isRunning = false;
        }
    }

    private void EquipWeapon(string weaponName)
    {
        Item prefab = PrefabManager.Instance.GetItemPrefab(weaponName);
        _equippedWeapon = (Weapon)Instantiate(prefab, _rightHandWeaponHolder);

        FireArmEvents fireArmEvents = new FireArmEvents
        {
            onShoot = onWeaponShoot,
            onReloadStarted = onWeaponReloadStarted,
            onReloadFinished = onWeaponReloadFinished,
        };

        _equippedWeapon.RegisterEvents(fireArmEvents);
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
        _context.isRunning = isRunning;
    }

    public void setIsAiming(bool isAiming)
    {
        _context.isAiming = isAiming;
    }

    public void HandleShoot()
    {
        _equippedWeapon.TriggerShoot(_aimTarget.position);
    }

    public void HandleReload()
    {
        _equippedWeapon.TriggerReload();
    }

    public void HandleTakeDamage(IInteractable attacker)
    {
        _context.attacker = attacker;
        _context.isBeingAttacked = true;
    }

    public void HandleRecoverControl()
    {
        _context.isBeingAttacked = false;
    }

    private void onWeaponShoot()
    {
        _bodyAimRecoil.ApplyRecoil(_equippedWeapon.recoilForce);
    }

    private void onWeaponReloadStarted()
    {
        _context.animator.SetBool(ReloadingAnimatorParameter, true);
        _context.isReloading = true;
    }

    private void onWeaponReloadFinished()
    {
        _context.animator.SetBool(ReloadingAnimatorParameter, false);
        _context.isReloading = false;
    }
}
