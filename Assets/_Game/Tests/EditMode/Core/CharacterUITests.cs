using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public class CharacterUITests
    {
        private GameObject _host;
        private CharacterUIController _controller;

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("UIHost");
            _controller = _host.AddComponent<CharacterUIController>();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_host);
        }

        [Test]
        public void CreateAimUI_SetsCrosshairFlagOnly()
        {
            var context = CharacterUIContext.CreateAimUI(true);
            Assert.IsTrue(context.displayCrosshair);
            Assert.AreEqual(0, context.clipSize);
            Assert.AreEqual(0, context.maxClipSize);
        }

        [Test]
        public void CreateShootUI_SetsClipCountsOnly()
        {
            var context = CharacterUIContext.CreateShootUI(3, 7);
            Assert.IsFalse(context.displayCrosshair);
            Assert.AreEqual(3, context.clipSize);
            Assert.AreEqual(7, context.maxClipSize);
        }

        [Test]
        public void UpdateUI_NotifiesObserversWithAimElement()
        {
            var observer = new RecordingUIObserver();
            _controller.AddObserver(observer);
            var context = CharacterUIContext.CreateAimUI(false);
            _controller.UpdateUI(context);

            Assert.AreEqual(1, observer.Elements.Count);
            Assert.AreEqual(CharacterUIElement.AimUI, observer.Elements[0]);
            Assert.IsFalse(observer.Contexts[0].displayCrosshair);
        }

        [Test]
        public void CharacterUIElement_EnumValues()
        {
            Assert.AreEqual(0, (int)CharacterUIElement.AimUI);
            Assert.AreEqual(1, (int)CharacterUIElement.ShootUI);
        }
    }
}
