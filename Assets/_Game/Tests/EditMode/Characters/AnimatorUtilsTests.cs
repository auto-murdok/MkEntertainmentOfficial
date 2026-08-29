using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public class AnimatorUtilsTests
    {
        private GameObject _host;
        private Animator _animator;

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("AnimatorHost");
            _animator = _host.AddComponent<Animator>();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_host);
        }

        [Test]
        public void Hashes_AreStableAndNonZero()
        {
            Assert.AreEqual(Animator.StringToHash("Horizontal"), AnimatorUtils.HorizontalHash);
            Assert.AreEqual(Animator.StringToHash("Vertical"), AnimatorUtils.VerticalHash);
            Assert.AreEqual(Animator.StringToHash("isReloading"), AnimatorUtils.IsReloadingHash);
            Assert.AreEqual(Animator.StringToHash("Bite"), AnimatorUtils.BiteHash);
            Assert.AreEqual(Animator.StringToHash("TakeBite"), AnimatorUtils.TakeBiteHash);
        }

        [Test]
        public void DampFactor_ZeroSpeed_IsZero()
        {
            Assert.AreEqual(0f, AnimatorUtils.DampFactor(0f, 0.016f));
        }

        [Test]
        public void DampFactor_NegativeDeltaTime_ClampsToZero()
        {
            Assert.AreEqual(0f, AnimatorUtils.DampFactor(5f, -1f));
        }

        [Test]
        public void DampFactor_KnownValues_MatchExponentialDecay()
        {
            Assert.AreEqual(1f - Mathf.Exp(-1f), AnimatorUtils.DampFactor(10f, 0.1f), 0.0001f);
            Assert.AreEqual(1f - Mathf.Exp(-5f), AnimatorUtils.DampFactor(5f, 1f), 0.0001f);
        }

        [Test]
        public void DampFactor_MonotonicallyIncreasesWithSpeed()
        {
            Assert.Less(AnimatorUtils.DampFactor(1f, 0.1f), AnimatorUtils.DampFactor(10f, 0.1f));
        }

        [Test]
        public void SetMovementRootMotion_NullAnimator_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => AnimatorUtils.SetMovementRootMotion(null, Vector2.one, 10f));
        }

        [Test]
        public void DisableMovementRootMotion_NullAnimator_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => AnimatorUtils.DisableMovementRootMotion(null, 10f));
        }

        [Test]
        public void SetLayerWeight_NullAnimator_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => AnimatorUtils.SetLayerWeight(null, 0, 1f, 10f));
        }

        [Test]
        public void SetMovementRootMotion_WithAnimator_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => AnimatorUtils.SetMovementRootMotion(_animator, new Vector2(0.25f, 0.75f), 10f));
        }
    }
}
