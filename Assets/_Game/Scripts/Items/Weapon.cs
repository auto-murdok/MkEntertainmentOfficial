using System;
using UnityEngine;

public class Weapon : Item, IAmmoReceiver, IWeapon
{
    [Header("Settings")]
    [SerializeField] private float _fireRate = 0.2f;
    [SerializeField] private float _recoilForce = 5f;
    public float recoilForce { get { return _recoilForce; } }
    [SerializeField] private int _clipSize = 5;
    public int clipSize { get { return _clipSize; } }
    public int maxClipSize => _clipSize;
    [Tooltip("Reserve ammo pool the reload pulls from. Refilled by ammo pickups.")]
    [SerializeField] private int _reserveAmmo = 45;
    public int reserveAmmo => _firearm is Handgun h ? h.reserveAmmo : _reserveAmmo;

    // Internal
    private IFirearm _firearm;
    private Action<Vector3> _onTriggerPressed;
    private Action _onReload;

    private void Awake()
    {
        _firearm = GetComponent<IFirearm>();
        // Finite reserve from the weapon config; refilled by ammo pickups
        // (zombie drops). Reload math treats huge values as a de-facto
        // infinite pool.
        _firearm.Prepare(_clipSize, _reserveAmmo);

        // Push the weapon's own config into the firearm so the firearm state
        // machine has a single source of truth for cadence (damage lives on
        // the projectile prefab itself).
        if (_firearm is Handgun handgun)
        {
            handgun.SetFireRate(_fireRate);
        }

        _onTriggerPressed = _firearm.Shoot;
        _onReload = _firearm.TriggerReload;
    }

    public void InjectUIController(CharacterUIController uiController)
    {
        if (_firearm is Handgun handgun)
        {
            handgun.InjectUIController(uiController);
        }
    }

    public void RegisterEvents(FirearmEvents events)
    {
        _firearm.RegisterEvents(events);
    }

    public void TriggerShoot(Vector3 aimPosition)
    {
        _onTriggerPressed?.Invoke(aimPosition);
    }

    public void TriggerReload()
    {
        _onReload?.Invoke();
    }

    // IAmmoReceiver: ammo pickups (and future systems) add to the reserve pool.
    public void AddReserveAmmo(int amount)
    {
        if (_firearm is Handgun handgun)
        {
            handgun.AddReserveAmmo(amount);
        }
    }
}
