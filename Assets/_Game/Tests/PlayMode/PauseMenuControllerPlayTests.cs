using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Game.Tests.PlayMode
{
    public class PauseMenuControllerPlayTests
    {
        private GameObject _host;

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("PauseMenuHost");
        }

        [TearDown]
        public void TearDown()
        {
            if (_host != null) Object.DestroyImmediate(_host);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        // AddComponent never runs Awake in the editor, and this controller
        // builds lazily — mirror the runtime flow by opening it once.
        private PauseMenuController CreateOpenedController()
        {
            PauseMenuController controller = _host.AddComponent<PauseMenuController>();
            controller.SetOpen(true);
            return controller;
        }

        [Test]
        public void EnsureBuilt_CreatesOverlayWithButtons_ClosedByDefault()
        {
            PauseMenuController controller = _host.AddComponent<PauseMenuController>();
            controller.EnsureBuilt();

            Canvas canvas = _host.GetComponentInChildren<Canvas>();
            Assert.IsNotNull(canvas, "Pause menu canvas was not created.");
            Assert.AreEqual(RenderMode.ScreenSpaceOverlay, canvas.renderMode);
            Assert.IsNotNull(FindButton(_host, "ResumeButton"), "Resume button was not created.");
            Assert.IsNotNull(FindButton(_host, "QuitToMenuButton"), "Quit-to-menu button was not created.");

            Assert.IsFalse(controller.isOpen, "Overlay must start closed.");
            CanvasGroup group = canvas.GetComponent<CanvasGroup>();
            Assert.IsFalse(group.blocksRaycasts, "Closed overlay must not block gameplay clicks.");
        }

        [Test]
        public void SetOpen_TogglesVisibilityAndCursor()
        {
            PauseMenuController controller = CreateOpenedController();

            Assert.IsTrue(controller.isOpen);
            CanvasGroup group = _host.GetComponentInChildren<Canvas>().GetComponent<CanvasGroup>();
            Assert.AreEqual(1f, group.alpha);
            Assert.IsTrue(group.interactable);
            Assert.AreEqual(CursorLockMode.None, Cursor.lockState, "Opening the menu must release the cursor.");

            controller.SetOpen(false);
            Assert.IsFalse(controller.isOpen);
            Assert.AreEqual(0f, group.alpha);
            Assert.IsFalse(group.blocksRaycasts);
            Assert.AreEqual(CursorLockMode.Locked, Cursor.lockState, "Closing the menu must re-lock the cursor.");
        }

        [Test]
        public void Toggle_FlipsState()
        {
            PauseMenuController controller = _host.AddComponent<PauseMenuController>();
            controller.Toggle();
            Assert.IsTrue(controller.isOpen);
            controller.Toggle();
            Assert.IsFalse(controller.isOpen);
        }

        [Test]
        public void Resume_RaisesEventOnceAndCloses()
        {
            PauseMenuController controller = CreateOpenedController();

            int requests = 0;
            controller.resumeRequested += () => requests++;

            controller.Resume();
            Assert.IsFalse(controller.isOpen, "Resume must close the overlay.");
            Assert.AreEqual(1, requests, "resumeRequested must fire exactly once.");

            // Resume on an already-closed overlay is a no-op.
            controller.Resume();
            Assert.AreEqual(1, requests);
        }

        [Test]
        public void QuitToMenu_RaisesEventOnceAndGuardsDoubleQuit()
        {
            PauseMenuController controller = _host.AddComponent<PauseMenuController>();
            SetQuitLoadsScene(controller, false); // do not load MainMenu inside the test run
            controller.SetOpen(true);

            int requests = 0;
            controller.quitToMenuRequested += () => requests++;

            controller.QuitToMenu();
            Assert.IsTrue(controller.isQuitting);
            Assert.AreEqual(1, requests, "quitToMenuRequested must fire exactly once.");

            controller.QuitToMenu();
            Assert.AreEqual(1, requests, "Double QuitToMenu must be a no-op.");

            // A quitting controller cannot be re-opened.
            controller.SetOpen(true);
            Assert.IsFalse(controller.isOpen);
        }

        private static void SetQuitLoadsScene(PauseMenuController controller, bool value)
        {
            var so = new UnityEditor.SerializedObject(controller);
            so.FindProperty("_quitLoadsMenuScene").boolValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
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
