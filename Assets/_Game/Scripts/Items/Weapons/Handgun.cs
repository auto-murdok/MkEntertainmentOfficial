using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

public class Handgun : StateMachine<HandgunState, HandgunContext>, IFirearm
{
    [Header("Transforms")]
    [SerializeField] private GameObject _bulletPrefab;
    [SerializeField] private Transform _shootPoint;
    private FirearmEvents _firearmEvents; public FirearmEvents fireArmEvents { get { return _firearmEvents; } }

    // Amount of positional kick applied to the weapon when firing.
    private const int GunKickAmount = 5;

    // Fallback cadence used until Prepare() supplies the weapon's fire rate.
    private const float DefaultFireRate = 0.05f;

    // Distance the projectile starts ahead of the muzzle, so it spawns clear
    // of the gun geometry.
    private const float MuzzleExitOffset = 0.1f;

    // Projectile pool: instantiated bullets are recycled instead of destroyed
    // (Unity Manual: "Pooling and reusing objects" — official projectile pattern).
    private ObjectPool<BulletProjectile> _bulletPool;

    private void Awake()
    {
        states[HandgunState.Ready] = new HandgunReadyState();
        states[HandgunState.Shooting] = new HandgunShootingState();
        states[HandgunState.Reloading] = new HandgunReloadingState();

        _context.animator = gameObject.GetComponent<Animator>();
        _context.gunKick = GunKickAmount;
        _context.fireRate = DefaultFireRate;
        if (debugStateMachine)
        {
            OnStateChanged += state => Debug.Log($"[{gameObject.name}] -> {state}");
        }

        _bulletPool = new ObjectPool<BulletProjectile>(
            CreateProjectile,
            null,
            OnReleaseToPool,
            OnDestroyPooledObject,
            collectionCheck: true,
            defaultCapacity: 10,
            maxSize: 100);
    }

    private BulletProjectile CreateProjectile()
    {
        BulletProjectile projectile = Instantiate(_bulletPrefab).GetComponent<BulletProjectile>();
        projectile.objectPool = _bulletPool;
        // Bullets live dormant in the pool between shots.
        projectile.gameObject.SetActive(false);
        return projectile;
    }

    private void OnReleaseToPool(BulletProjectile pooledObject)
    {
        pooledObject.gameObject.SetActive(false);
    }

    private void OnDestroyPooledObject(BulletProjectile pooledObject)
    {
        Destroy(pooledObject.gameObject);
    }

    public void Prepare(int clipSize, int reserveAmmo)
    {
        _context.maxClipSize = clipSize;
        _context.clipSize = clipSize;
        _context.reserveAmmo = reserveAmmo;
    }

    public void SetFireRate(float fireRate)
    {
        _context.fireRate = fireRate > 0f ? fireRate : DefaultFireRate;
    }

    // Live-bullet count straight from the pool (checked-out objects) — lets the
    // DebugHud read it without a per-refresh FindObjectsByType scene scan.
    public int liveBullets => _bulletPool != null ? _bulletPool.CountActive : 0;

    public void InjectUIController(CharacterUIController uiController)
    {
        // UI references are injected by the composition root (equip site) — the
        // weapon must never go looking for scene objects itself.
        _context.UIController = uiController;
    }

    // AIM-FIRST BEHAVIOUR: the shot direction is the stored aim vector toward
    // the world aim point (CharacterLocomotion._aimTarget), which is only
    // meaningful while the player is aiming — the aim camera/ray drives it.
    // When shooting without aiming, the weapon's rest pose points DOWN, so the
    // muzzle-forward fallback naturally sends the bullet into the ground. That
    // is intended: fire first, then shoot.
    public void Shoot(Vector3 mouseWorldPosition)
    {
        if (!_context.isReloading && !_context.isTriggerPressed)
        {
            Vector3 shootPos = _shootPoint != null ? _shootPoint.position : transform.position;
            Vector3 diff = mouseWorldPosition - shootPos;
            Vector3 forward = _shootPoint != null ? _shootPoint.forward : transform.forward;
            _context.aimDirection = diff.sqrMagnitude > 0.001f ? diff.normalized : forward;
            _context.isTriggerPressed = true;
        }
    }

    public void TriggerReload()
    {
        if (_context.clipSize < _context.maxClipSize && _context.reserveAmmo > 0)
        {
            _context.isReloading = true;
        }
    }

    // Returns true only when a projectile was actually launched (used to gate
    // the onShoot event so recoil never plays on a dry fire).
    public bool ExecuteActualShoot()
    {
        if (_bulletPrefab == null) return false;

        Vector3 spawnPos = _shootPoint != null ? _shootPoint.position : transform.position;
        Vector3 forward = _shootPoint != null ? _shootPoint.forward : transform.forward;
        Vector3 direction = _context.aimDirection.sqrMagnitude > 0.001f ? _context.aimDirection : forward;

        // Nudge the spawn point out of the muzzle so the bullet does not start
        // embedded in the gun geometry itself.
        spawnPos += direction * MuzzleExitOffset;

        BulletProjectile bullet = _bulletPool.Get();
        bullet.Launch(spawnPos, Quaternion.LookRotation(direction, Vector3.up), transform.root.gameObject);
        CombatLog.ReportImpact($"Bullet launched from {spawnPos:F2} dir {direction:F2}");
        return true;
    }

    public void RegisterEvents(FirearmEvents fireArmEvents)
    {
        _firearmEvents = fireArmEvents;
    }
}
