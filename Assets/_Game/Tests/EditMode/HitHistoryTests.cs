using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    // Lag-compensation primitives: the ring-buffer sampling (which pose
    // does a rewound shot test against?) and the ray-vs-capsule hit test.
    public class HitHistoryTests
    {
        [Test]
        public void Sample_ExactTick_ReturnsThatSample()
        {
            var history = new HitHistory();
            history.Record(10, new Vector3(1f, 0f, 0f), 0.5f, 0.4f);
            history.Record(11, new Vector3(2f, 0f, 0f), 0.5f, 0.4f);

            (Vector3 center, float radius, float _) = history.Sample(10);

            Assert.AreEqual(new Vector3(1f, 0f, 0f), center);
            Assert.AreEqual(0.5f, radius);
        }

        [Test]
        public void Sample_BetweenTicks_InterpolatesCenter()
        {
            var history = new HitHistory();
            history.Record(10, Vector3.zero, 0.5f, 0.4f);
            history.Record(12, new Vector3(2f, 0f, 0f), 0.5f, 0.4f);

            (Vector3 center, float _, float _) = history.Sample(11);

            Assert.AreEqual(1f, center.x, 0.001f);
        }

        [Test]
        public void Sample_OlderThanOldest_ClampsToOldest()
        {
            var history = new HitHistory();
            history.Record(10, new Vector3(1f, 0f, 0f), 0.5f, 0.4f);
            history.Record(11, new Vector3(2f, 0f, 0f), 0.5f, 0.4f);

            (Vector3 center, float _, float _) = history.Sample(3);

            Assert.AreEqual(new Vector3(1f, 0f, 0f), center);
        }

        [Test]
        public void Sample_NewerThanNewest_ClampsToNewest()
        {
            var history = new HitHistory();
            history.Record(10, new Vector3(1f, 0f, 0f), 0.5f, 0.4f);
            history.Record(11, new Vector3(2f, 0f, 0f), 0.5f, 0.4f);

            (Vector3 center, float _, float _) = history.Sample(99);

            Assert.AreEqual(new Vector3(2f, 0f, 0f), center);
        }

        [Test]
        public void RaycastCapsule_HeadOn_HitsAtCapsuleDistance()
        {
            bool hit = HitHistory.RaycastCapsule(
                new Vector3(-5f, 1f, 0f), Vector3.right,
                new Vector3(0f, 1f, 0f), 0.5f, 0.5f, 30f, out float distance);

            Assert.IsTrue(hit);
            Assert.AreEqual(5f, distance, 0.01f);
        }

        [Test]
        public void RaycastCapsule_OffsetWithinRadius_Hits()
        {
            bool hit = HitHistory.RaycastCapsule(
                new Vector3(-5f, 1f, 0.4f), Vector3.right,
                new Vector3(0f, 1f, 0f), 0.5f, 0.5f, 30f, out _);

            Assert.IsTrue(hit);
        }

        [Test]
        public void RaycastCapsule_OffsetBeyondRadius_Misses()
        {
            bool hit = HitHistory.RaycastCapsule(
                new Vector3(-5f, 1f, 0.6f), Vector3.right,
                new Vector3(0f, 1f, 0f), 0.5f, 0.5f, 30f, out _);

            Assert.IsFalse(hit);
        }

        [Test]
        public void RaycastCapsule_AboveTheCap_Misses()
        {
            // Spine tops out at y = 1.5; a ray at y = 3 passes 1.5 above it.
            bool hit = HitHistory.RaycastCapsule(
                new Vector3(-5f, 3f, 0f), Vector3.right,
                new Vector3(0f, 1f, 0f), 0.5f, 0.5f, 30f, out _);

            Assert.IsFalse(hit);
        }

        [Test]
        public void RaycastCapsule_TargetBehindTheRay_Misses()
        {
            bool hit = HitHistory.RaycastCapsule(
                new Vector3(5f, 1f, 0f), Vector3.right,
                new Vector3(0f, 1f, 0f), 0.5f, 0.5f, 30f, out _);

            Assert.IsFalse(hit);
        }

        [Test]
        public void RaycastCapsule_BeyondMaxDistance_Misses()
        {
            bool hit = HitHistory.RaycastCapsule(
                new Vector3(-50f, 1f, 0f), Vector3.right,
                new Vector3(0f, 1f, 0f), 0.5f, 0.5f, 30f, out _);

            Assert.IsFalse(hit, "The capsule is 50 m away; the ray segment ends at 30 m");
        }
    }
}
