using System;
using System.Collections;
using UnityEngine;

public sealed class ShellCasing : MonoBehaviour
{
    private Rigidbody _rigidbody;
    private Action<ShellCasing> _release;
    private Coroutine _releaseRoutine;

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
    }

    public void Launch(
        Vector3 position,
        Quaternion rotation,
        Vector3 velocity,
        Vector3 torque,
        float life,
        Action<ShellCasing> release)
    {
        _release = release;
        transform.SetPositionAndRotation(position, rotation);
        _rigidbody.linearVelocity = velocity;
        _rigidbody.angularVelocity = torque;

        if (_releaseRoutine != null) StopCoroutine(_releaseRoutine);
        _releaseRoutine = StartCoroutine(ReleaseAfter(life));
    }

    private IEnumerator ReleaseAfter(float life)
    {
        yield return new WaitForSeconds(Mathf.Max(0.1f, life));
        _release?.Invoke(this);
        _release = null;
        _releaseRoutine = null;
    }

    private void OnDisable()
    {
        if (_releaseRoutine != null)
        {
            StopCoroutine(_releaseRoutine);
            _releaseRoutine = null;
        }

        if (_rigidbody != null)
        {
            _rigidbody.linearVelocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;
        }
    }
}
