using System;
using UnityEngine;

public class Weapon : Item
{
    [Header("Settings")]
    [SerializeField] private float _fireRate = 0.2f;
    [SerializeField] private float _recoilForce = 5f;
    public float recoilForce { get { return _recoilForce; } }
    [SerializeField] private int _clipSize = 5;
    public int clipSize { get { return _clipSize; } }

    // Internal
    private IFirearm _firearm;
    private Action<Vector3> _onTriggerPressed;
    private Action _onReload;

    private void Awake()
    {
        _firearm = GetComponent<IFirearm>();
        // int.MaxValue reserve = infinite ammo pool for now (Ammo pickups not
        // yet wired into the inventory); reload math already supports it.
        _firearm.Prepare(_clipSize, int.MaxValue);

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
}
