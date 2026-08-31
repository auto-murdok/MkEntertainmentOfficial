using System;
using System.Collections.Generic;
using UnityEngine;

// Room-by-room discovery state.
// Scans Floor_* objects at startup, keeps world bounds per room,
// reveals spawn room + any room the player enters (poll-based, no trigger
// colliders required). Local per-peer — each client sees its own exploration;
// for shared-map replicate the compact bitset from the server (see header).
public class MinimapDiscovery : MonoBehaviour
{
    public event Action<int> OnRoomRevealed;
    public event Action OnReset;

    private readonly List<MapRoom> _rooms = new List<MapRoom>();
    private readonly HashSet<int> _discovered = new HashSet<int>();
    private readonly Dictionary<string, int> _nameToIndex = new Dictionary<string, int>();

    private Bounds _mapExtents;
    private bool _initialized;

    public IReadOnlyList<MapRoom> Rooms => _rooms;
    public Bounds MapExtents => _mapExtents;
    public bool IsInitialized => _initialized;

    // Tunables
    private const float BoundsExpand = 1.5f; // forgive thin walls
    private const float RevealPollInterval = 0.15f;
    private float _nextPoll;

    public bool IsDiscovered(int index) => _discovered.Contains(index);
    public bool IsDiscovered(string id) => _nameToIndex.TryGetValue(id, out int idx) && _discovered.Contains(idx);

    public int DiscoveredCount => _discovered.Count;
    public int RoomCount => _rooms.Count;

    private void Awake()
    {
        BuildRooms();
    }

    private void Start()
    {
        // Reveal spawn room immediately so map is never empty.
        RevealSpawnRoom();
    }

    private void Update()
    {
        if (!_initialized || _rooms.Count == 0) return;
        if (Time.unscaledTime < _nextPoll) return;
        _nextPoll = Time.unscaledTime + RevealPollInterval;

        Transform player = FindLocalPlayerTransform();
        if (player == null) return;

        Vector3 pos = player.position;
        // Use XZ containment (Y ignored). Expand bounds slightly so wall
        // thickness does not block enter detection when player stands near edge.
        for (int i = 0; i < _rooms.Count; i++)
        {
            if (_discovered.Contains(i)) continue;
            Bounds b = _rooms[i].worldBounds;
            b.Expand(new Vector3(BoundsExpand, 100f, BoundsExpand));
            if (b.Contains(pos))
            {
                Reveal(i);
            }
        }
    }

    public bool Reveal(int index)
    {
        if (index < 0 || index >= _rooms.Count) return false;
        if (!_discovered.Add(index)) return false;
        OnRoomRevealed?.Invoke(index);
        return true;
    }

    public bool Reveal(string id)
    {
        if (!_nameToIndex.TryGetValue(id, out int idx)) return false;
        return Reveal(idx);
    }

    // Reveals room + any graph neighbors (for corridor triggers / Edgar-style).
    public int RevealWithNeighbors(int index)
    {
        int count = 0;
        if (Reveal(index)) count++;
        if (index < 0 || index >= _rooms.Count) return count;
        foreach (int n in _rooms[index].neighbors)
            if (Reveal(n)) count++;
        return count;
    }

    public void RevealAll()
    {
        for (int i = 0; i < _rooms.Count; i++) Reveal(i);
    }

    public void ResetDiscovery()
    {
        _discovered.Clear();
        OnReset?.Invoke();
        RevealSpawnRoom();
    }

    // --- Room building ---

    private void BuildRooms()
    {
        _rooms.Clear();
        _nameToIndex.Clear();

        // Find all Floor_* root objects. In this arena each floor is a direct
        // child of the scene hierarchy; some have MeshRenderer, some have
        // nested visuals. Prefer Renderer bounds, fallback to Collider, then
        // to a 10x10 placeholder.
        var allTransforms = FindObjectsByType<Transform>(FindObjectsSortMode.None);
        var floorTransforms = new List<Transform>();
        foreach (var t in allTransforms)
        {
            if (t.name.StartsWith("Floor_", StringComparison.Ordinal))
                floorTransforms.Add(t);
        }

        // Deterministic order: alphabetical so indices stable across runs
        floorTransforms.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

        // Build each room entry
        foreach (var t in floorTransforms)
        {
            Bounds b = ResolveWorldBounds(t);
            var room = new MapRoom
            {
                index = _rooms.Count,
                id = t.name,
                displayName = ToDisplayName(t.name),
                worldBounds = b,
            };
            _rooms.Add(room);
            _nameToIndex[room.id] = room.index;
        }

        if (_rooms.Count == 0)
        {
            Debug.LogWarning("[MinimapDiscovery] No Floor_* found — map will be empty.");
            return;
        }

        // Compute map extents (union of all room bounds)
        _mapExtents = _rooms[0].worldBounds;
        for (int i = 1; i < _rooms.Count; i++)
            _mapExtents.Encapsulate(_rooms[i].worldBounds);

        // Naive neighbor inference: rooms whose expanded bounds overlap are neighbors
        // (cheap, runs once). Replace with explicit data if you author neighbors.
        const float neighborExpand = 3f;
        for (int i = 0; i < _rooms.Count; i++)
        {
            Bounds a = _rooms[i].worldBounds;
            a.Expand(new Vector3(neighborExpand, 0.1f, neighborExpand));
            for (int j = i + 1; j < _rooms.Count; j++)
            {
                Bounds bj = _rooms[j].worldBounds;
                if (a.Intersects(bj))
                {
                    _rooms[i].neighbors.Add(j);
                    _rooms[j].neighbors.Add(i);
                }
            }
        }

        _initialized = true;
        Debug.Log($"[MinimapDiscovery] Built {_rooms.Count} rooms. Extents center={_mapExtents.center} size={_mapExtents.size}");
    }

    private static Bounds ResolveWorldBounds(Transform root)
    {
        // Try renderers first (most accurate for floor planes)
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length > 0)
        {
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            if (b.size.x > 0.1f && b.size.z > 0.1f) return b;
        }

        var colliders = root.GetComponentsInChildren<Collider>(true);
        if (colliders.Length > 0)
        {
            Bounds b = colliders[0].bounds;
            for (int i = 1; i < colliders.Length; i++) b.Encapsulate(colliders[i].bounds);
            if (b.size.x > 0.1f && b.size.z > 0.1f) return b;
        }

        // Fallback: 10x10 square at root position (still gives a clickable room)
        Vector3 c = root.position;
        c.y = 0f;
        return new Bounds(c, new Vector3(10f, 0.5f, 10f));
    }

    private static string ToDisplayName(string floorName)
    {
        // Floor_Atrium -> ATRIUM, Floor_PlayerStart -> PLAYER START
        string s = floorName.StartsWith("Floor_") ? floorName.Substring(6) : floorName;
        s = s.Replace("_", " ");
        return s.ToUpperInvariant();
    }

    private void RevealSpawnRoom()
    {
        if (_rooms.Count == 0) return;
        // Prefer Floor_PlayerStart, then Floor_Atrium, then room containing spawner
        if (_nameToIndex.TryGetValue("Floor_PlayerStart", out int ps)) { Reveal(ps); return; }
        if (_nameToIndex.TryGetValue("Floor_Atrium", out int at)) { Reveal(at); return; }

        // Fallback: room containing the PlayerSpawner's own transform
        Vector3 p = transform.position;
        for (int i = 0; i < _rooms.Count; i++)
        {
            if (_rooms[i].worldBounds.Contains(p)) { Reveal(i); return; }
        }
        // Ultimate fallback: nearest room
        int nearest = 0; float best = float.MaxValue;
        for (int i = 0; i < _rooms.Count; i++)
        {
            float d = Vector3.Distance(_rooms[i].worldBounds.center, p);
            if (d < best) { best = d; nearest = i; }
        }
        Reveal(nearest);
    }

    private Transform FindLocalPlayerTransform()
    {
        if (LocalPlayerRegistry.TryGetLocalPlayerTransform(out var t)) return t;
        // Fallback for pre-registry edge (before brain Start): scene scan once
        var brain = FindFirstObjectByType<CharacterBrain>();
        if (brain != null) return brain.transform;
        return null;
    }
}
