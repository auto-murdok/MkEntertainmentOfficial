using System.Collections.Generic;
using UnityEngine;

// Pooled, purely cosmetic shot tracers: a thin stretched cube from muzzle
// to impact point, alive for a few frames. Tracers are how every peer SEES
// a shot in the server-authoritative model — the shot itself is a raycast,
// so this visual is spawned locally on each peer from the shot event (the
// owner spawns its predicted tracer at fire time; everyone else spawns from
// the server's confirmed-shot broadcast). Never networked, never pooled
// through NGO: a local UnityEngine.Pool-style list, matching how
// BulletProjectile pooled its visuals before.
public class TracerView : MonoBehaviour
{
    private const float LifetimeSeconds = 0.06f;
    private const float Thickness = 0.02f;

    private static TracerView _instance;

    private sealed class Tracer
    {
        public GameObject GameObject;
        public float ExpiresAt;
    }

    private readonly List<Tracer> _pool = new List<Tracer>();
    private Material _material;

    public static void Spawn(Vector3 from, Vector3 to)
    {
        if (from == to)
        {
            return;
        }

        EnsureInstance();
        _instance.SpawnInternal(from, to);
    }

    private static void EnsureInstance()
    {
        if (_instance != null)
        {
            return;
        }

        GameObject host = new GameObject(nameof(TracerView));
        DontDestroyOnLoad(host);
        _instance = host.AddComponent<TracerView>();

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }
        _instance._material = new Material(shader)
        {
            color = new Color(1f, 0.85f, 0.45f),
        };
    }

    private void SpawnInternal(Vector3 from, Vector3 to)
    {
        Tracer tracer = null;
        foreach (Tracer candidate in _pool)
        {
            if (!candidate.GameObject.activeSelf)
            {
                tracer = candidate;
                break;
            }
        }

        if (tracer == null)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Tracer";
            go.transform.SetParent(transform, worldPositionStays: false);
            Collider collider = go.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider); // visual only: must never enter physics
            }
            go.GetComponent<MeshRenderer>().sharedMaterial = _material;
            tracer = new Tracer { GameObject = go };
            _pool.Add(tracer);
        }

        Vector3 delta = to - from;
        float length = delta.magnitude;
        Transform t = tracer.GameObject.transform;
        t.position = from + delta * 0.5f;
        t.rotation = Quaternion.LookRotation(delta / length, Vector3.up);
        t.localScale = new Vector3(Thickness, Thickness, length);
        tracer.ExpiresAt = Time.time + LifetimeSeconds;
        tracer.GameObject.SetActive(true);
    }

    private void Update()
    {
        float now = Time.time;
        foreach (Tracer tracer in _pool)
        {
            if (tracer.GameObject.activeSelf && now >= tracer.ExpiresAt)
            {
                tracer.GameObject.SetActive(false);
            }
        }
    }
}
