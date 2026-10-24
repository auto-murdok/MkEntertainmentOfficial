using UnityEngine;

// Server-side hitbox history for lag compensation. NGO ships no server
// rewind, so the server records every damageable actor's capsule once per
// network tick into a ring buffer; when a shot request arrives stamped with
// the tick the shooter fired at, the hit test runs against the capsules as
// they were at that tick — the world the shooter actually saw
// (favor-the-shooter, the standard compensation model).
//
// Deliberate simplifications, sized for this game:
// - The hitbox is ONE vertical capsule per actor, approximated from its
//   physics bounds (center + radius + cylinder half-height). Actors are
//   upright; a vertical capsule is rotation-invariant around Y.
// - The buffer keeps ~2 s; requests are clamped to MaxRewindTicks so a
//   high-ping (or lying) client cannot shoot arbitrarily far into the past.
// Pure data + math (no networking types): EditMode-testable.
public sealed class HitHistory
{
    // 64 ticks at the default 30 Hz NGO tick rate ~= 2.1 s of history.
    public const int Capacity = 64;

    // ~0.27 s at 30 Hz — in line with shipped shooters' compensation caps
    // (Battlefield 4 capped at ~250 ms) so stale shots stay fair.
    public const int MaxRewindTicks = 8;

    private struct Entry
    {
        public int Tick;
        public Vector3 Center;
        public float Radius;
        public float HalfHeight;
    }

    private readonly Entry[] _entries = new Entry[Capacity];
    private int _count;
    private int _newest; // index of the most recent entry

    public void Record(int tick, Vector3 center, float radius, float halfHeight)
    {
        if (_count > 0 && _entries[_newest].Tick == tick)
        {
            return; // one sample per tick is the contract
        }

        _newest = (_newest + 1) % Capacity;
        _entries[_newest] = new Entry { Tick = tick, Center = center, Radius = radius, HalfHeight = halfHeight };
        if (_count < Capacity)
        {
            _count++;
        }
    }

    // The capsule at a past tick: exact sample when present, linear
    // interpolation between bracketing samples, clamped to the oldest
    // sample when the request reaches past the buffer.
    public (Vector3 Center, float Radius, float HalfHeight) Sample(int tick)
    {
        // Walk from newest to oldest (buffers are tiny; clarity over cleverness).
        Entry newer = default;
        bool haveNewer = false;

        for (int i = 0; i < _count; i++)
        {
            Entry e = _entries[(_newest - i + Capacity * 2) % Capacity];
            if (e.Tick == tick)
            {
                return (e.Center, e.Radius, e.HalfHeight);
            }
            if (e.Tick > tick)
            {
                newer = e;
                haveNewer = true;
                continue;
            }

            // e.Tick < tick: e is the older bracket.
            if (!haveNewer)
            {
                return (e.Center, e.Radius, e.HalfHeight); // tick is newer than everything: use newest
            }

            float span = newer.Tick - e.Tick;
            float t = span > 0f ? (tick - e.Tick) / span : 0f;
            return (Vector3.Lerp(e.Center, newer.Center, t),
                    Mathf.Lerp(e.Radius, newer.Radius, t),
                    Mathf.Lerp(e.HalfHeight, newer.HalfHeight, t));
        }

        // Buffer empty or tick older than the oldest sample: oldest wins.
        Entry oldest = _entries[(_newest - _count + 1 + Capacity * 2) % Capacity];
        return (oldest.Center, oldest.Radius, oldest.HalfHeight);
    }

    // Ray vs vertical capsule. The ray is treated as a segment of length
    // maxDistance; the test is the closest distance between that segment and
    // the capsule's spine segment (after Ericson, RTCD 5.1.9), compared
    // against the radius. `distance` reports the ray parameter of closest
    // approach — precise enough to order competing hits and place the
    // impact point, which is all the shot resolution needs.
    public static bool RaycastCapsule(Vector3 origin, Vector3 direction, Vector3 center,
        float radius, float halfHeight, float maxDistance, out float distance)
    {
        Vector3 spineA = center + Vector3.up * -halfHeight;
        Vector3 spineB = center + Vector3.up * halfHeight;
        Vector3 rayEnd = origin + direction * maxDistance;

        ClosestPoints(origin, rayEnd, spineA, spineB, out Vector3 onRay, out Vector3 onSpine);
        float sqrDist = (onRay - onSpine).sqrMagnitude;

        distance = Vector3.Dot(onRay - origin, direction);
        if (distance < 0f)
        {
            distance = 0f;
        }

        return sqrDist <= radius * radius;
    }

    // Closest points between segments p1q1 and p2q2 (Ericson 5.1.9).
    private static void ClosestPoints(Vector3 p1, Vector3 q1, Vector3 p2, Vector3 q2,
        out Vector3 c1, out Vector3 c2)
    {
        Vector3 d1 = q1 - p1;
        Vector3 d2 = q2 - p2;
        Vector3 r = p1 - p2;
        float a = Vector3.Dot(d1, d1);
        float e = Vector3.Dot(d2, d2);
        float f = Vector3.Dot(d2, r);

        float s, t;
        if (a <= Mathf.Epsilon && e <= Mathf.Epsilon)
        {
            s = 0f;
            t = 0f;
        }
        else if (a <= Mathf.Epsilon)
        {
            s = 0f;
            t = Mathf.Clamp01(f / e);
        }
        else
        {
            float c = Vector3.Dot(d1, r);
            if (e <= Mathf.Epsilon)
            {
                t = 0f;
                s = Mathf.Clamp01(-c / a);
            }
            else
            {
                float b = Vector3.Dot(d1, d2);
                float denom = a * e - b * b;
                s = denom > Mathf.Epsilon ? Mathf.Clamp01((b * f - c * e) / denom) : 0f;
                t = (b * s + f) / e;
                if (t < 0f)
                {
                    t = 0f;
                    s = Mathf.Clamp01(-c / a);
                }
                else if (t > 1f)
                {
                    t = 1f;
                    s = Mathf.Clamp01((b - c) / a);
                }
            }
        }

        c1 = p1 + d1 * s;
        c2 = p2 + d2 * t;
    }
}
