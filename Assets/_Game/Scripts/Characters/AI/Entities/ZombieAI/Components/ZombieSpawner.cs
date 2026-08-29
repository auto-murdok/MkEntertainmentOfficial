using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Manages spawning of different zombie archetypes and models into the scene.
/// Supports automated interval spawning as well as programmatic spawning.
/// </summary>
public class ZombieSpawner : MonoBehaviour
{
    [Serializable]
    public class ZombieSpawnEntry
    {
        public string label = "Walker";
        public GameObject prefab;
        public ZombieData data;
    }

    [Header("Spawn Configuration")]
    [SerializeField] private List<ZombieSpawnEntry> _zombieTypes = new List<ZombieSpawnEntry>();
    [SerializeField] private Transform[] _spawnPoints;
    [SerializeField] private float _spawnRadius = 15f;
    [SerializeField] private int _maxZombies = 20;

    [Header("Automated Spawning")]
    [SerializeField] private bool _autoSpawnEnabled = true;
    [SerializeField] private float _spawnInterval = 10f;
    private float _timer;

    private readonly List<ZombieBrain> _activeZombies = new List<ZombieBrain>();

    public IReadOnlyList<ZombieBrain> activeZombies => _activeZombies;

    public bool spawningEnabled => _autoSpawnEnabled;

    // SO event channel wiring (set by the composition root): the game-flow
    // layer broadcasts the toggle here, so this spawner never references
    // GameStateManager and the manager never references this class.
    private BoolEventChannel _spawningEnabledChannel;
    private bool _channelSubscribed;

    public BoolEventChannel spawningEnabledChannel
    {
        get => _spawningEnabledChannel;
        set
        {
            if (_channelSubscribed && _spawningEnabledChannel != null)
            {
                _spawningEnabledChannel.OnRaised -= SetSpawningEnabled;
            }
            _spawningEnabledChannel = value;
            _channelSubscribed = value != null;
            if (_spawningEnabledChannel != null)
            {
                _spawningEnabledChannel.OnRaised += SetSpawningEnabled;
            }
        }
    }

    // Game-flow hook: the GameStateManager switches spawning off on game over.
    public void SetSpawningEnabled(bool enabled) => _autoSpawnEnabled = enabled;

    private void OnDisable()
    {
        if (_channelSubscribed && _spawningEnabledChannel != null)
        {
            _spawningEnabledChannel.OnRaised -= SetSpawningEnabled;
            _channelSubscribed = false;
        }
    }

    private void Awake()
    {
        if (_spawnPoints == null || _spawnPoints.Length == 0)
        {
            var childList = new List<Transform>();
            for (int i = 0; i < transform.childCount; i++)
            {
                childList.Add(transform.GetChild(i));
            }
            if (childList.Count > 0)
            {
                _spawnPoints = childList.ToArray();
            }
        }
    }

    private void Update()
    {
        // Clean up dead/destroyed zombies
        for (int i = _activeZombies.Count - 1; i >= 0; i--)
        {
            if (_activeZombies[i] == null)
            {
                _activeZombies.RemoveAt(i);
            }
        }


        // Automated timer (spawns one zombie every 10 seconds)
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

        if (!TryGetSpawnPlacement(out Vector3 spawnPosition, out Quaternion spawnRotation))
        {
            Debug.LogWarning($"[{name}] Spawn skipped: no NavMesh within 5m of the chosen spawn point.");
            return null;
        }

        GameObject instance = Instantiate(entry.prefab, spawnPosition, spawnRotation);
        instance.name = $"{entry.label}_{instance.GetInstanceID()}";

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

    private bool TryGetSpawnPlacement(out Vector3 spawnPosition, out Quaternion spawnRotation)
    {
        Vector3 basePosition = transform.position;
        spawnRotation = Quaternion.identity;

        if (_spawnPoints != null && _spawnPoints.Length > 0)
        {
            int index = UnityEngine.Random.Range(0, _spawnPoints.Length);
            if (_spawnPoints[index] != null)
            {
                basePosition = _spawnPoints[index].position;
                spawnRotation = _spawnPoints[index].rotation;
            }
        }
        else
        {
            Vector2 randomCircle = UnityEngine.Random.insideUnitCircle * _spawnRadius;
            basePosition += new Vector3(randomCircle.x, 0, randomCircle.y);
            spawnRotation = Quaternion.Euler(0, UnityEngine.Random.Range(0f, 360f), 0);
        }

        // Sample on NavMesh — failing loudly beats spawning off-mesh zombies
        // that would idle forever.
        if (NavMesh.SamplePosition(basePosition, out NavMeshHit hit, 5f, NavMesh.AllAreas))
        {
            spawnPosition = hit.position;
            return true;
        }

        spawnPosition = basePosition;
        return false;
    }
}
