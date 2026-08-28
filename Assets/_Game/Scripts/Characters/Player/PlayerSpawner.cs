using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using Cinemachine;

/// <summary>
/// Spawns the player at runtime from a reusable player prefab, mirroring ZombieSpawner.
/// Wires the input subject, applies PlayerData, swaps the visual model if requested,
/// and rebinds all Cinemachine cameras to the freshly spawned player.
/// </summary>
public class PlayerSpawner : MonoBehaviour
{
    [Serializable]
    public class PlayerSpawnEntry
    {
        public string label = "Survivor";
        public GameObject playerPrefab;
        [Tooltip("Optional visual model prefab swapped in right after spawning (uses the prefab's baked model when null).")]
        public GameObject modelPrefab;
        [Tooltip("Optional data-driven config applied on spawn.")]
        public PlayerData data;
    }

    [Header("Spawn Configuration")]
    [SerializeField] private List<PlayerSpawnEntry> _playerTypes = new List<PlayerSpawnEntry>();
    [SerializeField] private Transform[] _spawnPoints;
    [SerializeField] private float _spawnRadius = 5f;
    [Tooltip("Index of the entry to spawn on start; -1 spawns the first entry.")]
    [SerializeField] private int _spawnEntryIndex = -1;
    [SerializeField] private bool _autoSpawnOnStart = true;

    [Header("Wiring")]
    [Tooltip("The scene's InputHandler (Subject) the spawned player will observe for input.")]
    [SerializeField] private Subject<InputHandlerActions, InputValue> _inputSubject;

    private CharacterBrain _currentPlayer;

    public CharacterBrain currentPlayer => _currentPlayer;

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

    private void Start()
    {
        if (!_autoSpawnOnStart || _playerTypes.Count == 0)
        {
            return;
        }

        int index = _spawnEntryIndex >= 0 && _spawnEntryIndex < _playerTypes.Count ? _spawnEntryIndex : 0;
        SpawnPlayer(_playerTypes[index]);
    }

    public GameObject SpawnPlayer(int entryIndex)
    {
        if (entryIndex < 0 || entryIndex >= _playerTypes.Count)
        {
            Debug.LogWarning($"[{name}] Cannot spawn player: entry index {entryIndex} out of range.");
            return null;
        }

        return SpawnPlayer(_playerTypes[entryIndex]);
    }

    public GameObject SpawnPlayer(PlayerSpawnEntry entry)
    {
        if (entry.playerPrefab == null)
        {
            Debug.LogWarning($"[{name}] Cannot spawn player: prefab is null for {entry.label}");
            return null;
        }

        GetSpawnPlacement(out Vector3 spawnPosition, out Quaternion spawnRotation);

        GameObject instance = Instantiate(entry.playerPrefab, spawnPosition, spawnRotation);
        instance.name = $"{entry.label}_{instance.GetInstanceID()}";

        CharacterBrain brain = instance.GetComponent<CharacterBrain>();
        if (brain != null)
        {
            brain.SetInputSubject(_inputSubject);
        }

        CharacterLocomotion locomotion = instance.GetComponent<CharacterLocomotion>();
        if (locomotion != null && entry.data != null)
        {
            locomotion.SetPlayerData(entry.data);
        }

        if (entry.modelPrefab != null)
        {
            PlayerModelSlot modelSlot = instance.GetComponent<PlayerModelSlot>();
            if (modelSlot == null)
            {
                modelSlot = instance.AddComponent<PlayerModelSlot>();
            }
            modelSlot.SwapModel(entry.modelPrefab);
        }

        PlayerSockets sockets = instance.GetComponent<PlayerSockets>();
        if (sockets != null)
        {
            RebindCameras(sockets.cameraHook);
        }

        _currentPlayer = brain;

        Debug.Log($"[{name}] Spawned {instance.name} at {spawnPosition}");
        return instance;
    }

    /// <summary>Points every Cinemachine virtual camera in the scene at the spawned player's camera hook.</summary>
    public void RebindCameras(Transform cameraHook)
    {
        CinemachineVirtualCameraBase[] cameras = FindObjectsOfType<CinemachineVirtualCameraBase>();
        for (int i = 0; i < cameras.Length; i++)
        {
            cameras[i].Follow = cameraHook;
            cameras[i].LookAt = cameraHook;
        }
    }

    public void DespawnCurrentPlayer()
    {
        if (_currentPlayer != null)
        {
            Destroy(_currentPlayer.gameObject);
            _currentPlayer = null;
        }
    }

    private void GetSpawnPlacement(out Vector3 spawnPosition, out Quaternion spawnRotation)
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

        if (NavMesh.SamplePosition(basePosition, out NavMeshHit hit, 5f, NavMesh.AllAreas))
        {
            spawnPosition = hit.position;
        }
        else
        {
            spawnPosition = basePosition;
        }
    }
}
