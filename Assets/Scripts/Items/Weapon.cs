using System;
using UnityEngine;

public class Weapon : Item
{
    [Header("Settings")]
    [SerializeField] private float _damage = 1f;
    [SerializeField] private float _fireRate = 0.2f;
    [SerializeField] private float _recoilForce = 5f; public float recoilForce { get { return _recoilForce; } }
    [SerializeField] private int _clipSize = 5; public int clipSize { get { return _clipSize; } }

    // Internal
    private IFireArm _fireArm;
    private Action<Vector3> _onTriggerPressed;
    private Action _onReload;

    private void Awake()
    {
        _fireArm = GetComponent<IFireArm>();
        _fireArm.Prepare(_clipSize);

        _onTriggerPressed = _fireArm.Shoot;
        _onReload = _fireArm.TriggerReload;
    }
    public void RegisterEvents(FireArmEvents events)
    {
        _fireArm.RegisterEvents(events);
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
