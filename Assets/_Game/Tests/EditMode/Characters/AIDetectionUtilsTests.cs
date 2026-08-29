using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public class AIDetectionUtilsTests
    {
        private GameObject _originHost;

        [SetUp]
        public void SetUp()
        {
            _originHost = new GameObject("DetectionOrigin");
            _originHost.transform.position = Vector3.zero;
            _originHost.transform.rotation = Quaternion.identity;
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_originHost);
        }

        [Test]
        public void IsInLineOfSight_TargetDirectlyAhead_IsTrue()
        {
            bool result = AIDetectionUtils.IsInLineOfSight(_originHost.transform, new Vector3(0f, 0f, 5f), 120f);
            Assert.IsTrue(result);
        }

        [Test]
        public void IsInLineOfSight_TargetOutsideCone_IsFalse()
        {
            // 90 degrees off forward, outside the 60-degree half cone of a 120-degree FOV.
            bool result = AIDetectionUtils.IsInLineOfSight(_originHost.transform, new Vector3(5f, 0f, 0f), 120f);
            Assert.IsFalse(result);
        }

        [Test]
        public void IsInLineOfSight_TargetInsideCone_IsTrue()
        {
            Vector3 target = Quaternion.Euler(0f, 45f, 0f) * Vector3.forward * 5f;
            bool result = AIDetectionUtils.IsInLineOfSight(_originHost.transform, target, 120f);
            Assert.IsTrue(result);
        }

        [Test]
        public void IsInLineOfSight_TargetBehind_IsFalse()
        {
            bool result = AIDetectionUtils.IsInLineOfSight(_originHost.transform, new Vector3(0f, 0f, -5f), 120f);
            Assert.IsFalse(result);
        }

        [Test]
        public void IsInLineOfSight_NarrowCone_RejectsHalfConeEdge()
        {
            // 45 degrees off forward, outside the 30-degree half cone of a 60-degree FOV.
            Vector3 target = Quaternion.Euler(0f, 45f, 0f) * Vector3.forward * 5f;
            bool result = AIDetectionUtils.IsInLineOfSight(_originHost.transform, target, 60f);
            Assert.IsFalse(result);
        }

        [Test]
        public void IsInLineOfSight_NarrowCone_AcceptsInsideTarget()
        {
            // 20 degrees off forward, inside the 30-degree half cone of a 60-degree FOV.
            Vector3 target = Quaternion.Euler(0f, 20f, 0f) * Vector3.forward * 5f;
            bool result = AIDetectionUtils.IsInLineOfSight(_originHost.transform, target, 60f);
            Assert.IsTrue(result);
        }

        [Test]
        public void IsInLineOfSight_InvalidFieldOfView_FallsBackToDefaultCone()
        {
            // 90 degrees off forward is outside the default 120-degree FOV.
            Vector3 side = new Vector3(5f, 0f, 0f);
            bool negativeResult = AIDetectionUtils.IsInLineOfSight(_originHost.transform, side, -10f);
            bool hugeResult = AIDetectionUtils.IsInLineOfSight(_originHost.transform, side, 500f);
            Assert.IsFalse(negativeResult);
            Assert.IsFalse(hugeResult);
        }

        [Test]
        public void IsNotBlockedByObstacles_EmptyObstacleLayer_IsTrue()
        {
            Assert.IsTrue(AIDetectionUtils.IsNotBlockedByObstacles(Vector3.zero, new Vector3(0, 0, 5f), default));
        }

        [Test]
        public void IsNotBlockedByObstacles_ClearLine_IsTrue()
        {
            var emptyLayer = new LayerMask();
            Assert.IsTrue(AIDetectionUtils.IsNotBlockedByObstacles(Vector3.zero, new Vector3(0, 0, 5f), emptyLayer));
        }

        [Test]
        public void DetectViaLineOfSight_NullOrigin_ReturnsDefault()
        {
            var result = AIDetectionUtils.DetectViaLineOfSight<IDamageable>(null, 10f, default, default, 120f);
            Assert.AreEqual(default, result);
        }

        [Test]
        public void ZombieData_DefaultObstacleMask_IncludesDefaultWaterAndZombieLayers()
        {
            Assert.AreEqual(133, ZombieData.DefaultObstacleMask);
        }
    }
}
