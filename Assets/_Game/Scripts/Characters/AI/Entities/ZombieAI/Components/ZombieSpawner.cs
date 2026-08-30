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
    [SerializeField] private int _maxZombies = 50;

    [Header("Automated Spawning")]
    [SerializeField] private bool _autoSpawnEnabled = true;
    [SerializeField] private float _spawnInterval = 30f;
    private float _timer;

    // Tracked as instances (not brains) so the cap holds for any prefab —
    // including stripped-down ones without a ZombieBrain.
    private readonly List<GameObject> _activeInstances = new List<GameObject>();

    public IReadOnlyList<GameObject> activeInstances => _activeInstances;
    public int activeZombieCount => _activeInstances.Count;

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

    // Opening wave: one zombie per spawn point so the arena starts fully
    // populated. Runs in Start (not Awake) because the composition root wires
    // the spawning channel in Awake; respects the cap and the enabled flag.
    // Returns the number of zombies actually spawned (test seam).
    public int SpawnInitialWave()
    {
        if (!_autoSpawnEnabled || _zombieTypes.Count == 0 || _spawnPoints == null)
        {
            return 0;
        }

        int spawned = 0;
        for (int i = 0; i < _spawnPoints.Length; i++)
        {
            if (_activeInstances.Count >= _maxZombies)
            {
                break;
            }
            if (_spawnPoints[i] == null)
            {
                continue;
            }
            int randomIndex = UnityEngine.Random.Range(0, _zombieTypes.Count);
            if (SpawnZombie(_zombieTypes[randomIndex], _spawnPoints[i]) != null)
            {
                spawned++;
            }
        }
        return spawned;
    }

    private void Start()
    {
        // Zombies are not networked yet: on networked clients skip spawning
        // entirely, or every peer would simulate its own private zombie horde.
        // (NetworkManager.Singleton is null in single-player scenes/tests.)
        Unity.Netcode.NetworkManager networkManager = Unity.Netcode.NetworkManager.Singleton;
        if (networkManager != null && !networkManager.IsServer)
        {
            return;
        }
        SpawnInitialWave();
    }

    private void Update()
    {
        // Clean up dead/destroyed zombies
        for (int i = _activeInstances.Count - 1; i >= 0; i--)
        {
            if (_activeInstances[i] == null)
            {
                _activeInstances.RemoveAt(i);
            }
        }

        // Networked clients never spawn zombies (see Start).
        Unity.Netcode.NetworkManager networkManager = Unity.Netcode.NetworkManager.Singleton;
        if (networkManager != null && !networkManager.IsServer)
        {
            return;
        }

        // Automated timer (spawns one zombie every 30 seconds)
        if (_autoSpawnEnabled && _zombieTypes.Count > 0 && _activeInstances.Count < _maxZombies)
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
        return SpawnZombie(entry, null);
    }

    public GameObject SpawnZombie(ZombieSpawnEntry entry, Transform spawnPoint)
    {
        if (entry.prefab == null)
        {
            Debug.LogWarning($"[{name}] Cannot spawn: prefab is null for {entry.label}");
            return null;
        }

        if (_activeInstances.Count >= _maxZombies)
        {
            Debug.LogWarning($"[{name}] Max zombie limit ({_maxZombies}) reached.");
            return null;
        }

        if (!TryGetSpawnPlacement(spawnPoint, out Vector3 spawnPosition, out Quaternion spawnRotation))
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

        // Networked arena: the spawner only runs on the server, which owns the
        // zombie simulation — clients receive the spawned instance (the zombie
        // prefab is registered in the NetworkPrefabs list). Single-player and
        // tests have no NetworkManager and keep the plain local behaviour.
        Unity.Netcode.NetworkManager networkManager = Unity.Netcode.NetworkManager.Singleton;
        if (networkManager != null)
        {
            Unity.Netcode.NetworkObject networkObject = instance.GetComponent<Unity.Netcode.NetworkObject>();
            if (networkObject != null && !networkObject.IsSpawned)
            {
                networkObject.Spawn(true); // destroyWithScene — zombies belong to the arena
            }
        }

        _activeInstances.Add(instance);

        return instance;
    }

    public void ClearAllZombies()
    {
        foreach (GameObject zombie in _activeInstances)
        {
            if (zombie != null)
            {
                Destroy(zombie);
            }
        }
        _activeInstances.Clear();
        Debug.Log($"[{name}] Cleared all spawned zombies.");
    }

    private bool TryGetSpawnPlacement(Transform spawnPoint, out Vector3 spawnPosition, out Quaternion spawnRotation)
    {
        Vector3 basePosition = transform.position;
        spawnRotation = Quaternion.identity;

        if (spawnPoint != null)
        {
            // Explicit point (opening wave): every zombie gets its own point.
            basePosition = spawnPoint.position;
            spawnRotation = spawnPoint.rotation;
        }
        else if (_spawnPoints != null && _spawnPoints.Length > 0)
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
