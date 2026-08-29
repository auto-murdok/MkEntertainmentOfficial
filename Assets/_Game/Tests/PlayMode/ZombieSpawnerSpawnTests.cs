using System.Collections;
using System.Linq;
using NUnit.Framework;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    public class ZombieSpawnerSpawnTests
    {
        private GameObject _host;
        private GameObject _plane;
        private ZombieSpawner _spawner;
        private GameObject _zombiePrefab;

        [SetUp]
        public void SetUp()
        {
            // A floor + a synchronously baked surface so NavMesh.SamplePosition
            // succeeds no matter which scene the play-mode run opened in.
            _plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            _plane.transform.localScale = new Vector3(6f, 1f, 6f);
            _plane.AddComponent<NavMeshSurface>().BuildNavMesh();

            _host = new GameObject("ZombieSpawnerHost");
            // Children must exist before AddComponent: Awake harvests them into
            // _spawnPoints when the serialized array is empty.
            CreateSpawnPoint(new Vector3(0f, 0.1f, 0f));
            CreateSpawnPoint(new Vector3(8f, 0.1f, 0f));
            CreateSpawnPoint(new Vector3(-8f, 0.1f, 0f));
            _spawner = _host.AddComponent<ZombieSpawner>();

            _zombiePrefab = new GameObject("ZombiePrefab");

            SetSerializedField("_maxZombies", 50);
            SetSerializedField("_spawnInterval", 30f);
            ConfigureZombieTypes();

            // The spawner's Start fires the opening wave on the next frame;
            // tests count what exists after that point.
        }

        [TearDown]
        public void TearDown()
        {
            ClearSpawnedZombies();
            if (_zombiePrefab != null) Object.DestroyImmediate(_zombiePrefab);
            if (_host != null) Object.DestroyImmediate(_host);
            if (_plane != null) Object.DestroyImmediate(_plane);
            NavMesh.RemoveAllNavMeshData();
        }

        // Destroys every spawned zombie instance. Shared by TearDown and the
        // test-body spawner reset below.
        private static void ClearSpawnedZombies()
        {
            foreach (Transform instance in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (instance.name.StartsWith("Walker_"))
                {
                    Object.DestroyImmediate(instance.gameObject);
                }
            }
        }

        // The test runner may execute a [UnityTest] body a full frame after
        // SetUp — by then the shared spawner's Start has already fired its
        // opening wave (observed: 3 Walker_ zombies exist before the first
        // coroutine step). Tests that need the spawner to NEVER spawn must
        // recreate it with the guard state set before its Start can run.
        private void RecreateSpawner()
        {
            if (_host != null) Object.DestroyImmediate(_host);
            ClearSpawnedZombies();

            _host = new GameObject("ZombieSpawnerHost");
            CreateSpawnPoint(new Vector3(0f, 0.1f, 0f));
            CreateSpawnPoint(new Vector3(8f, 0.1f, 0f));
            CreateSpawnPoint(new Vector3(-8f, 0.1f, 0f));
            _spawner = _host.AddComponent<ZombieSpawner>();

            SetSerializedField("_maxZombies", 50);
            SetSerializedField("_spawnInterval", 30f);
            ConfigureZombieTypes();
        }

        [UnityTest]
        public IEnumerator Start_SpawnsOneZombiePerSpawnPoint()
        {
            yield return null;
            yield return null;

            AssertSpawnedCount(3, "The opening wave must place one zombie on every spawn point.");
        }

        [UnityTest]
        public IEnumerator Start_SpawningDisabled_SpawnsNothing()
        {
            RecreateSpawner();
            _spawner.SetSpawningEnabled(false);
            yield return null;
            yield return null;

            AssertSpawnedCount(0, "No zombies may spawn while spawning is disabled.");
        }

        [UnityTest]
        public IEnumerator Start_EmptyZombieTypes_SpawnsNothing()
        {
            RecreateSpawner();
            SetSerializedField("_zombieTypes", new System.Collections.Generic.List<ZombieSpawner.ZombieSpawnEntry>());
            yield return null;
            yield return null;

            AssertSpawnedCount(0, "No zombies may spawn without configured zombie types.");
        }

        [UnityTest]
        public IEnumerator IntervalSpawn_AddsZombiesAfterIntervalElapses()
        {
            yield return null;
            yield return null;
            AssertSpawnedCount(3, "Precondition: opening wave complete.");

            SetSerializedField("_spawnInterval", 0.05f);
            yield return new WaitForSeconds(0.5f);

            AssertSpawnedCountGreater(3, "The automated timer must keep spawning after the opening wave.");
        }

        [UnityTest]
        public IEnumerator IntervalSpawn_RespectsMaxZombies()
        {
            SetSerializedField("_maxZombies", 3);
            yield return null;
            yield return null;

            SetSerializedField("_spawnInterval", 0.05f);
            yield return new WaitForSeconds(0.5f);

            AssertSpawnedCount(3, "Spawning must stop at the configured maximum.");
        }

        private void CreateSpawnPoint(Vector3 position)
        {
            var point = new GameObject("SpawnPoint");
            point.transform.SetParent(_host.transform, false);
            point.transform.position = position;
        }

        private void ConfigureZombieTypes()
        {
            var entry = new ZombieSpawner.ZombieSpawnEntry { label = "Walker", prefab = _zombiePrefab };
            SetSerializedField("_zombieTypes", new System.Collections.Generic.List<ZombieSpawner.ZombieSpawnEntry> { entry });
        }

        private static int CountSpawnedZombies()
        {
            return Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                .Count(t => t.name.StartsWith("Walker_"));
        }

        private static void AssertSpawnedCount(int expected, string message)
        {
            Assert.AreEqual(expected, CountSpawnedZombies(), message);
        }

        private static void AssertSpawnedCountGreater(int exclusiveLower, string message)
        {
            Assert.Greater(CountSpawnedZombies(), exclusiveLower, message);
        }

        private void SetSerializedField(string fieldName, object value)
        {
            var so = new UnityEditor.SerializedObject(_spawner);
            var prop = so.FindProperty(fieldName);
            if (value is int i)
            {
                prop.intValue = i;
            }
            else if (value is float f)
            {
                prop.floatValue = f;
            }
            else if (value is System.Collections.Generic.List<ZombieSpawner.ZombieSpawnEntry> list)
            {
                prop.arraySize = list.Count;
                for (int index = 0; index < list.Count; index++)
                {
                    var element = prop.GetArrayElementAtIndex(index);
                    element.FindPropertyRelative("label").stringValue = list[index].label;
                    element.FindPropertyRelative("prefab").objectReferenceValue = list[index].prefab;
                    element.FindPropertyRelative("data").objectReferenceValue = list[index].data;
                }
            }
            else
            {
                Assert.Fail($"Unsupported field type for {fieldName}: {value?.GetType().Name}");
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}


