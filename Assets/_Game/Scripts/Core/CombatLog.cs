using System;
using UnityEngine;

// Combat event log: a fixed-capacity ring buffer of formatted entries that the
// HUD renders. Entries are kind-tagged so the player-facing ticker can filter
// out diagnostic noise (bullet launches, scenery hits) while the F3 debug HUD
// still sees everything. Reports are formatted once, at report time (damage
// events are rare), so per-frame cost is a plain struct copy.
public static class CombatLog
{
    // Scoped damage source: wraps a TakeDamage call so the victim's
    // ApplyDamage report can name the attacker type ("Bullet", "ZombieBite").
    public readonly struct SourceScope : IDisposable
    {
        private readonly string _previous;

        public SourceScope(string source)
        {
            _previous = _currentSource;
            _currentSource = source;
        }

        public void Dispose()
        {
            _currentSource = _previous;
        }
    }

    // Importance of an entry. The HUD ticker shows Impact and above; Debug
    // entries are diagnostic noise only rendered by the F3 overlay.
    public enum EntryKind
    {
        Debug = 0,
        Impact = 1,
        Damage = 2,
    }

    private struct Entry
    {
        public string Text;
        public EntryKind Kind;
    }

    private const int Capacity = 8;

    private static readonly Entry[] _entries = new Entry[Capacity];
    private static int _head;
    private static int _count;
    private static string _currentSource = "unknown";

    public static SourceScope BeginSource(string source)
    {
        return new SourceScope(source);
    }

    // Called from ActorBrainBase.ApplyDamage — every damage an actor receives
    // flows through here exactly once.
    public static void ReportDamage(float amount, float remainingHitPoints, GameObject victim)
    {
        Append($"[{Time.time:F1}s] {_currentSource} -> {SafeName(victim)} took {amount:F1} (HP {remainingHitPoints:F0})", EntryKind.Damage);
    }

    // Non-damaging events. Diagnostic noise (bullet launches, scenery hits,
    // pool releases) passes Kind.Debug; gameplay moments (ammo pickups) use
    // the default Kind.Impact so they reach the HUD ticker.
    public static void ReportImpact(string message, EntryKind kind = EntryKind.Impact)
    {
        Append($"[{Time.time:F1}s] {message}", kind);
    }

    // Copies the most recent entries of every kind (oldest first) into the
    // caller's buffer. Returns how many were written.
    public static int CopyRecent(string[] buffer)
    {
        return CopyRecent(buffer, EntryKind.Debug);
    }

    // Copies the most recent entries with kind >= minKind (oldest first).
    public static int CopyRecent(string[] buffer, EntryKind minKind)
    {
        int written = 0;
        for (int i = 0; i < _count && written < buffer.Length; i++)
        {
            int index = (_head - _count + i + Capacity * 2) % Capacity;
            if (_entries[index].Kind >= minKind)
            {
                buffer[written++] = _entries[index].Text;
            }
        }
        return written;
    }

    private static void Append(string entry, EntryKind kind)
    {
        _entries[_head] = new Entry { Text = entry, Kind = kind };
        _head = (_head + 1) % Capacity;
        _count = Mathf.Min(_count + 1, Capacity);
    }

    private static string SafeName(GameObject gameObject)
    {
        return gameObject != null ? gameObject.name : "<destroyed>";
    }
}
