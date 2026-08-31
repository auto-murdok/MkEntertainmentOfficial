using UnityEngine;

/// <summary>
/// Immutable weapon definition — the single source of truth for all tunable
/// ballistics/presentation. Designers author one per weapon family (Pistol,
/// Rifle, Shotgun) under Assets/_Game/Data/Weapons/. No Weapon MonoBehaviour
/// field duplicates these values.
/// </summary>
[CreateAssetMenu(fileName = "WeaponDefinition", menuName = "Game/Weapon Definition")]
public sealed class WeaponDefinition : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string _id = "SM_Gun_Pistol";
    public string id => _id;

    [Header("Ballistics")]
    [Tooltip("Server-authoritative damage per pellet/projectile.")]
    [SerializeField] private float _damage = 25f;
    public float damage => _damage;

    [Tooltip("Max hitscan range in metres.")]
    [SerializeField] private float _range = 80f;
    public float range => _range;

    [Tooltip("Seconds between shots (rechambering). Smaller = higher ROF.")]
    [SerializeField] private float _fireRate = 0.2f;
    public float fireRate => _fireRate;

    [Tooltip("Base spread in degrees (cone half-angle).")]
    [SerializeField] private float _baseSpreadDegrees = 0.8f;
    public float baseSpreadDegrees => _baseSpreadDegrees;

    [Tooltip("Pellet count per trigger pull (1 for pistol/rifle, 8 for shotgun).")]
    [SerializeField] private int _pelletCount = 1;
    public int pelletCount => Mathf.Max(1, _pelletCount);

    [Header("Fire Mode")]
    [SerializeField] private WeaponFireMode _fireMode = WeaponFireMode.Semi;
    public WeaponFireMode fireMode => _fireMode;
    [SerializeField] private int _burstCount = 3;
    public int burstCount => Mathf.Max(2, _burstCount);

    [Header("Ammo")]
    [SerializeField] private AmmoType _ammoType = AmmoType.NineMm;
    public AmmoType ammoType => _ammoType;
    [SerializeField] private int _clipSize = 12;
    public int clipSize => Mathf.Max(1, _clipSize);
    [SerializeField] private int _defaultReserve = 36;
    public int defaultReserve => Mathf.Max(0, _defaultReserve);
    [SerializeField] private float _reloadDuration = 1.6f;
    public float reloadDuration => Mathf.Max(0.2f, _reloadDuration);

    [Header("Recoil / Bloom")]
    [Tooltip("Scalar passed to RigRecoil. Visual only, not ballistics.")]
    [SerializeField] private float _recoilForce = 5f;
    public float recoilForce => _recoilForce;
    [Tooltip("Bloom added per shot in degrees, recovered over bloomRecovery seconds.")]
    [SerializeField] private float _bloomPerShot = 0.6f;
    public float bloomPerShot => _bloomPerShot;
    [SerializeField] private float _bloomRecovery = 4f;
    public float bloomRecovery => _bloomRecovery;

    [Header("Presentation")]
    [Tooltip("Optional tracer visual (no collider/damage) spawned for hitscan feedback.")]
    [SerializeField] private GameObject _tracerPrefab;
    public GameObject tracerPrefab => _tracerPrefab;
}
