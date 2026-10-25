using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// The head-look weight math is pure so every peer eases the gaze
    /// identically: ramp rate is derived from the ramp duration across the
    /// full weight range, and the angular falloff keeps the head from trying
    /// to look where the body should turn instead.
    /// </summary>
    public class ZombieHeadLookTests
    {
        [Test]
        public void RampWeight_MovesTowardGoalAtRampRate()
        {
            // Full range 0.85 in 0.3 s -> a 0.1 s step moves ~0.2833.
            float w = ZombieHeadLook.RampWeight(0f, 0.85f, 0.85f, 0.3f, 0.1f);
            Assert.AreEqual(0.85f / 0.3f * 0.1f, w, 1e-4f);
        }

        [Test]
        public void RampWeight_DoesNotOvershootGoal()
        {
            float w = ZombieHeadLook.RampWeight(0.8f, 0.85f, 0.85f, 0.3f, 1f);
            Assert.AreEqual(0.85f, w, 1e-5f);
        }

        [Test]
        public void RampWeight_RampsDownToZero()
        {
            float w = ZombieHeadLook.RampWeight(0.85f, 0f, 0.85f, 0.5f, 0.25f);
            Assert.AreEqual(0.425f, w, 1e-4f);
            w = ZombieHeadLook.RampWeight(w, 0f, 0.85f, 0.5f, 1f);
            Assert.AreEqual(0f, w, 1e-5f);
        }

        [Test]
        public void RampWeight_ClampsGoalToMaxWeight()
        {
            float w = ZombieHeadLook.RampWeight(0f, 5f, 0.85f, 0.3f, 10f);
            Assert.AreEqual(0.85f, w, 1e-5f);
        }

        [Test]
        public void AngleFalloff_FullInsideFadeStart()
        {
            Assert.AreEqual(1f, ZombieHeadLook.AngleFalloff(30f, 55f, 75f));
            Assert.AreEqual(1f, ZombieHeadLook.AngleFalloff(55f, 55f, 75f));
        }

        [Test]
        public void AngleFalloff_ZeroAtAndBeyondFadeEnd()
        {
            Assert.AreEqual(0f, ZombieHeadLook.AngleFalloff(75f, 55f, 75f));
            Assert.AreEqual(0f, ZombieHeadLook.AngleFalloff(120f, 55f, 75f));
        }

        [Test]
        public void AngleFalloff_HalfwayBetweenFadeBounds()
        {
            Assert.AreEqual(0.5f, ZombieHeadLook.AngleFalloff(65f, 55f, 75f), 1e-5f);
        }
    }
}
