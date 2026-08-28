using UnityEngine;

public interface IDamageable
{
    public float remainingHitPoints { get; }
    // Attacker-driven: the damage amount is always supplied by the source, never
    // hardcoded inside the victim (gold-standard IDamageable pattern).
    public void TakeDamage(float amount);
}
