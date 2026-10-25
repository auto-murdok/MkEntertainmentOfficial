using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public class AimSensitivityTests
    {
        [Test]
        public void FovRatioScale_ProjectPair_MatchesFocalLengthRatio()
        {
            // The project's cameras: follow FOV 40, aim FOV 30.
            // tan(15 deg) / tan(20 deg) = 0.736.
            Assert.AreEqual(0.736f, AimSensitivity.FovRatioScale(40f, 30f), 0.002f);
        }

        [Test]
        public void FovRatioScale_SameFov_IsOne()
        {
            Assert.AreEqual(1f, AimSensitivity.FovRatioScale(40f, 40f), 0.0001f);
        }

        [Test]
        public void FovRatioScale_ZoomingOut_IsInverse()
        {
            Assert.AreEqual(1.359f, AimSensitivity.FovRatioScale(30f, 40f), 0.002f);
        }

        [Test]
        public void FovRatioScale_DegenerateInput_IsOne()
        {
            Assert.AreEqual(1f, AimSensitivity.FovRatioScale(0f, 30f));
            Assert.AreEqual(1f, AimSensitivity.FovRatioScale(40f, 0f));
            Assert.AreEqual(1f, AimSensitivity.FovRatioScale(-10f, 30f));
            Assert.AreEqual(1f, AimSensitivity.FovRatioScale(40f, 180f));
        }
    }
}
