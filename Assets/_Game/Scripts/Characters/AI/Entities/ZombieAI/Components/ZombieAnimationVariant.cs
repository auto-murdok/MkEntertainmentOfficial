using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Per-zombie animation variety built on the Kimodo clip set. The server
/// rolls one seed per zombie; every peer derives the same variant from it
/// (Knuth multiplicative hash): a walk clip, an idle clip and a cycle phase
/// offset, so no two zombies move in lockstep. The variant is applied as an
/// AnimatorOverrideController over the animator's current controller,
/// swapping only the locomotion blend tree's base idle/walk clips — attack
/// and bite states keep their original clips. The phase offset feeds the
/// controller's Phase parameter, which the blend tree state binds as its
/// cycle offset.
///
/// Each walk variant also carries its measured natural (authored root-motion)
/// speed. A per-peer driver keeps the locomotion state's SpeedMul parameter
/// at intendedSpeed / naturalSpeed for the active variant, so every gait's
/// cadence matches the zombie's actual travel instead of one shared rate:
/// the host/local peer reads intent from the FSM context (chase vs commanded
/// walk), remote clients derive the rate from the replicated position delta.
/// Nothing about the rate is replicated — it is recomputed locally from data
/// every peer already has (variant seed, data speeds, replicated motion).
/// </summary>
public class ZombieAnimationVariant : NetworkBehaviour
{
    [Header("Base clips (the locomotion blend tree slots being replaced)")]
    [SerializeField] private AnimationClip _baseIdleClip;
    [SerializeField] private AnimationClip _baseWalkClip;

    [Header("Variant clips (Kimodo set)")]
    [SerializeField] private AnimationClip[] _walkVariants;
    [SerializeField] private AnimationClip[] _idleVariants;

    [Header("Natural (authored root-motion) speeds, m/s — measured per clip")]
    [Tooltip("Natural travel speed of the base walk clip.")]
    [SerializeField] private float _baseWalkNaturalSpeed = 0.325f;
    [Tooltip("Parallel to the walk variants: each clip's measured natural travel speed.")]
    [SerializeField] private float[] _walkNaturalSpeeds;

    public const float MinRate = 0.5f;
    public const float MaxRate = 2.0f;
    private const float IdleSpeedThreshold = 0.05f;
    private const float RateDeadband = 0.03f;
    private const float RateWriteInterval = 0.1f;

    private static readonly int SpeedMulHash = Animator.StringToHash("SpeedMul");
    private static readonly int PhaseHash = Animator.StringToHash("Phase");

    private readonly NetworkVariable<int> _seed = new NetworkVariable<int>(0);

    private Animator _animator;
    private ZombieBehavior _behavior;
    private bool _applied;
    private float _naturalSpeed;
    private float _currentRate = 1f;
    private float _lastWrittenRate = -1f;
    private float _writeTimer;
    private Vector3 _lastPosition;
    private float _smoothedSpeed;

    /// <summary>
    /// Pure derivation, identical on every peer: seed → variant indices
    /// and a phase in [0, 1]. An index is -1 when no variants of that kind
    /// are configured.
    /// </summary>
    public static void Derive(int seed, int walkCount, int idleCount, out int walkIndex, out int idleIndex, out float phase)
    {
        unchecked
        {
            uint hash = (uint)seed * 2654435761u; // Knuth multiplicative hash
            walkIndex = walkCount > 0 ? (int)(hash % (uint)walkCount) : -1;
            idleIndex = idleCount > 0 ? (int)((hash >> 8) % (uint)idleCount) : -1;
            phase = ((hash >> 16) & 0xFFFF) / 65535f;
        }
    }

    /// <summary>
    /// Pure rate computation: the playback rate that makes a clip authored
    /// at <paramref name="naturalSpeed"/> match <paramref name="speed"/>.
    /// A (near-)stationary zombie gets the neutral rate so its idle plays at
    /// its authored cadence; the rate is clamped so no gait is ever played
    /// slower than half or faster than double its authored speed.
    /// </summary>
    public static float ComputeRate(float speed, float naturalSpeed)
    {
        if (naturalSpeed <= 0f || speed < IdleSpeedThreshold)
        {
            return 1f;
        }
        return Mathf.Clamp(speed / naturalSpeed, MinRate, MaxRate);
    }

    /// <summary>
    /// Pure lookup: the natural speed of a walk variant index, falling back
    /// to the base clip's speed for the no-variant case or a table that is
    /// missing/mismatched (a misconfigured table must never zero the rate).
    /// </summary>
    public static float NaturalSpeedFor(int walkIndex, float[] naturalSpeeds, float baseNaturalSpeed)
    {
        if (walkIndex >= 0 && naturalSpeeds != null && walkIndex < naturalSpeeds.Length && naturalSpeeds[walkIndex] > 0f)
        {
            return naturalSpeeds[walkIndex];
        }
        return baseNaturalSpeed;
    }

    public override void OnNetworkSpawn()
    {
        _animator = GetComponent<Animator>();
        PublishNaturalSpeed(_baseWalkNaturalSpeed);

        if (IsServer && _seed.Value == 0)
        {
            _seed.Value = Random.Range(1, int.MaxValue);
        }

        _seed.OnValueChanged += HandleSeedChanged;
        if (_seed.Value != 0)
        {
            Apply(_seed.Value);
        }
    }

    public override void OnNetworkDespawn()
    {
        _seed.OnValueChanged -= HandleSeedChanged;
    }

    private void Start()
    {
        // Pure local play (no NetworkManager in the scene, e.g. the
        // single-player arena): OnNetworkSpawn never fires, so roll and
        // apply locally — the variant stays a visual-only choice either way.
        if (NetworkManager.Singleton == null && !_applied)
        {
            _animator = GetComponent<Animator>();
            Apply(Random.Range(1, int.MaxValue));
        }
    }

    private void HandleSeedChanged(int previousValue, int newValue)
    {
        if (newValue != 0)
        {
            Apply(newValue);
        }
    }

    private void Apply(int seed)
    {
        if (_applied || _animator == null || _baseIdleClip == null || _baseWalkClip == null)
        {
            return;
        }

        Derive(seed,
            _walkVariants != null ? _walkVariants.Length : 0,
            _idleVariants != null ? _idleVariants.Length : 0,
            out int walkIndex, out int idleIndex, out float phase);

        var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
        if (idleIndex >= 0)
        {
            overrides.Add(new KeyValuePair<AnimationClip, AnimationClip>(_baseIdleClip, _idleVariants[idleIndex]));
        }
        if (walkIndex >= 0)
        {
            overrides.Add(new KeyValuePair<AnimationClip, AnimationClip>(_baseWalkClip, _walkVariants[walkIndex]));
        }
        if (overrides.Count == 0)
        {
            return;
        }

        var overrideController = new AnimatorOverrideController(_animator.runtimeAnimatorController);
        overrideController.ApplyOverrides(overrides);
        _animator.runtimeAnimatorController = overrideController;
        _animator.SetFloat(PhaseHash, phase);
        _naturalSpeed = NaturalSpeedFor(walkIndex, _walkNaturalSpeeds, _baseWalkNaturalSpeed);
        PublishNaturalSpeed(_naturalSpeed);
        _lastPosition = transform.position;
        _applied = true;
    }

    // The chase state caps the agent's speed by the active gait's natural
    // speed; publish it into the shared context (base speed until a variant
    // is applied). Peers that never run the FSM simply never read it.
    private void PublishNaturalSpeed(float naturalSpeed)
    {
        if (_behavior == null)
        {
            _behavior = GetComponent<ZombieBehavior>();
        }
        if (_behavior != null && _behavior._context != null)
        {
            _behavior._context.locomotionNaturalSpeed = naturalSpeed;
        }
    }

    private void Update()
    {
        if (!_applied || _animator == null || !_animator.isActiveAndEnabled)
        {
            return;
        }

        if (_behavior == null)
        {
            _behavior = GetComponent<ZombieBehavior>();
        }

        // This peer's rendered velocity from position deltas (works on every
        // peer; on clients it is the velocity of the replicated motion).
        float deltaTime = Time.deltaTime;
        Vector3 position = transform.position;
        if (deltaTime > 0f)
        {
            Vector3 delta = position - _lastPosition;
            delta.y = 0f;
            float instantSpeed = delta.magnitude / deltaTime;
            _smoothedSpeed = Mathf.Lerp(_smoothedSpeed, instantSpeed, 1f - Mathf.Exp(-deltaTime / 0.15f));
        }
        _lastPosition = position;

        float targetRate = ComputeTargetRate();
        _currentRate = Mathf.MoveTowards(_currentRate, targetRate, deltaTime * 4f);

        // Gated writes: the value is a per-frame float, so only push it into
        // the animator when it actually moved — NetworkAnimator syncs changed
        // parameters, and a churned float would tax every zombie each tick.
        _writeTimer -= deltaTime;
        if (_writeTimer <= 0f && Mathf.Abs(_currentRate - _lastWrittenRate) > RateDeadband)
        {
            _animator.SetFloat(SpeedMulHash, _currentRate);
            _lastWrittenRate = _currentRate;
            _writeTimer = RateWriteInterval;
        }
    }

    private float ComputeTargetRate()
    {
        // Authority/local peer: intent comes from the live FSM context — a
        // detected target means the chase speed, a commanded destination the
        // walk speed, neither means idle (neutral rate).
        if (_behavior != null && _behavior.enabled && _behavior.zombieData != null)
        {
            var context = _behavior._context;
            if (context.target != null)
            {
                return ComputeRate(_behavior.zombieData.chaseSpeed, _naturalSpeed);
            }
            if (context.moveDestination != null)
            {
                return ComputeRate(_behavior.zombieData.walkSpeed, _naturalSpeed);
            }
            return 1f;
        }

        // Remote client: the behavior is disabled there, so match the rate
        // to the velocity the replicated motion actually renders at.
        return ComputeRate(_smoothedSpeed, _naturalSpeed);
    }
}
