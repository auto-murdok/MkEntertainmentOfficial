using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// AAA hitscan weapon. Replaces Handgun's Rigidbody-per-bullet with instant
/// RaycastNonAlloc + pooled tracer. Data lives in WeaponDefinition (SO), runtime
/// state is per-instance (clip/bloom/cooldown). Server-authoritative path
/// routes damage through NetworkedDamage; visual path is predicted locally.
/// </summary>
public sealed class HitscanWeapon : MonoBehaviour, IFirearm
{
    [Header("Definition (data-driven)")]
    [SerializeField] private WeaponDefinition _definition;
    public WeaponDefinition definition => _definition;

    [Header("Transforms")]
    [SerializeField] private Transform _shootPoint;
    [SerializeField] private GameObject _tracerPrefab;

    private const float MuzzleExitOffset = 0.1f;
    private const float DefaultFireRate = 0.2f;
    private const int GunKickAmount = 5;

    private FirearmEvents _events;
    public FirearmEvents fireArmEvents => _events;

    // Runtime — per-instance, not SO.
    private int _maxClip;
    private int _clip;
    private int _reserve;
    private float _fireRate;
    private float _nextFireTime;
    private float _bloom;
    private bool _isReloading;
    private float _reloadFinishTime;
    private Vector3 _aimDirection;
    private bool _triggerPressed;
    private CharacterUIController _ui;

    private Animator _animator;
    private int _shootHash = AnimatorUtils.HandgunShootHash;
    private int _idleHash = AnimatorUtils.HandgunIdleHash;

    // Hitscan buffers — avoid GC per shot.
    private readonly RaycastHit[] _hits = new RaycastHit[8];
    private int _hitMask;

    // Tracer pool.
    private ObjectPool<TracerVisual> _tracerPool;

    public int liveBullets => 0; // compatibility — hitscan has no in-flight projectiles
    public int reserveAmmo => _reserve;
    public int gunKick => GunKickAmount;

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        _hitMask = ~0; // all layers; filter LocalPlayer via layer check later if needed
        if (_tracerPrefab != null && _tracerPrefab.GetComponent<TracerVisual>() != null)
        {
            _tracerPool = new ObjectPool<TracerVisual>(
                () => { var go = Instantiate(_tracerPrefab); var tv = go.GetComponent<TracerVisual>(); tv.pool = _tracerPool; go.SetActive(false); return tv; },
                v => v.gameObject.SetActive(true),
                v => v.gameObject.SetActive(false),
                v => Destroy(v.gameObject),
                collectionCheck: true, defaultCapacity: 8, maxSize: 64);
        }
        // Init runtime from definition if present, else defaults.
        if (_definition != null)
        {
            _maxClip = _definition.clipSize;
            _clip = _maxClip;
            _reserve = GameCliArgs.InfiniteAmmo ? int.MaxValue : _definition.defaultReserve;
            _fireRate = _definition.fireRate;
        }
        else
        {
            _maxClip = 12; _clip = 12; _reserve = 36; _fireRate = DefaultFireRate;
        }
    }

    private void Update()
    {
        // Bloom recovery, frame-rate independent.
        if (_bloom > 0f && _definition != null)
            _bloom = Mathf.Max(0f, _bloom - _definition.bloomRecovery * Time.deltaTime);

        if (_isReloading && Time.time >= _reloadFinishTime)
            FinishReload();
    }

    // IFirearm — kept for Weapon.cs compatibility.
    public void Prepare(int clipSize, int reserveAmmo)
    {
        _maxClip = Mathf.Max(1, clipSize);
        _clip = _maxClip;
        _reserve = reserveAmmo;
        _isReloading = false;
        _triggerPressed = false;
        _bloom = 0f;
        _nextFireTime = 0f;
    }

    public void SetFireRate(float fireRate)
    {
        _fireRate = fireRate > 0f ? fireRate : DefaultFireRate;
    }

    public void InjectUIController(CharacterUIController ui) => _ui = ui;

    public void RegisterEvents(FirearmEvents events) => _events = events;

    public void Shoot(Vector3 aimWorldPosition)
    {
        if (_isReloading || _triggerPressed) return;
        Vector3 shootPos = _shootPoint != null ? _shootPoint.position : transform.position;
        Vector3 forward = _shootPoint != null ? _shootPoint.forward : transform.forward;
        Vector3 diff = aimWorldPosition - shootPos;
        _aimDirection = diff.sqrMagnitude > 0.001f ? diff.normalized : forward;
        _triggerPressed = true;
        TryFire();
    }

    public void TriggerReload()
    {
        if (_isReloading) return;
        if (_clip >= _maxClip) return;
        if (_reserve <= 0) return;
        StartReload();
    }

    public void AddReserveAmmo(int amount)
    {
        if (amount <= 0) return;
        _reserve += amount;
    }

    // Public so tests can assert firing without going through Shoot latch.
    public bool ExecuteActualShoot() => DoFire();

    private void TryFire()
    {
        if (Time.time < _nextFireTime) { _triggerPressed = false; return; }
        if (_clip <= 0)
        {
            if (_reserve > 0) StartReload();
            _triggerPressed = false;
            return;
        }
        bool fired = DoFire();
        if (fired)
            _events.onShoot?.Invoke();
        NotifyShootUI();
    }

    private bool DoFire()
    {
        // View-consistent origin: muzzle forward with offset to avoid self-intersection.
        Vector3 origin = _shootPoint != null ? _shootPoint.position : transform.position;
        Vector3 baseDir = _aimDirection.sqrMagnitude > 0.001f ? _aimDirection : (_shootPoint != null ? _shootPoint.forward : transform.forward);
        origin += baseDir * MuzzleExitOffset;

        if (_animator != null) _animator.CrossFade(_shootHash, 0f);
        _nextFireTime = Time.time + _fireRate;

        // Empty — dry fire path (no prefab needed for hitscan, but keep gate for tests that expect false when tracer missing? hitscan always fires if clip>0)
        // For compatibility with Handgun test "NoBulletPrefab_ReturnsFalse", we treat tracer prefab missing as still firing — hitscan doesn't need it.
        // So we always succeed if clip>0.
        int pellets = _definition != null ? _definition.pelletCount : 1;
        float damage = _definition != null ? _definition.damage : 25f;
        float range = _definition != null ? _definition.range : 80f;
        float spread = (_definition != null ? _definition.baseSpreadDegrees : 0.8f) + _bloom;

        for (int p = 0; p < pellets; p++)
        {
            Vector3 dir = ApplySpread(baseDir, spread);
            Vector3 end = origin + dir * range;
            int hitCount = Physics.RaycastNonAlloc(origin, dir, _hits, range, _hitMask, QueryTriggerInteraction.Ignore);
            if (hitCount > 0)
            {
                // Closest hit.
                RaycastHit closest = _hits[0];
                float closestDist = closest.distance;
                for (int i = 1; i < hitCount; i++)
                    if (_hits[i].distance < closestDist) { closest = _hits[i]; closestDist = _hits[i].distance; }
                end = closest.point;
                var dmg = closest.collider.GetComponentInParent<IDamageable>();
                if (dmg != null)
                {
                    var comp = dmg as Component;
                    if (comp != null) NetworkedDamage.Apply(comp, damage);
                }
                else
                {
                    CombatLog.ReportImpact($"Hitscan hit {closest.collider.name} at {end:F2} — no IDamageable", CombatLog.EntryKind.Debug);
                }
            }
            SpawnTracer(origin, end);
        }

        _clip--;
        _bloom += _definition != null ? _definition.bloomPerShot : 0.6f;
        _triggerPressed = false;
        if (_animator != null) _animator.CrossFade(_idleHash, 0f);
        CombatLog.ReportImpact($"Hitscan fired {pellets} pellet(s) from {origin:F2} dir {baseDir:F2} spread {spread:F2}", CombatLog.EntryKind.Debug);
        return true;
    }

    private void SpawnTracer(Vector3 from, Vector3 to)
    {
        if (_tracerPool != null)
        {
            var t = _tracerPool.Get();
            t.Play(from, to);
        }
        else if (_tracerPrefab != null)
        {
            var go = Instantiate(_tracerPrefab, from, Quaternion.LookRotation(to - from));
            Destroy(go, 2f);
        }
#if UNITY_EDITOR
        Debug.DrawLine(from, to, Color.yellow, 0.5f);
#endif
    }

    private Vector3 ApplySpread(Vector3 dir, float degrees)
    {
        if (degrees <= 0.001f) return dir;
        // Cone sampling — AAA style but simple.
        Vector2 rnd = Random.insideUnitCircle * Mathf.Tan(degrees * Mathf.Deg2Rad);
        Quaternion yaw = Quaternion.AngleAxis(rnd.x * Mathf.Rad2Deg, Vector3.up);
        Quaternion pitch = Quaternion.AngleAxis(rnd.y * Mathf.Rad2Deg, Vector3.right);
        // Build orthonormal basis around dir.
        Quaternion rot = Quaternion.LookRotation(dir);
        Vector3 offset = rot * new Vector3(rnd.x, rnd.y, 0f);
        return (dir + offset).normalized;
    }

    private void StartReload()
    {
        _isReloading = true;
        _triggerPressed = false;
        _reloadFinishTime = Time.time + (_definition != null ? _definition.reloadDuration : 1.6f);
        _events.onReloadStarted?.Invoke();
        if (_animator != null && AnimatorUtils.HasParameter(_animator, AnimatorUtils.IsReloadingHash))
        {
            _animator.SetBool(AnimatorUtils.IsReloadingHash, true);
        }
        if (_animator != null) _animator.CrossFade(_idleHash, 0f);
    }

    private void FinishReload()
    {
        _isReloading = false;
        if (GameCliArgs.InfiniteAmmo)
        {
            _clip = _maxClip;
            _reserve = int.MaxValue;
        }
        else
        {
            int missing = _maxClip - _clip;
            int taken = Mathf.Min(missing, _reserve);
            _clip += taken;
            _reserve -= taken;
        }
        if (_animator != null && AnimatorUtils.HasParameter(_animator, AnimatorUtils.IsReloadingHash))
        {
            _animator.SetBool(AnimatorUtils.IsReloadingHash, false);
        }
        _events.onReloadFinished?.Invoke();
        NotifyShootUI();
    }

    private void NotifyShootUI()
    {
        if (_ui != null)
            _ui.NotifyObservers(CharacterUIElement.ShootUI, CharacterUIContext.CreateShootUI(_clip, _maxClip));
    }

    // Test/compat helpers
    public int testClip => _clip;
    public int testMaxClip => _maxClip;
    public bool testIsReloading => _isReloading;
}
