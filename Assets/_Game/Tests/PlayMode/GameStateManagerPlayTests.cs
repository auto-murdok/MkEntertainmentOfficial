using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    public class GameStateManagerPlayTests
    {
        private readonly List<GameObject> _cleanup = new List<GameObject>();
        private GameStateManager _gsm;
        private ZombieSpawner _spawner;

        [SetUp]
        public void SetUp()
        {
            var gsmHost = new GameObject("GameStateManagerHost");
            _gsm = gsmHost.AddComponent<GameStateManager>();
            _cleanup.Add(gsmHost);

            var spawnerHost = new GameObject("ZombieSpawnerHost");
            _spawner = spawnerHost.AddComponent<ZombieSpawner>();
            _gsm.RegisterSpawningToggle(_spawner.SetSpawningEnabled);
            _cleanup.Add(spawnerHost);
        }

        [TearDown]
        public void TearDown()
        {
            // Global state (time scale, cursor, singleton) must not leak
            // into other play-mode tests.
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            foreach (var go in _cleanup)
            {
                if (go != null) Object.DestroyImmediate(go);
            }
            _cleanup.Clear();
        }

        [Test]
        public void SetGameOver_TransitionsStateAndNotifies()
        {
            GameState notified = GameState.Playing;
            _gsm.OnGameStateChanged += s => notified = s;

            _gsm.SetGameOver();

            Assert.AreEqual(GameState.GameOver, _gsm.state);
            Assert.AreEqual(GameState.GameOver, notified);
        }

        [Test]
        public void SetGameOver_ReleasesCursor()
        {
            _gsm.SetGameOver();

            Assert.AreEqual(CursorLockMode.None, Cursor.lockState);
            Assert.IsTrue(Cursor.visible);
        }

        [UnityTest]
        public IEnumerator SetGameOver_FreezesTimeAfterCollapseWindow()
        {
            _gsm.SetGameOver();

            // The ragdoll collapse window runs at normal speed, then freezes.
            Assert.AreEqual(1f, Time.timeScale, "Time must run while the ragdoll collapses.");
            yield return new WaitForSecondsRealtime(2f);
            Assert.AreEqual(0f, Time.timeScale, "Time must freeze after the collapse window.");
        }

        [Test]
        public void SetGameOver_StopsSpawner()
        {
            Assert.IsTrue(_spawner.spawningEnabled);
            _gsm.SetGameOver();
            Assert.IsFalse(_spawner.spawningEnabled);
        }

        [Test]
        public void SetGameOver_CreatesOverlayCanvas()
        {
            _gsm.SetGameOver();

            var canvas = Object.FindFirstObjectByType<Canvas>();
            Assert.IsNotNull(canvas, "Game over overlay canvas was not created.");
            Assert.AreEqual(RenderMode.ScreenSpaceOverlay, canvas.renderMode);
        }

        [Test]
        public void SetGameOver_IsIdempotent()
        {
            _gsm.SetGameOver();
            int notifications = 0;
            _gsm.OnGameStateChanged += _ => notifications++;

            _gsm.SetGameOver();

            Assert.AreEqual(0, notifications, "GameOver must fire the state change only once.");
        }

        [Test]
        public void PlayerDeath_TriggersGameOver()
        {
            var brainHost = new GameObject("TestBrainHost");
            _cleanup.Add(brainHost);
            var brain = brainHost.AddComponent<TestBrain>();
            brain.Died += _gsm.NotifyPlayerDied;

            brain.CallApplyDamage(100f);

            Assert.AreEqual(GameState.GameOver, _gsm.state);
        }

        [Test]
        public void PlayerDeath_SublethalDamage_KeepsGameRunning()
        {
            var brainHost = new GameObject("TestBrainHost");
            _cleanup.Add(brainHost);
            var brain = brainHost.AddComponent<TestBrain>();
            brain.Died += _gsm.NotifyPlayerDied;

            brain.CallApplyDamage(99f);

            Assert.AreEqual(GameState.Playing, _gsm.state);
        }

        [Test]
        public void ZombieSpawner_SetSpawningEnabled_TogglesSpawning()
        {
            _spawner.SetSpawningEnabled(false);
            Assert.IsFalse(_spawner.spawningEnabled);
            _spawner.SetSpawningEnabled(true);
            Assert.IsTrue(_spawner.spawningEnabled);
        }
    }
}
