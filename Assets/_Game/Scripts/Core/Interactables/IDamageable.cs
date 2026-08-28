using UnityEngine;

public interface IDamageable
{
    public float remainingHitPoints { get; }
    public void TakeDamage();
}
