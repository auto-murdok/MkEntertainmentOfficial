using System.Collections;
using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// Pure visual tracer for hitscan — no collider, no damage. Pooled, lives ~0.08s.
/// AAA pattern: hitscan is instant; this is only the feedback line.
/// </summary>
public sealed class TracerVisual : MonoBehaviour
{
    [SerializeField] private float _lifetime = 0.08f;
    [SerializeField] private TrailRenderer _trail;

    private IObjectPool<TracerVisual> _pool;
    private Coroutine _returnRoutine;

    public IObjectPool<TracerVisual> pool { set => _pool = value; }

    public void Play(Vector3 from, Vector3 to)
    {
        transform.position = from;
        if (_trail != null)
        {
            _trail.Clear();
            _trail.enabled = true;
        }
        // Move instantly via coroutine to simulate short flight without physics.
        if (_returnRoutine != null) StopCoroutine(_returnRoutine);
        _returnRoutine = StartCoroutine(Flight(from, to));
    }

    private IEnumerator Flight(Vector3 from, Vector3 to)
    {
        float t = 0f;
        float duration = 0.06f;
        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            transform.position = Vector3.Lerp(from, to, t);
            yield return null;
        }
        yield return new WaitForSeconds(_lifetime - duration);
        Release();
    }

    private void Release()
    {
        if (_trail != null) _trail.Clear();
        if (_pool != null) _pool.Release(this);
        else Destroy(gameObject);
        _returnRoutine = null;
    }

    private void OnDisable()
    {
        if (_returnRoutine != null)
        {
            StopCoroutine(_returnRoutine);
            _returnRoutine = null;
        }
        if (_trail != null) _trail.Clear();
    }
}
