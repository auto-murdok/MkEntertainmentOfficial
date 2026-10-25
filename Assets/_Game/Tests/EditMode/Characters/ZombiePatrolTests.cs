using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// The patrol's pure pieces: candidate points must stay on the disc
    /// around home (patrols never drift across the map) and pause rolls must
    /// stay inside the configured range.
    /// </summary>
    public class ZombiePatrolTests
    {
        [Test]
        public void CandidatePoint_UnitVectorLandsOnRadius()
        {
            Vector3 home = new Vector3(10f, 0f, -4f);
            Vector3 point = ZombiePatrol.CandidatePoint(home, 8f, new Vector2(1f, 0f));
            Assert.AreEqual(new Vector3(18f, 0f, -4f), point);
        }

        [Test]
        public void CandidatePoint_StaysWithinRadius()
        {
            Vector3 home = new Vector3(2f, 0f, 3f);
            Vector2[] samples =
            {
                new Vector2(0.7f, 0.7f), new Vector2(-1f, 0f), new Vector2(0f, -1f),
                new Vector2(0.3f, -0.9f), new Vector2(-0.5f, -0.5f),
            };
            foreach (Vector2 sample in samples)
            {
                Vector3 point = ZombiePatrol.CandidatePoint(home, 8f, sample);
                float distance = Vector3.Distance(home, point);
                Assert.LessOrEqual(distance, 8f + 1e-4f, $"sample {sample} escaped the patrol radius");
                Assert.AreEqual(0f, point.y, 0f, "patrol points stay on the ground plane of home");
            }
        }

        [Test]
        public void CandidatePoint_ZeroVectorReturnsHome()
        {
            Vector3 home = new Vector3(-6f, 0f, 11f);
            Assert.AreEqual(home, ZombiePatrol.CandidatePoint(home, 8f, Vector2.zero));
        }

        [Test]
        public void RandomPause_HitsRangeEndpointsAndMidpoint()
        {
            Assert.AreEqual(2f, ZombiePatrol.RandomPause(2f, 5f, 0f), 1e-5f);
            Assert.AreEqual(5f, ZombiePatrol.RandomPause(2f, 5f, 1f), 1e-5f);
            Assert.AreEqual(3.5f, ZombiePatrol.RandomPause(2f, 5f, 0.5f), 1e-5f);
        }

        [Test]
        public void RandomPause_ClampsOutOfRangeRolls()
        {
            Assert.AreEqual(2f, ZombiePatrol.RandomPause(2f, 5f, -3f), 1e-5f);
            Assert.AreEqual(5f, ZombiePatrol.RandomPause(2f, 5f, 7f), 1e-5f);
        }
    }
}
