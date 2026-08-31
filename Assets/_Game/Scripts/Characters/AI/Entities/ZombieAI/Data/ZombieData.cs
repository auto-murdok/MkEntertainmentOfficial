using UnityEngine;

[CreateAssetMenu(fileName = "NewZombieData", menuName = "AI/Zombie Data")]
public class ZombieData : ScriptableObject
{
    [Header("General & Visuals")]
    [SerializeField] private string _zombieTypeName = "Standard Zombie";
    [SerializeField] private AnimatorOverrideController _animatorOverride;

    [Header("Health & Damage")]
    [SerializeField] private float _maxHitPoints = 100f;
    [SerializeField] private float _biteDamage = 30f;
    [SerializeField] private float _corpseDestroyDelay = 10f;

    [Header("Detection & Senses")]
    [SerializeField] private float _detectionMaxDistance = 5f;
    [Tooltip("Full forward vision-cone angle in degrees (half of it on each side of forward). 120 = a 60-degree half cone.")]
    [SerializeField] private float _fieldOfViewAngle = 120f;
    [SerializeField] private LayerMask _detectionLayerMask;
    [Tooltip("Layers that block line-of-sight (environment geometry, other actors). Empty = vision is never blocked.")]
    [SerializeField] private LayerMask _obstacleLayerMask = (LayerMask)DefaultObstacleMask;

    [Header("Combat & Sizing")]
    [Tooltip("Distance at which the zombie bites. ALSO the separation threshold for re-biting: once the victim is farther than this after a push-off, recentlyBitten clears and a new bite can start after the cooldown.")]
    [SerializeField] private float _biteRange = 1.2f;
    [Tooltip("Length of the bite in seconds (zombie side). The grab/push-off split is ReleaseFraction in ZombieBitingState.")]
    [SerializeField] private float _biteDuration = 1.5f;
    [Tooltip("NavMeshAgent radius used while roaming / chasing.")]
    [SerializeField] private float _defaultAgentRadius = 0.3f;
    [Tooltip("NavMeshAgent radius while biting (kept small so the zombie hugs the victim).")]
    [UnityEngine.Serialization.FormerlySerializedAs("_bittingAgentRadius")]
    [SerializeField] private float _bitingAgentRadius = 0.1f;

    [Header("Hand Attack (victim already pinned by another zombie)")]
    [Tooltip("Damage of the standing right-hand swing, used when the victim is locked in another zombie's bite grab.")]
    [SerializeField] private float _handAttackDamage = 15f;
    [Tooltip("Reach of the right-hand swing at the hit frame (a swipe reaches further than the grab bite).")]
    [SerializeField] private float _handAttackRange = 1.6f;
    [Tooltip("Length of the right-hand swing in seconds. The hit lands at HitFraction of this (ZombieHandAttackState).")]
    [SerializeField] private float _handAttackDuration = 1.2f;
    [Tooltip("Cooldown after a bite before the zombie may bite again.")]
    [SerializeField] private float _biteCooldown = 1.2f;
    [Tooltip("Cooldown after a hand attack before the zombie may attack again.")]
    [SerializeField] private float _handAttackCooldown = 1.5f;

    [Header("Locomotion")]
    [SerializeField] private float _walkSpeed = 1.5f;
    [SerializeField] private float _chaseSpeed = 3.5f;

    [Header("Ammo Drop (ammunition economy)")]
    [Tooltip("Pickup prefab instantiated at the corpse position on death. Null = no drop.")]
    [SerializeField] private GameObject _ammoDropPrefab;

    // Public Getters
    // Default obstacle mask: environment (Default) + Water + other zombies.
    // Matches the values previously stored in the Walker/Runner assets (which
    // excluded Default and let zombies see through arena walls).
    public const int DefaultObstacleMask = 133;

    public string zombieTypeName => _zombieTypeName;
    public AnimatorOverrideController animatorOverride => _animatorOverride;
    public float maxHitPoints => _maxHitPoints;
    public float biteDamage => _biteDamage;
    public float corpseDestroyDelay => _corpseDestroyDelay;
    public float detectionMaxDistance => _detectionMaxDistance;
    public float fieldOfViewAngle => _fieldOfViewAngle;
    public LayerMask detectionLayerMask => _detectionLayerMask;
    public LayerMask obstacleLayerMask => _obstacleLayerMask;
    public float biteRange => _biteRange;
    public float biteDuration => _biteDuration;
    public float defaultAgentRadius => _defaultAgentRadius;
    public float bitingAgentRadius => _bitingAgentRadius;
    [System.Obsolete("Typo alias — use bitingAgentRadius")]
    public float bittingAgentRadius => _bitingAgentRadius;
    public float handAttackDamage => _handAttackDamage;
    public float handAttackRange => _handAttackRange;
    public float handAttackDuration => _handAttackDuration;
    public float walkSpeed => _walkSpeed;
    public float chaseSpeed => _chaseSpeed;
    public GameObject ammoDropPrefab => _ammoDropPrefab;
    public float biteCooldown => _biteCooldown;
    public float handAttackCooldown => _handAttackCooldown;
}
