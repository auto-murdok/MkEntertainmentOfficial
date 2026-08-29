using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.PlayMode
{
    // Detection sweep on real colliders: verifies the NEAREST in-cone survivor
    // wins regardless of the order the physics sphere overlap reports colliders.
    public class AIDetectionPlayTests
    {
        private const float LaneHeight = 820f;
        private static readonly Vector3 LaneOrigin = new Vector3(0f, LaneHeight, 0f);

        private GameObject _originHost;
        private GameObject _nearGo;
        private GameObject _farGo;

        [SetUp]
        public void SetUp()
        {
            _originHost = new GameObject("DetectionOrigin");
            _originHost.transform.SetPositionAndRotation(LaneOrigin, Quaternion.identity);

            _nearGo = CreateSurvivor("NearSurvivor", LaneOrigin + new Vector3(0f, 0f, 3f));
            _farGo = CreateSurvivor("FarSurvivor", LaneOrigin + new Vector3(0f, 0f, 6f));

            // Tests use the Default layer; sync so the overlap query sees the
            // freshly created colliders.
            Physics.SyncTransforms();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_originHost);
            Object.DestroyImmediate(_nearGo);
            if (_farGo != null) Object.DestroyImmediate(_farGo);
        }

        private static GameObject CreateSurvivor(string name, Vector3 position)
        {
            GameObject go = new GameObject(name);
            go.transform.position = position;
            go.AddComponent<BoxCollider>();
            go.AddComponent<FakePinnedVictim>();
            return go;
        }

        private static ISurvivor Detect(Transform origin)
        {
            // Obstacle mask empty -> never blocked (clear-air lane).
            return AIDetectionUtils.DetectViaLineOfSight<ISurvivor>(
                origin, 10f, 1 << 0, default, AIDetectionUtils.DefaultFieldOfViewAngle);
        }

        [Test]
        public void DetectViaLineOfSight_ReturnsNearestSurvivor()
        {
            ISurvivor result = Detect(_originHost.transform);
            Assert.AreEqual(_nearGo.transform, ((FakePinnedVictim)result).transform);
        }

        [Test]
        public void DetectViaLineOfSight_FallsBackToNextNearestWhenNearRemoved()
        {
            Object.DestroyImmediate(_nearGo);
            Physics.SyncTransforms();

            ISurvivor result = Detect(_originHost.transform);
            Assert.AreEqual(_farGo.transform, ((FakePinnedVictim)result).transform);
        }

        [Test]
        public void DetectViaLineOfSight_SurvivorOutsideCone_IsIgnored()
        {
            // Remove the in-cone survivor, then place the remaining one 90
            // degrees off forward: outside the default 60-degree half cone.
            Object.DestroyImmediate(_farGo);
            _farGo = null;
            _nearGo.transform.position = LaneOrigin + new Vector3(6f, 0f, 0f);
            Physics.SyncTransforms();

            ISurvivor result = Detect(_originHost.transform);
            Assert.IsNull(result);
        }
    }
}
