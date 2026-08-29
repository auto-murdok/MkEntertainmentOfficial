using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Handgun : StateMachine<HandgunState, HandgunContext>, IFirearm
{
    [Header("Transforms")]
    [SerializeField] private GameObject _bulletPrefab;
    [SerializeField] private Transform _shootPoint;
    private FirearmEvents _firearmEvents; public FirearmEvents fireArmEvents { get { return _firearmEvents; } }

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
        OnStateChanged += state => Debug.Log($"[{gameObject.name}] -> {state}");
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
            Vector3 shootPos = _shootPoint != null ? _shootPoint.position : transform.position;
            Vector3 diff = mouseWorldPosition - shootPos;
            Vector3 forward = _shootPoint != null ? _shootPoint.forward : transform.forward;
            _context.aimDirection = diff.sqrMagnitude > 0.001f ? diff.normalized : forward;
            _context.isTriggerPressed = true;
        }
    }

    public void TriggerReload()
    {
        if (_context.clipSize < _context.maxClipSize)
        {
            _context.isReloading = true;
        }
    }

    public void ExecuteActualShoot()
    {
        if (_bulletPrefab == null) return;

        Vector3 spawnPos = _shootPoint != null ? _shootPoint.position : transform.position;
        Vector3 forward = _shootPoint != null ? _shootPoint.forward : transform.forward;
        Vector3 direction = _context.aimDirection.sqrMagnitude > 0.001f ? _context.aimDirection : forward;

        Instantiate(_bulletPrefab, spawnPos, Quaternion.LookRotation(direction, Vector3.up));
    }

    public void RegisterEvents(FirearmEvents fireArmEvents)
    {
        _firearmEvents = fireArmEvents;
    }
}
