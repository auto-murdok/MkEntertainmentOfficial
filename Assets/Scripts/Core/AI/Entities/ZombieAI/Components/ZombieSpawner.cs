using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Manages spawning of different zombie archetypes and models into the scene,
/// supporting automated waves, random placement on NavMesh, and on-screen testing controls.
/// Uses Function keys (F1, F2, F9) to avoid conflicting with player controls (WASD, R, K, Shift, etc.).
/// </summary>
public class ZombieSpawner : MonoBehaviour
{
    [Serializable]
    public class ZombieSpawnEntry
    {
        public string label = "Walker";
        public GameObject prefab;
        public ZombieData data;
        [Tooltip("Use Function keys (F1, F2, etc.) to prevent gameplay key conflicts.")]
        public KeyCode spawnKey = KeyCode.F1;
    }

    [Header("Spawn Configuration")]
    [SerializeField] private List<ZombieSpawnEntry> _zombieTypes = new List<ZombieSpawnEntry>();
    [SerializeField] private Transform[] _spawnPoints;
    [SerializeField] private float _spawnRadius = 15f;
    [SerializeField] private int _maxZombies = 20;

    [Header("Automated Spawning (Optional)")]
    [SerializeField] private bool _autoSpawnEnabled = false;
    [SerializeField] private float _spawnInterval = 5f;
    private float _timer;

    [Header("Testing & Debug GUI")]
    [SerializeField] private bool _showDebugUI = true;

    private readonly List<ZombieBrain> _activeZombies = new List<ZombieBrain>();

    public IReadOnlyList<ZombieBrain> activeZombies => _activeZombies;

    private void Update()
    {
        // Clean up dead/destroyed zombies
        _activeZombies.RemoveAll(z => z == null);

        // Key shortcuts for spawning (F1, F2, etc.)
        foreach (ZombieSpawnEntry entry in _zombieTypes)
        {
            if (entry.spawnKey != KeyCode.None && Input.GetKeyDown(entry.spawnKey))
            {
                SpawnZombie(entry);
            }
        }

        if (Input.GetKeyDown(KeyCode.Delete) || Input.GetKeyDown(KeyCode.F9))
        {
            ClearAllZombies();
        }

        // Automated timer
        if (_autoSpawnEnabled && _zombieTypes.Count > 0 && _activeZombies.Count < _maxZombies)
        {
            _timer += Time.deltaTime;
            if (_timer >= _spawnInterval)
            {
                _timer = 0f;
                int randomIndex = UnityEngine.Random.Range(0, _zombieTypes.Count);
                SpawnZombie(_zombieTypes[randomIndex]);
            }
        }
    }

    public GameObject SpawnZombie(ZombieSpawnEntry entry)
    {
        if (entry.prefab == null)
        {
            Debug.LogWarning($"[{name}] Cannot spawn: prefab is null for {entry.label}");
            return null;
        }

        if (_activeZombies.Count >= _maxZombies)
        {
            Debug.LogWarning($"[{name}] Max zombie limit ({_maxZombies}) reached.");
            return null;
        }

        Vector3 spawnPosition = GetSpawnPosition();

        GameObject instance = Instantiate(entry.prefab, spawnPosition, Quaternion.identity);
        instance.name = $"{entry.label}_{instance.GetInstanceID()}";

        // Orient newly spawned zombie towards the player
        CharacterBrain player = FindFirstObjectByType<CharacterBrain>();
        if (player != null)
        {
            Vector3 lookTarget = player.transform.position;
            lookTarget.y = instance.transform.position.y;
            instance.transform.LookAt(lookTarget);
        }

        ZombieBehavior behavior = instance.GetComponent<ZombieBehavior>();
        if (behavior != null && entry.data != null)
        {
            behavior.SetZombieData(entry.data);
        }

        ZombieBrain brain = instance.GetComponent<ZombieBrain>();
        if (brain != null)
        {
            _activeZombies.Add(brain);
        }

        Debug.Log($"[{name}] Spawned {instance.name} at {spawnPosition}");
        return instance;
    }

    public void ClearAllZombies()
    {
        foreach (ZombieBrain zombie in _activeZombies)
        {
            if (zombie != null)
            {
                Destroy(zombie.gameObject);
            }
        }
        _activeZombies.Clear();
        Debug.Log($"[{name}] Cleared all spawned zombies.");
    }

    private Vector3 GetSpawnPosition()
    {
        Vector3 basePosition = transform.position;

        if (_spawnPoints != null && _spawnPoints.Length > 0)
        {
            int index = UnityEngine.Random.Range(0, _spawnPoints.Length);
            if (_spawnPoints[index] != null)
            {
                basePosition = _spawnPoints[index].position;
            }
        }
        else
        {
            Vector2 randomCircle = UnityEngine.Random.insideUnitCircle * _spawnRadius;
            basePosition += new Vector3(randomCircle.x, 0, randomCircle.y);
        }

        // Sample on NavMesh
        if (NavMesh.SamplePosition(basePosition, out NavMeshHit hit, 5f, NavMesh.AllAreas))
        {
            return hit.position;
        }

        return basePosition;
    }

    private void OnGUI()
    {
        if (!_showDebugUI) return;

        GUILayout.BeginArea(new Rect(10, 40, 280, 300), "Zombie Testing Controls", GUI.skin.window);

        GUILayout.Label($"Active Zombies: {_activeZombies.Count} / {_maxZombies}");

        GUILayout.Space(5);
        GUILayout.Label("<b>Spawn Shortcuts (Function Keys):</b>");

        for (int i = 0; i < _zombieTypes.Count; i++)
        {
            ZombieSpawnEntry entry = _zombieTypes[i];
            if (GUILayout.Button($"Spawn {entry.label} [{entry.spawnKey}]"))
            {
                SpawnZombie(entry);
            }
        }

        GUILayout.Space(5);
        _autoSpawnEnabled = GUILayout.Toggle(_autoSpawnEnabled, "Auto-Spawn Waves");

        if (GUILayout.Button("Clear All Zombies [F9 / Del]"))
        {
            ClearAllZombies();
        }

        GUILayout.EndArea();
    }
}
