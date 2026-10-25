using Unity.Netcode;
using UnityEngine;
using UnityEngine.Animations.Rigging;

// Cosmetic head look-at IK (Animation Rigging: Neck + Head MultiAimConstraints
// driven by one look-target transform, rig authored on the prefab).
//
// When the zombie has a detected survivor, its head turns to look straight at
// the survivor's head; when the target is lost the look eases back to neutral.
// This is purely visual: it is computed locally on every peer from data that
// peer already has, and nothing about the IK is replicated. The host/server
// reads the authoritative target from the FSM context; remote clients (where
// ZombieBehavior is disabled by NetworkedZombieController) re-derive the
// target from replicated transforms — the nearest survivor in front of the
// zombie, which is the one it is chasing. The rig evaluates after the
// Animator, so the look layers on top of whatever locomotion/attack pose the
// state machine produced; during bite / hand-attack poses the weight is
// forced to zero so those clips own the head.
[RequireComponent(typeof(ZombieBehavior))]
public class ZombieHeadLook : NetworkBehaviour
{
    [Header("Rig (authored on the prefab)")]
    [SerializeField] private Rig _rig;
    [SerializeField] private Transform _lookTarget;

    [Header("Aim")]
    [Tooltip("Height above the survivor's root to aim the gaze at (TargetPosition is the feet): roughly head height.")]
    [SerializeField] private float _aimHeight = 1.55f;
    [Tooltip("Smoothing applied to the look target itself, so the gaze does not jitter with the survivor's footsteps.")]
    [SerializeField] private float _targetSmoothTime = 0.12f;

    [Header("Weight")]
    [Tooltip("Ceiling for the rig weight. 1.0 contorts the neck; 0.85 keeps the look believable.")]
    [SerializeField] private float _maxWeight = 0.85f;
    [SerializeField] private float _rampInSeconds = 0.3f;
    [SerializeField] private float _rampOutSeconds = 0.5f;
    [Tooltip("Angle from the zombie's facing where the look starts fading out.")]
    [SerializeField] private float _lookFadeStartAngle = 55f;
    [Tooltip("Angle from the zombie's facing beyond which the head no longer tries to look (the body should turn instead).")]
    [SerializeField] private float _maxLookAngle = 75f;

    private static readonly int BiteStateHash = Animator.StringToHash("Base Layer.Bite");
    private static readonly int HandAttackStateHash = Animator.StringToHash("Base Layer.RHandAttack");
    private static readonly Collider[] OverlapBuffer = new Collider[8];

    private ZombieBehavior _behavior;
    private Animator _animator;
    private float _weight;
    private Vector3 _targetVelocity;

    // The remote-client derivation scans at the same cadence as the server's
    // vision scan instead of every frame; between scans the last derived aim
    // point is reused (and smoothed), so the gaze never jitters.
    private const float DeriveScanInterval = 0.15f;
    private float _deriveScanTimer;
    private bool _derivedHasAim;
    private Vector3 _derivedAimPoint;

    // Moves a weight toward a goal at a rate derived from the ramp duration:
    // crossing the full [0, maxWeight] range takes rampSeconds.
    public static float RampWeight(float current, float goal, float maxWeight, float rampSeconds, float deltaTime)
    {
        float step = rampSeconds > 0f ? maxWeight / rampSeconds * deltaTime : maxWeight;
        return Mathf.MoveTowards(current, Mathf.Clamp(goal, 0f, maxWeight), step);
    }

    // 1 at or inside fadeStart, 0 at or beyond fadeEnd, linear between.
    public static float AngleFalloff(float angleDegrees, float fadeStart, float fadeEnd)
    {
        if (angleDegrees <= fadeStart) return 1f;
        if (angleDegrees >= fadeEnd) return 0f;
        return 1f - (angleDegrees - fadeStart) / (fadeEnd - fadeStart);
    }

    private void Awake()
    {
        _behavior = GetComponent<ZombieBehavior>();
        _animator = GetComponent<Animator>();
        if (_rig != null)
        {
            _rig.weight = 0f;
        }
    }

    private void LateUpdate()
    {
        if (_rig == null || _lookTarget == null) return;

        float goal = 0f;
        if (!IsAttackPose() && TryGetAimPoint(out Vector3 aimPoint))
        {
            Vector3 toTarget = aimPoint - transform.position;
            toTarget.y = 0f;
            float angle = toTarget.sqrMagnitude > 0.001f
                ? Vector3.Angle(FlatForward(), toTarget)
                : 0f;
            goal = _maxWeight * AngleFalloff(angle, _lookFadeStartAngle, _maxLookAngle);

            if (goal > 0f)
            {
                _lookTarget.position = Vector3.SmoothDamp(
                    _lookTarget.position, aimPoint, ref _targetVelocity, _targetSmoothTime);
            }
        }

        bool rampingIn = goal > _weight;
        _weight = RampWeight(_weight, goal, _maxWeight, rampingIn ? _rampInSeconds : _rampOutSeconds, Time.deltaTime);

        if (_weight <= 0f && goal <= 0f)
        {
            // Park the target straight ahead so the next ramp-in does not
            // swing the head from a stale position.
            _lookTarget.position = transform.position + transform.forward * 2f + Vector3.up * _aimHeight;
            _targetVelocity = Vector3.zero;
        }

        if (!Mathf.Approximately(_rig.weight, _weight))
        {
            _rig.weight = _weight;
        }
    }

    private Vector3 FlatForward()
    {
        Vector3 forward = transform.forward;
        forward.y = 0f;
        return forward.sqrMagnitude > 0.001f ? forward.normalized : Vector3.forward;
    }

    private bool IsAttackPose()
    {
        if (_behavior != null && _behavior.enabled && _behavior._context != null)
        {
            return _behavior._context.isBiting || _behavior._context.isHandAttacking;
        }

        if (_animator != null)
        {
            AnimatorStateInfo state = _animator.GetCurrentAnimatorStateInfo(0);
            return state.fullPathHash == BiteStateHash || state.fullPathHash == HandAttackStateHash;
        }

        return false;
    }

    private bool TryGetAimPoint(out Vector3 aimPoint)
    {
        aimPoint = default;

        // Authority path: the FSM is live on this peer (host/server or pure
        // local play) and holds the real detected target.
        if (_behavior != null && _behavior.enabled && _behavior._context != null && _behavior._context.target != null)
        {
            aimPoint = _behavior._context.target.TargetPosition + Vector3.up * _aimHeight;
            return true;
        }

        // Remote-client derivation: the behavior is disabled here, so pick the
        // nearest survivor in front of the zombie from replicated transforms.
        if (IsSpawned && !IsServer && _behavior != null)
        {
            _deriveScanTimer -= Time.deltaTime;
            if (_deriveScanTimer <= 0f)
            {
                _deriveScanTimer = DeriveScanInterval;
                _derivedHasAim = ScanForDerivedAim(out _derivedAimPoint);
            }

            aimPoint = _derivedAimPoint;
            return _derivedHasAim;
        }

        return false;
    }

    private bool ScanForDerivedAim(out Vector3 aimPoint)
    {
        aimPoint = default;
        ZombieData data = _behavior.zombieData;
        LayerMask mask = data != null ? data.detectionLayerMask : default;
        if (mask.value == 0) return false;

        float range = (data != null ? data.detectionMaxDistance : 5f) + 2f;
        int count = Physics.OverlapSphereNonAlloc(transform.position, range, OverlapBuffer, mask);
        Vector3 forward = FlatForward();
        float bestSqr = float.MaxValue;
        Vector3 best = default;
        bool found = false;

        for (int i = 0; i < count; i++)
        {
            ISurvivor survivor = OverlapBuffer[i].GetComponentInParent<ISurvivor>();
            if (survivor == null) continue;

            Vector3 to = survivor.TargetPosition - transform.position;
            to.y = 0f;
            float sqr = to.sqrMagnitude;
            if (sqr < 0.04f || sqr >= bestSqr) continue;
            if (Vector3.Angle(forward, to) > _maxLookAngle + 15f) continue;

            bestSqr = sqr;
            best = survivor.TargetPosition;
            found = true;
        }

        if (!found) return false;
        aimPoint = best + Vector3.up * _aimHeight;
        return true;
    }
}
