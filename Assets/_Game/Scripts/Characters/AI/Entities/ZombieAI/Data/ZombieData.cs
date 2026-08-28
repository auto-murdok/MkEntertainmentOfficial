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
    [SerializeField] private float _corpseDestroyDelay = 5f;

    [Header("Detection & Senses")]
    [SerializeField] private float _detectionMaxDistance = 5f;
    [SerializeField] private int _minDetectionAngle = 100;
    [SerializeField] private int _maxDetectionAngle = 180;
    [SerializeField] private LayerMask _detectionLayerMask;
    [SerializeField] private LayerMask _ignoreLayerMask;

    [Header("Combat & Sizing")]
    [Tooltip("Distance at which the zombie bites. ALSO the separation threshold for re-biting: once the victim is farther than this after a push-off, recentlyBitten clears and a new bite can start after the cooldown.")]
    [SerializeField] private float _biteRange = 1.2f;
    [Tooltip("Length of the bite in seconds (zombie side). The grab/push-off split is ReleaseFraction in ZombieBitingState.")]
    [SerializeField] private float _biteDuration = 1.5f;
    [Tooltip("NavMeshAgent radius used while roaming / chasing.")]
    [SerializeField] private float _defaultAgentRadius = 0.3f;
    [Tooltip("NavMeshAgent radius while biting (kept small so the zombie hugs the victim).")]
    [SerializeField] private float _bittingAgentRadius = 0.1f;

    [Header("Locomotion")]
    [SerializeField] private float _walkSpeed = 1.5f;
    [SerializeField] private float _chaseSpeed = 3.5f;

    // Public Getters
    public string zombieTypeName => _zombieTypeName;
    public AnimatorOverrideController animatorOverride => _animatorOverride;
    public float maxHitPoints => _maxHitPoints;
    public float biteDamage => _biteDamage;
    public float corpseDestroyDelay => _corpseDestroyDelay;
    public float detectionMaxDistance => _detectionMaxDistance;
    public int minDetectionAngle => _minDetectionAngle;
    public int maxDetectionAngle => _maxDetectionAngle;
    public LayerMask detectionLayerMask => _detectionLayerMask;
    public LayerMask ignoreLayerMask => _ignoreLayerMask;
    public float biteRange => _biteRange;
    public float biteDuration => _biteDuration;
    public float defaultAgentRadius => _defaultAgentRadius;
    public float bittingAgentRadius => _bittingAgentRadius;
    public float walkSpeed => _walkSpeed;
    public float chaseSpeed => _chaseSpeed;
}
