using UnityEngine;
using UnityEngine.Pool;

public class BulletProjectile : MonoBehaviour
{
    [Tooltip("Distance (metres) the projectile may travel before it is released back to the pool.")]
    [SerializeField] private float _maxTravelDistance = 30f;
    [SerializeField] private float _damage = 25f;

    private const float ProjectileSpeed = 50f;

    private float _maxTravelDistanceSqr;

    private Rigidbody _bulletRigidbody;
    private Collider _collider;
    private Vector3 _initialPosition;
    private IObjectPool<BulletProjectile> _objectPool;

    // Release guard: one bullet can report several OnCollisionEnter callbacks
    // in the same physics step (one per contact pair), but it may only go back
    // to the pool once.
    private bool _isReleased;
    // Single-hit guard: a body crossing several overlapping limb colliders in
    // one physics step must score exactly one damage event per flight.
    private bool _hasHit;
    // Owner collider cache: the shooter never changes between flights, so the
    // rig scan runs once instead of on every Launch.
    private GameObject _cachedOwner;
    private Collider[] _cachedOwnerColliders;

    void Awake()
    {
        _bulletRigidbody = GetComponent<Rigidbody>();
        _collider = GetComponent<Collider>();
        _maxTravelDistanceSqr = Mathf.Max(1f, _maxTravelDistance) * Mathf.Max(1f, _maxTravelDistance);
        // Speculative CCD: swept contacts are generated against ALL body types
        // (static, kinematic, dynamic). ContinuousDynamic skips kinematic
        // bodies, which let fast bullets tunnel through ragdoll limbs.
        _bulletRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
    }

    // Called by the pool owner every time this projectile is handed out.
    // Damage is owned by the projectile prefab itself (serialized _damage),
    // not overridden per shot.
    public void Launch(Vector3 position, Quaternion rotation, GameObject owner)
    {
        // Teleport the rigidbody directly: a transform-only move on a freshly
        // re-activated body can leave the physics pose at the pool's creation
        // point (observed: bullets "hitting" the ground at the origin).
        _bulletRigidbody.position = position;
        _bulletRigidbody.rotation = rotation;
        transform.SetPositionAndRotation(position, rotation);
        _initialPosition = position;
        _isReleased = false;
        _hasHit = false;

        // Activate only after the pose is corrected: a pooled body re-activates
        // at its dormant pose (often inside whatever it last hit), and Unity
        // registers that stale overlap as a contact — the next shot would
        // instantly die on the shooter's old collision. Reactivating at the
        // corrected pose registers a clean broadphase entry instead.
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }
        _bulletRigidbody.linearVelocity = transform.forward * ProjectileSpeed;

        // The projectile spawns inside the shooter's rig, so it must never
        // collide with the owner (ragdoll bones and CharacterController
        // included). Without this the bullet dies on the shooter at frame 1.
        if (owner != null)
        {
            if (owner != _cachedOwner)
            {
                _cachedOwner = owner;
                _cachedOwnerColliders = owner.GetComponentsInChildren<Collider>(true);
            }
            foreach (Collider ownerCollider in _cachedOwnerColliders)
            {
                Physics.IgnoreCollision(_collider, ownerCollider, true);
            }
        }

#if UNITY_EDITOR
        Debug.DrawLine(position, position + transform.forward * 2f, Color.yellow, 1f);
#endif
    }

    // Pool reference injected by the owning firearm at creation time.
    public IObjectPool<BulletProjectile> objectPool
    {
        set => _objectPool = value;
    }

    // Idempotent (guarded by _isReleased) so test code can release a live bullet
    // to verify pool bookkeeping.
    public void ReleaseToPool()
    {
        if (_isReleased) return;
        _isReleased = true;

        // Reset rigidbody state before going back into the pool so the next
        // launch starts from a clean slate (Unity official pooling pattern).
        _bulletRigidbody.linearVelocity = Vector3.zero;
        _bulletRigidbody.angularVelocity = Vector3.zero;

        if (_objectPool != null)
        {
            _objectPool.Release(this);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Update()
    {
        if ((transform.position - _initialPosition).sqrMagnitude > _maxTravelDistanceSqr)
        {
            CombatLog.ReportImpact($"Bullet max-range release at {transform.position:F1}");
            ReleaseToPool();
        }
    }

    private void OnCollisionEnter(Collision other)
    {
        // Later contact callbacks (same physics step, same body) after the
        // scoring hit are ignored — the projectile is already spent/released.
        if (_isReleased || _hasHit)
        {
            return;
        }

        // Stale-contact guard: a pooled body re-activated for its next flight
        // can report a residual contact pair from the previous flight (the
        // contact lies BEHIND the travelling bullet). A valid hit for a
        // forward-only projectile is always ahead of it, so reject contacts
        // behind the current pose instead of scoring a phantom hit.
        Vector3 velocity = _bulletRigidbody.linearVelocity;
        if (velocity.sqrMagnitude > 0.01f)
        {
            Vector3 point = other.GetContact(0).point;
            if (Vector3.Dot(point - transform.position, velocity) < 0f)
            {
                return;
            }
        }

        _hasHit = true;

        IDamageable damageable = other.gameObject.GetComponentInParent<IDamageable>();
        if (damageable != null)
        {
            using (CombatLog.BeginSource("Bullet"))
            {
                damageable.TakeDamage(_damage);
            }
        }
        else
        {
            Vector3 point = other.GetContact(0).point;
            CombatLog.ReportImpact($"Bullet hit {other.gameObject.name} [{LayerMask.LayerToName(other.gameObject.layer)}] at {point:F2} — no IDamageable");
        }

        ReleaseToPool();
    }
}
