using UnityEngine;

// Walk-over ammo pickup dropped by dead zombies (see ZombieBrain.DropAmmo).
// Consumed by the first target carrying a Weapon (the player); anything else
// passes straight through. The trigger collider and a kinematic Rigidbody are
// set up on the AmmoPickup prefab.
public class AmmoPickup : MonoBehaviour
{
    [SerializeField] private int _amount = 15;

    private bool _consumed;

    public int amount => _amount;

    private void OnTriggerEnter(Collider other)
    {
        TryPickup(other.gameObject);
    }

    // Returns true when the pickup was consumed. Public so tests can drive
    // consumption without physics simulation.
    public bool TryPickup(GameObject target)
    {
        if (_consumed || target == null || _amount <= 0)
        {
            return false;
        }

        // The gun lives deep in the player rig (right-hand holder), so resolve
        // from the collider's root. Zombies have no Weapon and are ignored.
        Weapon weapon = target.transform.root.GetComponentInChildren<Weapon>();
        if (weapon == null)
        {
            return false;
        }

        weapon.AddReserveAmmo(_amount);
        _consumed = true;
        CombatLog.ReportImpact($"Ammo pickup: +{_amount} reserve");
        Destroy(gameObject);
        return true;
    }
}
