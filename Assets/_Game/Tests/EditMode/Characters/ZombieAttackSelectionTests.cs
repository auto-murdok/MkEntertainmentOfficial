using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public class ZombieAttackSelectionTests
    {
        [Test]
        public void CanVictimBeBitten_NullVictim_IsTrue()
        {
            StubBiteTarget attacker = new StubBiteTarget(2);
            Assert.IsTrue(ZombieBehavior.CanVictimBeBitten(null, attacker));
        }

        [Test]
        public void CanVictimBeBitten_PlainInteractable_IsTrue()
        {
            StubInteractable victim = new StubInteractable(1);
            StubBiteTarget attacker = new StubBiteTarget(2);
            Assert.IsTrue(ZombieBehavior.CanVictimBeBitten(victim, attacker));
        }

        [Test]
        public void CanVictimBeBitten_FreeBiteTarget_IsTrue()
        {
            StubBiteTarget victim = new StubBiteTarget(1) { canBeBitten = true };
            StubBiteTarget attacker = new StubBiteTarget(2);
            Assert.IsTrue(ZombieBehavior.CanVictimBeBitten(victim, attacker));
        }

        [Test]
        public void CanVictimBeBitten_PinnedByOther_IsFalse()
        {
            StubBiteTarget pinHolder = new StubBiteTarget(2);
            StubBiteTarget victim = new StubBiteTarget(1) { canBeBitten = false, currentBiter = pinHolder };
            StubBiteTarget otherAttacker = new StubBiteTarget(3);
            Assert.IsFalse(ZombieBehavior.CanVictimBeBitten(victim, otherAttacker));
        }

        [Test]
        public void CanVictimBeBitten_PinnedBySelf_IsTrue()
        {
            StubBiteTarget attacker = new StubBiteTarget(2);
            StubBiteTarget victim = new StubBiteTarget(1) { canBeBitten = false, currentBiter = attacker };
            Assert.IsTrue(ZombieBehavior.CanVictimBeBitten(victim, attacker));
        }

        [Test]
        public void CanVictimBeBitten_PinnedByUnknownAttacker_IsFalse()
        {
            StubBiteTarget victim = new StubBiteTarget(1) { canBeBitten = false, currentBiter = null };
            StubBiteTarget otherAttacker = new StubBiteTarget(3);
            Assert.IsFalse(ZombieBehavior.CanVictimBeBitten(victim, otherAttacker));
        }

        [Test]
        public void ZombieData_HandAttackDefaults_AreNonZero()
        {
            ZombieData data = ScriptableObject.CreateInstance<ZombieData>();
            try
            {
                Assert.Greater(data.handAttackDamage, 0f);
                Assert.Greater(data.handAttackRange, 0f);
                Assert.Greater(data.handAttackDuration, 0f);
                Assert.GreaterOrEqual(data.handAttackRange, data.biteRange);
            }
            finally
            {
                Object.DestroyImmediate(data);
            }
        }

        [Test]
        public void HandAttackingState_IsRegisteredInZombieStates()
        {
            Assert.IsTrue(System.Enum.IsDefined(typeof(ZombieStates), "HandAttacking"));
        }
    }
}
