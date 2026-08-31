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
    [Tooltip("Optional data-driven definition. When assigned, clip/reserve/fireRate/recoil are sourced from it; serialized fields are fallback for legacy prefabs.")]
    [SerializeField] private WeaponDefinition _definition;
    [Tooltip("Reserve ammo pool the reload pulls from. Refilled by ammo pickups.")]
    [SerializeField] private int _reserveAmmo = 45;
    public int reserveAmmo
    {
        get
        {
            if (_firearm is HitscanWeapon hw) return hw.reserveAmmo;
            if (_firearm is Handgun h) return h.reserveAmmo;
            return _reserveAmmo;
        }
    }

    // Internal
    private IFirearm _firearm;
    private Action<Vector3> _onTriggerPressed;
    private Action _onReload;

    private void Awake()
    {
        _firearm = GetComponent<IFirearm>();
        // Resolve definition-driven stats when available (AAA path).
        int defClip = _definition != null ? _definition.clipSize : _clipSize;
        int defReserve = _definition != null ? _definition.defaultReserve : _reserveAmmo;
        float defFireRate = _definition != null ? _definition.fireRate : _fireRate;
        // Finite reserve from the weapon config; refilled by ammo pickups
        // (zombie drops). Reload math treats huge values as a de-facto
        // infinite pool. CLI --infiniteAmmo overrides to int.MaxValue for
        // automated runs where the agent should never run dry.
        int reserve = GameCliArgs.InfiniteAmmo ? int.MaxValue : defReserve;
        _firearm.Prepare(defClip, reserve);

        // Push the weapon's own config into the firearm so the firearm state
        // machine has a single source of truth for cadence (damage lives on
        // the projectile prefab itself).
        if (_firearm is Handgun handgun)
        {
            handgun.SetFireRate(defFireRate);
        }
        else if (_firearm is HitscanWeapon hitscan)
        {
            hitscan.SetFireRate(defFireRate);
        }

        _onTriggerPressed = _firearm.Shoot;
        _onReload = _firearm.TriggerReload;
    }

    public void InjectUIController(CharacterUIController uiController)
    {
        if (_firearm is HitscanWeapon hw2)
        {
            hw2.InjectUIController(uiController);
            return;
        }
        if (_firearm is Handgun handgun)
        {
            handgun.InjectUIController(uiController);
        }
    }

    public void RegisterEvents(FirearmEvents events)
    {
        WeaponEffects effects = GetComponent<WeaponEffects>();
        if (effects == null)
        {
            _firearm.RegisterEvents(events);
            return;
        }

        _firearm.RegisterEvents(new FirearmEvents
        {
            onShoot = events.onShoot + effects.PlayShootEffects,
            onReloadStarted = events.onReloadStarted,
            onReloadFinished = events.onReloadFinished,
        });
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
        if (_firearm is HitscanWeapon hw3)
        {
            hw3.AddReserveAmmo(amount);
            return;
        }
        if (_firearm is Handgun handgun)
        {
            handgun.AddReserveAmmo(amount);
        }
    }
}
