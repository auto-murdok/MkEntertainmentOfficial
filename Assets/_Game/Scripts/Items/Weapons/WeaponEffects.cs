using System;
using UnityEngine;
using UnityEngine.Pool;

// Local weapon presentation. Gameplay remains driven by Handgun and BulletProjectile.
public sealed class WeaponEffects : MonoBehaviour
{
    [Header("Muzzle")]
    [SerializeField] private ParticleSystem _muzzleFlash;
    [SerializeField] private ParticleSystem _muzzleSmoke;
    [SerializeField] private Light _muzzleLight;
    [SerializeField] private float _lightDuration = 0.04f;

    [Header("Shell Ejection")]
    [SerializeField] private ShellCasing _shellPrefab;
    [SerializeField] private Transform _ejectPoint;
    [SerializeField] private float _shellLife = 3f;
    [SerializeField] private Vector3 _ejectionVelocity = new Vector3(2f, 1f, -0.5f);
    [SerializeField] private Vector3 _ejectionTorque = new Vector3(4f, 7f, 3f);

    private ObjectPool<ShellCasing> _shellPool;
    private float _lightOffAt;

    private void Awake()
    {
        if (_shellPrefab != null)
        {
            _shellPool = new ObjectPool<ShellCasing>(
                CreateShell,
                shell => shell.gameObject.SetActive(true),
                shell => shell.gameObject.SetActive(false),
                shell => Destroy(shell.gameObject),
                collectionCheck: true,
                defaultCapacity: 4,
                maxSize: 16);
        }
    }

    public void PlayShootEffects()
    {
        if (_muzzleFlash != null) _muzzleFlash.Play(true);
        if (_muzzleSmoke != null) _muzzleSmoke.Play(true);
        if (_muzzleLight != null)
        {
            _muzzleLight.enabled = true;
            _lightOffAt = Time.time + _lightDuration;
        }

        if (_shellPool == null || _ejectPoint == null) return;

        ShellCasing shell = _shellPool.Get();
        shell.Launch(
            _ejectPoint.position,
            _ejectPoint.rotation,
            _ejectPoint.TransformDirection(_ejectionVelocity),
            _ejectionTorque,
            _shellLife,
            _shellPool.Release);
    }

    private void Update()
    {
        if (_muzzleLight != null && _muzzleLight.enabled && Time.time >= _lightOffAt)
        {
            _muzzleLight.enabled = false;
        }
    }

    private ShellCasing CreateShell()
    {
        ShellCasing shell = Instantiate(_shellPrefab);
        shell.gameObject.SetActive(false);
        return shell;
    }
}
