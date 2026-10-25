using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// The variant derivation is the one piece of ZombieAnimationVariant
    /// every peer must agree on bit-for-bit: same seed, same zombie.
    /// </summary>
    public class ZombieAnimationVariantTests
    {
        [Test]
        public void Derive_IsDeterministic()
        {
            ZombieAnimationVariant.Derive(1234567, 3, 2, out int walkA, out int idleA, out float phaseA);
            ZombieAnimationVariant.Derive(1234567, 3, 2, out int walkB, out int idleB, out float phaseB);

            Assert.AreEqual(walkA, walkB);
            Assert.AreEqual(idleA, idleB);
            Assert.AreEqual(phaseA, phaseB);
        }

        [Test]
        public void Derive_StaysInRange()
        {
            for (int seed = 1; seed <= 5000; seed++)
            {
                ZombieAnimationVariant.Derive(seed, 3, 2, out int walk, out int idle, out float phase);

                Assert.That(walk, Is.InRange(0, 2), $"seed {seed}");
                Assert.That(idle, Is.InRange(0, 1), $"seed {seed}");
                Assert.That(phase, Is.InRange(0f, 1f), $"seed {seed}");
            }
        }

        [Test]
        public void Derive_UsesEveryVariant()
        {
            var walkCounts = new int[3];
            var idleCounts = new int[2];

            for (int seed = 1; seed <= 3000; seed++)
            {
                ZombieAnimationVariant.Derive(seed, 3, 2, out int walk, out int idle, out _);
                walkCounts[walk]++;
                idleCounts[idle]++;
            }

            // Loose uniformity: the hash must not starve any clip.
            foreach (int count in walkCounts)
            {
                Assert.That(count, Is.GreaterThan(500), "every walk variant must be reachable");
            }
            foreach (int count in idleCounts)
            {
                Assert.That(count, Is.GreaterThan(500), "every idle variant must be reachable");
            }
        }

        [Test]
        public void Derive_NoVariantsConfigured_ReturnsMinusOne()
        {
            ZombieAnimationVariant.Derive(42, 0, 0, out int walk, out int idle, out float phase);

            Assert.AreEqual(-1, walk);
            Assert.AreEqual(-1, idle);
            Assert.That(phase, Is.InRange(0f, 1f));
        }
    }
}
