using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Game.Tests.PlayMode
{
    public class MainMenuControllerPlayTests
    {
        private GameObject _host;

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("MainMenuHost");
        }

        [TearDown]
        public void TearDown()
        {
            if (_host != null) Object.DestroyImmediate(_host);
        }

        [Test]
        public void BuildUI_CreatesOverlayCanvasWithButtons()
        {
            MainMenuController controller = _host.AddComponent<MainMenuController>();
            controller.BuildUI();

            Canvas canvas = _host.GetComponentInChildren<Canvas>();
            Assert.IsNotNull(canvas, "Menu canvas was not created.");
            Assert.AreEqual(RenderMode.ScreenSpaceOverlay, canvas.renderMode);

            Button start = FindButton(_host, "StartGameButton");
            Button quit = FindButton(_host, "QuitButton");
            Assert.IsNotNull(start, "Start Game button was not created.");
            Assert.IsNotNull(quit, "Quit button was not created.");
        }

        [Test]
        public void BuildUI_IsIdempotent()
        {
            MainMenuController controller = _host.AddComponent<MainMenuController>();
            controller.BuildUI();
            controller.BuildUI();

            Assert.AreEqual(1, _host.GetComponentsInChildren<Canvas>(true).Length,
                "BuildUI must not stack duplicate canvases.");
        }

        [UnityTest]
        public IEnumerator StartGame_RaisesEventOnceAndGuardsDoubleStart()
        {
            MainMenuController controller = _host.AddComponent<MainMenuController>();
            controller.BuildUI();

            int requests = 0;
            controller.startRequested += () => requests++;

            // StartGame begins the unscaled fade-out; one frame is not enough to
            // reach the async scene load, so no arena scene is opened here.
            controller.StartGame();
            Assert.IsTrue(controller.isTransitioning, "StartGame must enter the transitioning state.");
            Assert.AreEqual(1, requests, "startRequested must fire exactly once.");
            yield return null;

            controller.StartGame();
            Assert.AreEqual(1, requests, "Double StartGame must be a no-op.");
        }

        [Test]
        public void QuitGame_RaisesEventAndEntersTransition()
        {
            MainMenuController controller = _host.AddComponent<MainMenuController>();
            controller.BuildUI();

            int requests = 0;
            controller.quitRequested += () => requests++;

            controller.QuitGame();

            Assert.IsTrue(controller.isTransitioning);
            Assert.AreEqual(1, requests, "quitRequested must fire exactly once.");
        }

        private static Button FindButton(GameObject root, string name)
        {
            foreach (Button button in root.GetComponentsInChildren<Button>(true))
            {
                if (button.name == name)
                {
                    return button;
                }
            }
            return null;
        }
    }
}
