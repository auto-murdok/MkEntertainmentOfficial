using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// The agent-driven chase cap: the agent may never move the body faster
    /// than the active gait can honestly play (natural speed x the
    /// playback-rate ceiling), so chasing cannot reintroduce foot slide.
    /// </summary>
    public class ZombieChasingStateTests
    {
        [Test]
        public void EffectiveChaseSpeed_CapsAtGaitCeiling()
        {
            // Walker chase 3.5 against the creep gait (1.039): capped to
            // 1.039 x 2.0, not the paper 3.5 the clip cannot play.
            Assert.AreEqual(1.039f * ZombieAnimationVariant.MaxRate,
                ZombieChasingState.EffectiveChaseSpeed(3.5f, 1.039f), 1e-4f);
            // Runner chase 5.5 against the stiff gait (0.66) caps hardest.
            Assert.AreEqual(0.66f * ZombieAnimationVariant.MaxRate,
                ZombieChasingState.EffectiveChaseSpeed(5.5f, 0.66f), 1e-4f);
        }

        [Test]
        public void EffectiveChaseSpeed_UnderCeilingPassesThrough()
        {
            Assert.AreEqual(1.5f, ZombieChasingState.EffectiveChaseSpeed(1.5f, 1.039f), 1e-4f);
        }

        [Test]
        public void EffectiveChaseSpeed_UnknownNaturalLeavesDataSpeed()
        {
            Assert.AreEqual(3.5f, ZombieChasingState.EffectiveChaseSpeed(3.5f, 0f), 1e-4f);
            Assert.AreEqual(3.5f, ZombieChasingState.EffectiveChaseSpeed(3.5f, -1f), 1e-4f);
        }
    }
}
