using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class BulletProjectile : MonoBehaviour
{
    private Rigidbody bulletRigidbody;
    private Vector3 _initialPosition;
    private float _maxDistance = 30f;

    void Awake()
    {
        bulletRigidbody = GetComponent<Rigidbody>();
    }

    void Start()
    {
        float speed = 50f;
        bulletRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        bulletRigidbody.velocity = transform.forward * speed;
        _initialPosition = transform.position;
        //Debug.Break();
        Debug.DrawLine(transform.position, transform.forward, Color.yellow, 10f);
    }

    void Update()
    {
        if (Vector3.Distance(transform.position, _initialPosition) > _maxDistance)
        {
            Destroy(gameObject);
        }
    }

    private void OnCollisionEnter(Collision other)
    {
        IDamageable damageable = other.gameObject.GetComponentInParent<IDamageable>();
        if (damageable != null) {
            damageable.TakeDamage();
            Debug.LogWarning("HIT DAMAGEABLE" + other.gameObject.name);
        } else {
            Debug.LogWarning("HIT NON DAMAGEABLE" + other.gameObject.name);
        }
        
        Destroy(gameObject);
    }
}
