using UnityEngine;

public class BulletProjectile : MonoBehaviour
{
    private const float MaxTravelDistance = 30f;
    private const float MaxTravelDistanceSqr = MaxTravelDistance * MaxTravelDistance;
    private const float ProjectileSpeed = 50f;
    private const float DebugLineDuration = 10f;

    [SerializeField] private float _damage = 25f;

    private Rigidbody _bulletRigidbody;
    private Vector3 _initialPosition;

    void Awake()
    {
        _bulletRigidbody = GetComponent<Rigidbody>();
    }

    void Start()
    {
        _bulletRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        _bulletRigidbody.linearVelocity = transform.forward * ProjectileSpeed;
        _initialPosition = transform.position;
        Debug.DrawLine(transform.position, transform.forward, Color.yellow, DebugLineDuration);
    }

    void Update()
    {
        if ((transform.position - _initialPosition).sqrMagnitude > MaxTravelDistanceSqr)
        {
            Destroy(gameObject);
        }
    }

    private void OnCollisionEnter(Collision other)
    {
        IDamageable damageable = other.gameObject.GetComponentInParent<IDamageable>();
        if (damageable != null)
        {
            damageable.TakeDamage(_damage);
        }

        Destroy(gameObject);
    }
}
