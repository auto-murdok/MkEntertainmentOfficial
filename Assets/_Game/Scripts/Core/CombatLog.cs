using System;
using UnityEngine;

// Debug-only combat event log: a fixed-capacity ring buffer of formatted
// entries that the DebugHud renders. Reports are formatted once, at report
// time (damage events are rare), so per-frame cost is a plain string copy.
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

    private const int Capacity = 8;

    private static readonly string[] _entries = new string[Capacity];
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
        Append($"[{Time.time:F1}s] {_currentSource} -> {SafeName(victim)} took {amount:F1} (HP {remainingHitPoints:F0})");
    }

    // Non-damaging physics events (e.g. a bullet hitting scenery).
    public static void ReportImpact(string message)
    {
        Append($"[{Time.time:F1}s] {message}");
    }

    // Copies the most recent entries (oldest first) into the caller's buffer.
    // Returns how many were written.
    public static int CopyRecent(string[] buffer)
    {
        int count = Mathf.Min(_count, buffer.Length);
        for (int i = 0; i < count; i++)
        {
            int index = (_head - _count + i + Capacity * 2) % Capacity;
            buffer[i] = _entries[index];
        }
        return count;
    }

    private static void Append(string entry)
    {
        _entries[_head] = entry;
        _head = (_head + 1) % Capacity;
        _count = Mathf.Min(_count + 1, Capacity);
    }

    private static string SafeName(GameObject gameObject)
    {
        return gameObject != null ? gameObject.name : "<destroyed>";
    }
}
