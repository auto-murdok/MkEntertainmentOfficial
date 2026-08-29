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
            bool result = AIDetectionUtils.IsInLineOfSight(_originHost.transform, new Vector3(0f, 0f, 5f), 0, 180);
            Assert.IsTrue(result);
        }

        [Test]
        public void IsInLineOfSight_TargetOutsideDefaultCone_IsFalse()
        {
            bool result = AIDetectionUtils.IsInLineOfSight(_originHost.transform, new Vector3(5f, 0f, 0f), 0, 180);
            Assert.IsFalse(result);
        }

        [Test]
        public void IsInLineOfSight_TargetInsideDefaultCone_IsTrue()
        {
            Vector3 target = Quaternion.Euler(0f, 45f, 0f) * Vector3.forward * 5f;
            bool result = AIDetectionUtils.IsInLineOfSight(_originHost.transform, target, 0, 180);
            Assert.IsTrue(result);
        }

        [Test]
        public void IsInLineOfSight_TargetBehind_IsFalse()
        {
            bool result = AIDetectionUtils.IsInLineOfSight(_originHost.transform, new Vector3(0f, 0f, -5f), 0, 180);
            Assert.IsFalse(result);
        }

        [Test]
        public void IsInLineOfSight_MinPositiveWithMax180_UsesBackwardHalfCone()
        {
            Vector3 behind = Quaternion.Euler(0f, 160f, 0f) * Vector3.forward * 5f;
            bool result = AIDetectionUtils.IsInLineOfSight(_originHost.transform, behind, 10, 180);
            Assert.IsTrue(result);
        }

        [Test]
        public void IsInLineOfSight_InvalidAngleRange_FallsBackToDefaultCone()
        {
            Vector3 side = new Vector3(5f, 0f, 0f);
            bool result = AIDetectionUtils.IsInLineOfSight(_originHost.transform, side, -10, 500);
            Assert.IsFalse(result);
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
            var result = AIDetectionUtils.DetectViaLineOfSight<IDamageable>(null, 10f, default, default, 0, 180);
            Assert.AreEqual(default, result);
        }
    }
}
