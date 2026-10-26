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
/// </summary>
public class ZombieAnimationVariant : NetworkBehaviour
{
    [Header("Base clips (the locomotion blend tree slots being replaced)")]
    [SerializeField] private AnimationClip _baseIdleClip;
    [SerializeField] private AnimationClip _baseWalkClip;

    [Header("Variant clips (Kimodo set)")]
    [SerializeField] private AnimationClip[] _walkVariants;
    [SerializeField] private AnimationClip[] _idleVariants;

    private readonly NetworkVariable<int> _seed = new NetworkVariable<int>(0);

    private Animator _animator;
    private bool _applied;

    /// <summary>
    /// Authored ground speed (m/s) of the walk clip currently in the
    /// locomotion blend tree — the base clip until a variant is applied.
    /// Speed matching (ZombieAnimatorUtils.ComputeSpeedMul) divides the
    /// data-intended pace by this so every gait covers ground at the
    /// same intended speed instead of its own raw clip speed.
    /// </summary>
    public float CurrentWalkAuthoredSpeed { get; private set; } = 1f;

    /// <summary>Horizontal ground speed a clip's root motion authors.</summary>
    public static float AuthoredSpeed(AnimationClip clip)
    {
        if (clip == null)
        {
            return 0f;
        }
        Vector3 average = clip.averageSpeed;
        return new Vector2(average.x, average.z).magnitude;
    }

    private void Awake()
    {
        if (_baseWalkClip != null)
        {
            CurrentWalkAuthoredSpeed = AuthoredSpeed(_baseWalkClip);
        }
    }

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

    public override void OnNetworkSpawn()
    {
        _animator = GetComponent<Animator>();

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
            CurrentWalkAuthoredSpeed = AuthoredSpeed(_walkVariants[walkIndex]);
        }
        if (overrides.Count == 0)
        {
            return;
        }

        var overrideController = new AnimatorOverrideController(_animator.runtimeAnimatorController);
        overrideController.ApplyOverrides(overrides);
        _animator.runtimeAnimatorController = overrideController;
        _animator.SetFloat("Phase", phase);
        _applied = true;
    }
}
