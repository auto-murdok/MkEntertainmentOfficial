using System.Collections.Generic;
using UnityEngine;

/// <summary>Lightweight registry for player brains — replaces per-frame FindObjectsByType scans.
/// Each CharacterBrain registers on Start/OnDestroy; consumers read the small cached list.</summary>
public static class LocalPlayerRegistry
{
    private static readonly List<CharacterBrain> _brains = new List<CharacterBrain>(4);

    public static IReadOnlyList<CharacterBrain> Brains => _brains;

    public static void Register(CharacterBrain brain)
    {
        if (brain == null || _brains.Contains(brain)) return;
        _brains.Add(brain);
    }

    public static void Unregister(CharacterBrain brain)
    {
        if (brain == null) return;
        _brains.Remove(brain);
    }

    public static void Clear() => _brains.Clear();

    /// <summary>Network-aware local player resolution without scene scans.</summary>
    public static bool TryGetLocalPlayerTransform(out Transform t)
    {
        t = null;
        if (_brains.Count == 0) return false;

        var nm = Unity.Netcode.NetworkManager.Singleton;
        bool isNetworked = nm != null && nm.IsListening;

        if (isNetworked)
        {
            foreach (var b in _brains)
            {
                if (b == null) continue;
                var no = b.GetComponent<Unity.Netcode.NetworkObject>();
                if (no != null && no.IsOwner) { t = b.transform; return true; }
                if (no == null) { t = b.transform; return true; }
            }
            foreach (var b in _brains)
            {
                if (b == null) continue;
                var no = b.GetComponent<Unity.Netcode.NetworkObject>();
                if (no != null && no.IsSpawned) { t = b.transform; return true; }
            }
            return false;
        }

        t = _brains[0].transform;
        return t != null;
    }
}
