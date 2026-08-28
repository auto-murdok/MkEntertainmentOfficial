using UnityEngine;

public class BulletProjectile : MonoBehaviour
{
    private const float MaxTravelDistance = 30f;
    private const float ProjectileSpeed = 50f;
    private const float DebugLineDuration = 10f;

    private Rigidbody _bulletRigidbody;
    private Vector3 _initialPosition;

    void Awake()
    {
        _bulletRigidbody = GetComponent<Rigidbody>();
    }

    void Start()
    {
        _bulletRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        _bulletRigidbody.velocity = transform.forward * ProjectileSpeed;
        _initialPosition = transform.position;
        Debug.DrawLine(transform.position, transform.forward, Color.yellow, DebugLineDuration);
    }

    void Update()
    {
        if (Vector3.Distance(transform.position, _initialPosition) > MaxTravelDistance)
        {
            Destroy(gameObject);
        }
    }

    private void OnCollisionEnter(Collision other)
    {
        IDamageable damageable = other.gameObject.GetComponentInParent<IDamageable>();
        if (damageable != null)
        {
            damageable.TakeDamage();
            Debug.LogWarning("HIT DAMAGEABLE" + other.gameObject.name);
        }
        else
        {
            Debug.LogWarning("HIT NON DAMAGEABLE" + other.gameObject.name);
        }

        Destroy(gameObject);
    }
}
