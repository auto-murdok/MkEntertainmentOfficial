using UnityEngine;
using UnityEngine.Pool;

public class BulletProjectile : MonoBehaviour
{
    private const float MaxTravelDistance = 30f;
    private const float MaxTravelDistanceSqr = MaxTravelDistance * MaxTravelDistance;
    private const float ProjectileSpeed = 50f;

    [SerializeField] private float _damage = 25f;

    private Rigidbody _bulletRigidbody;
    private Collider _collider;
    private Vector3 _initialPosition;
    private IObjectPool<BulletProjectile> _objectPool;

    // Release guard: one bullet can report several OnCollisionEnter callbacks
    // in the same physics step (one per contact pair), but it may only go back
    // to the pool once.
    private bool _isReleased;

    void Awake()
    {
        _bulletRigidbody = GetComponent<Rigidbody>();
        _collider = GetComponent<Collider>();
        // CCD is a property of the rigidbody: configure it once here (at pool
        // creation), never per shot.
        _bulletRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
    }

    // Called by the pool owner every time this projectile is handed out.
    // Damage is owned by the projectile prefab itself (serialized _damage),
    // not overridden per shot.
    public void Launch(Vector3 position, Quaternion rotation, GameObject owner)
    {
        transform.SetPositionAndRotation(position, rotation);
        _initialPosition = position;
        _isReleased = false;
        _bulletRigidbody.linearVelocity = transform.forward * ProjectileSpeed;

        // The projectile spawns inside the shooter's rig, so it must never
        // collide with the owner (ragdoll bones and CharacterController
        // included). Without this the bullet dies on the shooter at frame 1.
        if (owner != null)
        {
            foreach (Collider ownerCollider in owner.GetComponentsInChildren<Collider>(true))
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

    private void ReleaseToPool()
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
        if ((transform.position - _initialPosition).sqrMagnitude > MaxTravelDistanceSqr)
        {
            ReleaseToPool();
        }
    }

    private void OnCollisionEnter(Collision other)
    {
        IDamageable damageable = other.gameObject.GetComponentInParent<IDamageable>();
        if (damageable != null)
        {
            damageable.TakeDamage(_damage);
        }

        ReleaseToPool();
    }
}
