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
            NetworkSession.desiredMode = NetworkSessionMode.Auto;
        }

        [TearDown]
        public void TearDown()
        {
            if (_host != null) Object.DestroyImmediate(_host);
            NetworkSession.desiredMode = NetworkSessionMode.Auto;
        }

        [Test]
        public void BuildUI_CreatesOverlayCanvasWithButtons()
        {
            MainMenuController controller = _host.AddComponent<MainMenuController>();
            controller.BuildUI();

            Canvas canvas = _host.GetComponentInChildren<Canvas>();
            Assert.IsNotNull(canvas, "Menu canvas was not created.");
            Assert.AreEqual(RenderMode.ScreenSpaceOverlay, canvas.renderMode);

            Assert.IsNotNull(FindButton(_host, "StartGameButton"), "Start Game button was not created.");
            Assert.IsNotNull(FindButton(_host, "HostButton"), "Host button was not created.");
            Assert.IsNotNull(FindButton(_host, "JoinButton"), "Join button was not created.");
            Assert.IsNotNull(FindButton(_host, "QuitButton"), "Quit button was not created.");
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

        [UnityTest]
        public IEnumerator HostGame_SetsHostModeRaisesEventAndGuardsDoubleStart()
        {
            MainMenuController controller = _host.AddComponent<MainMenuController>();
            controller.BuildUI();

            int requests = 0;
            controller.hostRequested += () => requests++;

            controller.HostGame();
            Assert.AreEqual(NetworkSessionMode.Host, NetworkSession.desiredMode,
                "HostGame must select the host session mode.");
            Assert.IsTrue(controller.isTransitioning);
            Assert.AreEqual(1, requests, "hostRequested must fire exactly once.");
            yield return null;

            controller.HostGame();
            Assert.AreEqual(1, requests, "Double HostGame must be a no-op.");
        }

        [UnityTest]
        public IEnumerator JoinGame_SetsClientModeRaisesEventAndGuardsDoubleStart()
        {
            MainMenuController controller = _host.AddComponent<MainMenuController>();
            controller.BuildUI();

            int requests = 0;
            controller.joinRequested += () => requests++;

            controller.JoinGame();
            Assert.AreEqual(NetworkSessionMode.Client, NetworkSession.desiredMode,
                "JoinGame must select the client session mode.");
            Assert.IsTrue(controller.isTransitioning);
            Assert.AreEqual(1, requests, "joinRequested must fire exactly once.");
            yield return null;

            controller.JoinGame();
            Assert.AreEqual(1, requests, "Double JoinGame must be a no-op.");
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
