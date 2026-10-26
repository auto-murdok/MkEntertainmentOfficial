using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Animations.Rigging;
using UnityEngine.InputSystem;

public class CharacterLocomotion : StateMachine<CharacterState, CharacterStateContext>
{
    private const string EquippedWeaponPrefabName = "FakeGun";
    private const string KeyboardAndMouseScheme = "Keyboard&Mouse";
    private const int AimAnimatorLayerIndex = 1;

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
    private float _baseLookSensitivity;
    private float _aimLookSensitivityScale = 1f;
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
    private CharacterController _characterController;

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

    // Once this exists on the Animator's GameObject, Unity hands root
    // motion to the script instead of applying it to the transform
    // itself ("Handled by Script"). Route the clip displacement through
    // the CharacterController so locomotion is collision-swept - it
    // stops at walls and at the zombies' body capsules instead of
    // teleporting through them. Rotation keeps the built-in behavior.
    private void OnAnimatorMove()
    {
        Animator animator = _context.animator;
        if (animator == null)
        {
            return;
        }

        transform.rotation *= animator.deltaRotation;
        if (_characterController != null && _characterController.enabled)
        {
            _characterController.Move(animator.deltaPosition);
        }
        else
        {
            transform.position += animator.deltaPosition;
        }
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
            topClamp = _topClamp,
            bottomClamp = _bottomClamp,
        };
    }

    private void InitializeAnimatorAndAgent()
    {
        _context.animator = GetComponent<Animator>();
        _characterController = GetComponent<CharacterController>();
        _context.characterController = _characterController;
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
        OnCommonUpdate += SyncTakingBiteAnimatorParam;
        OnCommonUpdate += RelieveAimEffect;
        OnCommonUpdate += RelieveMovementEffect;
        OnCommonUpdate += RelieveRecoilEffect;
    }

    private bool _animatorTakingBite;

    // Mirrors the take-bite flag into the Animator: take_bite's decoupled
    // exit transition keys off isTakingBite, so the victim clip ends on the
    // gameplay clock (blended) when she is freed, instead of playing out to
    // its authored exit time while she already has control back.
    private void SyncTakingBiteAnimatorParam(CharacterState currentState)
    {
        if (_context.animator == null || _animatorTakingBite == _context.isBeingAttacked)
        {
            return;
        }
        _animatorTakingBite = _context.isBeingAttacked;
        _context.animator.SetBool(AnimatorUtils.IsTakingBiteHash, _animatorTakingBite);
    }

    private void RelieveRecoilEffect(CharacterState currentStateEnum)
    {
        _bodyAimRecoil.RelieveRecoil();
    }

    private void RelieveAimEffect(CharacterState currentStateEnum)
    {
        if (currentStateEnum != CharacterState.Aiming && currentStateEnum != CharacterState.Reloading)
        {
            AnimatorUtils.SetLayerWeight(_context.animator, AimAnimatorLayerIndex, 0f, 10f);
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
        // Re-derived every frame: while aiming, look sensitivity is scaled
        // by the FOV ratio between the follow and aim cameras (focal-length
        // scaling — see AimSensitivity), so the same mouse movement tracks
        // at the same on-screen speed across the zoom.
        _cinemachineProps.lookSensivity = _baseLookSensitivity * (_context.isAiming ? _aimLookSensitivityScale : 1f);
        _cameraUtils.HandleCameraRotation(_context, _cinemachineProps);
    }

    // Seed the camera yaw/pitch from the placed camera target. The
    // composition root (PlayerRigging.WireLocalRig) calls this once spawn
    // placement and rig wiring are final, so the first rendered frame
    // already matches the spawn facing instead of snapping to (0, 0).
    // Re-call after any discontinuous move (respawn, teleport).
    public void SeedCamera()
    {
        _cameraUtils.SeedFrom(_cinemachineTarget);
    }

    public void setMovementInput(Vector2 movementInput)
    {
        _context.movementInput = movementInput;
    }

    public void setLookInput(Vector2 lookInput, string currentControlScheme)
    {
        _context.isCurrentDeviceMouse = currentControlScheme == KeyboardAndMouseScheme;
        _baseLookSensitivity = _context.isCurrentDeviceMouse ? _mouseLookSensitivity : _gamepadLookSensitivity;
        _context.lookInput = lookInput;
    }

    // Wired by CameraModeController when the camera rig is composed: the
    // FOV-ratio look-sensitivity scale to apply while aiming (1 = unscaled).
    public void SetAimLookSensitivityScale(float scale)
    {
        _aimLookSensitivityScale = scale > 0f ? scale : 1f;
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
            // _aimTarget is the AimTarget marker, which snaps to this
            // frame's center-screen ray hit (see AimTarget) — its position
            // IS the live aim point, free of the old smoothing lag.
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
        // The TakeBite clip is NOT started here: it is authored to begin at
        // the attacker's contact moment, so CharacterTakeBiteState fires the
        // trigger when the grab phase ends (see StartClip there).
    }

    // Animator triggers are not synced by value like bools/floats/integers:
    // on a networked owner they must be raised through NetworkAnimator.SetTrigger
    // to replicate (and NetworkAnimator.SetTrigger is an error on non-owners,
    // so single-player/un-owned paths fall back to the raw animator).
    public void SetAnimatorTrigger(int triggerHash)
    {
        if (_context.animator == null)
        {
            return;
        }
        if (_networkAnimator != null && _networkAnimator.IsSpawned && _networkAnimator.IsOwner)
        {
            _networkAnimator.SetTrigger(triggerHash);
        }
        else
        {
            _context.animator.SetTrigger(triggerHash);
        }
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
