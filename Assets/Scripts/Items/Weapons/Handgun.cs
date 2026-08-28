using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Handgun : StateMachine<HandgunState, HandgunContext>, IFireArm
{
    [Header("Transforms")]
    [SerializeField] private GameObject _bulletPrefab;
    [SerializeField] private Transform _shootPoint;
    private FireArmEvents _fireArmEvents; public FireArmEvents fireArmEvents { get { return _fireArmEvents; } }

    // Amount of positional kick applied to the weapon when firing.
    private const int GunKickAmount = 5;

    private void Awake()
    {
        states[HandgunState.Ready] = new HandgunReadyState();
        states[HandgunState.Shooting] = new HandgunShootingState();
        states[HandgunState.Reloading] = new HandgunReloadingState();

        _context.animator = gameObject.GetComponent<Animator>();
        _context.gunKick = GunKickAmount;
        // test only
        _context.UIController = GetComponentInParent<CharacterUIController>();
    }

    public void Prepare(int clipSize)
    {
        _context.maxClipSize = clipSize;
        _context.clipSize = clipSize;
    }

    public void Shoot(Vector3 mouseWorldPosition)
    {
        if (!_context.isReloading && !_context.isTriggerPressed)
        {
            _context.aimDirection = (mouseWorldPosition - _shootPoint.position).normalized;
            _context.isTriggerPressed = true;
        }
    }

    public void TriggerReload()
    {
        _context.isReloading = true;
    }

    public void ExecuteActualShoot()
    {
        Instantiate(_bulletPrefab, _shootPoint.position, Quaternion.LookRotation(_context.aimDirection, Vector3.up));
    }

    public void RegisterEvents(FireArmEvents fireArmEvents)
    {
        _fireArmEvents = fireArmEvents;
    }
}
