using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public class AmmoTests
    {
        private GameObject _host;
        private Ammo _ammo;

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("AmmoHost");
            _ammo = _host.AddComponent<Ammo>();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_host);
        }

        private void SetQuantity(int quantity)
        {
            var so = new UnityEditor.SerializedObject(_ammo);
            so.FindProperty("_quantity").intValue = quantity;
            so.ApplyModifiedProperties();
        }

        [Test]
        public void GetNextClip_PartialDraw_ReducesQuantity()
        {
            SetQuantity(10);
            Assert.AreEqual(4, _ammo.GetNextClip(4));
            Assert.AreEqual(6, _ammo.GetNextClip(6));
        }

        [Test]
        public void GetNextClip_RequestLargerThanReserve_ReturnsReserve()
        {
            SetQuantity(3);
            Assert.AreEqual(3, _ammo.GetNextClip(30));
            Assert.AreEqual(0, _ammo.GetNextClip(1));
        }

        [Test]
        public void GetNextClip_ExactlyEmptiesReserve()
        {
            SetQuantity(5);
            Assert.AreEqual(5, _ammo.GetNextClip(5));
            Assert.AreEqual(0, _ammo.GetNextClip(1));
        }

        [Test]
        public void GetNextClip_EmptyReserve_ReturnsZero()
        {
            SetQuantity(0);
            Assert.AreEqual(0, _ammo.GetNextClip(5));
        }

        [TestCase(10, 1, 1, 9)]
        [TestCase(10, 10, 10, 0)]
        [TestCase(10, 11, 10, 0)]
        [TestCase(7, 3, 3, 4)]
        public void GetNextClip_Parameterized_MatchesExpectedMath(int quantity, int request, int expectedTake, int expectedRemaining)
        {
            SetQuantity(quantity);
            Assert.AreEqual(expectedTake, _ammo.GetNextClip(request));
            Assert.AreEqual(expectedRemaining, _ammo.GetNextClip(expectedRemaining));
        }
    }
}
