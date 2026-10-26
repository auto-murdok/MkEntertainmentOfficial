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

    // Grounded vertical channel (docs/grounded_motor_and_corpse_layer.md):
    // the clip owns the ground plane, this channel owns Y. Without it the
    // motor had no way down: whatever the controller stepped up onto
    // (a corpse pile, a crate) became her new floor height forever.
    private const float GravityAcceleration = -20f;
    // Assigned (never accumulated) while grounded, and never 0: 0 lets
    // PhysX depenetration flicker the grounded state frame to frame.
    private const float GroundedStickSpeed = -2f;
    private const float TerminalFallSpeed = -50f;
    private const float GroundProbeRadius = 0.24f; // ~0.8x the controller radius
    private const float GroundProbeOriginHeight = 0.35f;
    private const float GroundProbeLength = 0.85f;
    private const float GroundSnapMaxDistance = 0.5f;
    private const int SnapGraceFramesAfterReset = 2;

    private float _verticalVelocity = GroundedStickSpeed;
    private bool _groundedPrev;
    private int _snapGraceFrames;
    private int _groundProbeMask;

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
    //
    // Exactly ONE Move per frame happens here, in the mode the active
    // state selected on the blackboard (CharacterStateContext.motorMode):
    // scripted interactions own their frames outright, so the grounded
    // channel can never compose sink or snap on top of a grapple pin.
    private void OnAnimatorMove()
    {
        Animator animator = _context.animator;
        if (animator == null || !_context.isAlive)
        {
            return;
        }

        // Remote copies are driven by NetworkTransform; running the motor
        // there too would double-drive the transform and burn the ground
        // probe on every peer. Unspawned (offline / tests) runs the motor.
        if (_networkAnimator != null && _networkAnimator.IsSpawned && !_networkAnimator.IsOwner)
        {
            return;
        }

        if (_context.motorResetRequested)
        {
            _context.motorResetRequested = false;
            ResetMotorState();
        }

        switch (_context.motorMode)
        {
            case MotorMode.ScriptedPin:
            case MotorMode.Disabled:
                // The interaction's own Move already ran in the FSM
                // update; the motor adds nothing this frame.
                return;
            case MotorMode.AuthoredY:
                transform.rotation *= animator.deltaRotation;
                MoveByRootMotion(animator.deltaPosition);
                _groundedPrev = false;
                return;
        }

        transform.rotation *= animator.deltaRotation;
        if (_characterController == null || !_characterController.enabled)
        {
            transform.position += animator.deltaPosition;
            return;
        }

        float deltaTime = Time.deltaTime;

        // Probe BEFORE the Move: the snap needs the current-frame gap to
        // the ground under the capsule. isGrounded cannot supply it — it
        // only reports the last Move and carries no distance. The mask
        // excludes actors and corpses, so the probe always measures the
        // floor itself, never the pile she is standing in.
        Vector3 probeOrigin = transform.position + Vector3.up * GroundProbeOriginHeight;
        bool probeHit = Physics.SphereCast(probeOrigin, GroundProbeRadius, Vector3.down,
            out RaycastHit groundHit, GroundProbeLength, _groundProbeMask, QueryTriggerInteraction.Ignore);

        // The clip's authored Y is discarded in this mode on purpose:
        // the vertical channel below is its only owner.
        Vector3 motion = animator.deltaPosition;
        motion.y = _verticalVelocity * deltaTime;

        if (_snapGraceFrames > 0)
        {
            _snapGraceFrames--;
        }
        else if (_groundedPrev && _verticalVelocity <= 0f && probeHit &&
                 groundHit.normal.y >= Mathf.Cos(_characterController.slopeLimit * Mathf.Deg2Rad))
        {
            // Grounded last frame and the floor fell away by a step (not
            // a ledge): fold the drop into this same Move so she stays
            // glued down stairs instead of floating off each tread.
            float gap = groundHit.distance - (GroundProbeOriginHeight - GroundProbeRadius);
            if (gap > _characterController.skinWidth && gap <= GroundSnapMaxDistance)
            {
                motion.y -= gap;
            }
        }

        CollisionFlags flags = _characterController.Move(motion);
        bool groundedNow = (flags & CollisionFlags.Below) != 0;
        if (groundedNow)
        {
            _verticalVelocity = GroundedStickSpeed;
        }
        else
        {
            _verticalVelocity = Mathf.Max(_verticalVelocity + GravityAcceleration * deltaTime, TerminalFallSpeed);
        }
        _groundedPrev = groundedNow;
    }

    private void MoveByRootMotion(Vector3 deltaPosition)
    {
        if (_characterController != null && _characterController.enabled)
        {
            _characterController.Move(deltaPosition);
        }
        else
        {
            transform.position += deltaPosition;
        }
    }

    // Clears the vertical channel after any discontinuous move (spawn,
    // respawn, teleport) or scripted control (bite exit): stale fall
    // speed and a stale grounded flag must never carry across them. The
    // snap stays disarmed for the first frames until grounding is
    // re-confirmed by a real Move.
    public void ResetMotorState()
    {
        _verticalVelocity = GroundedStickSpeed;
        _groundedPrev = false;
        _snapGraceFrames = SnapGraceFramesAfterReset;
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

        // The ground probe measures floors, not bodies: exclude every
        // actor layer (herself, zombies) and corpses, so standing in a
        // ragdoll pile can never read as "grounded on the pile".
        int excluded = 0;
        AddLayerBit(ref excluded, LayerUtils.LocalPlayerLayerName);
        AddLayerBit(ref excluded, LayerUtils.ZombieLayerName);
        AddLayerBit(ref excluded, LayerUtils.CorpseLayerName);
        _groundProbeMask = Physics.DefaultRaycastLayers & ~excluded;
        // The NavMeshAgent is intentionally NOT used for player locomotion (movement is
        // root-motion driven, which is the gold standard for a directly-controlled,
        // animated character). It exists only to pin the actor's radius during the
        // bite (CharacterTakeBiteState sets agent.radius + ResetPath); agent.speed is unused.
        _context.agent = GetComponent<NavMeshAgent>();
        _context.rig = _characterRig;
        _context.mainCameraTarget = _cinemachineTarget;
        _context.UIController = GetComponent<CharacterUIController>();
    }

    private static void AddLayerBit(ref int mask, string layerName)
    {
        int layer = LayerMask.NameToLayer(layerName);
        if (layer >= 0)
        {
            mask |= 1 << layer;
        }
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
