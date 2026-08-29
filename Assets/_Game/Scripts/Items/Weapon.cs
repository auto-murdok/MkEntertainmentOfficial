using System;
using UnityEngine;

public class Weapon : Item
{
    [Header("Settings")]
    [SerializeField] private float _damage = 1f;
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
        _firearm.Prepare(_clipSize);

        _onTriggerPressed = _firearm.Shoot;
        _onReload = _firearm.TriggerReload;
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
