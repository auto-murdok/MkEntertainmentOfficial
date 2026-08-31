using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public class GameCliArgsTests
    {
        [TearDown]
        public void TearDown()
        {
            GameCliArgs.ResetForTesting();
            GameCliArgs.Initialize();
            NetworkSession.ResetOverrides();
        }

        [Test]
        public void HasFlag_IsCaseInsensitiveAndDashInsensitive()
        {
            GameCliArgs.SetArgsForTesting("exe", "--Verbose");
            Assert.IsTrue(GameCliArgs.HasFlag("verbose"));
            Assert.IsTrue(GameCliArgs.HasFlag("VERBOSE"));
            Assert.IsTrue(GameCliArgs.HasFlag("Verbose"));
            Assert.IsTrue(GameCliArgs.IsVerbose);
        }

        [Test]
        public void GetValue_SpaceSeparatedAndEqualsSpelling_BothWork()
        {
            GameCliArgs.SetArgsForTesting("exe", "--scene", "NetworkedCombatArena");
            Assert.AreEqual("NetworkedCombatArena", GameCliArgs.RequestedScene);
            Assert.AreEqual("NetworkedCombatArena", GameCliArgs.GetValue("scene"));

            GameCliArgs.SetArgsForTesting("exe", "--scene=ExpandedCombatArena");
            Assert.AreEqual("ExpandedCombatArena", GameCliArgs.RequestedScene);

            GameCliArgs.SetArgsForTesting("exe", "-scene", "MainMenu");
            Assert.AreEqual("MainMenu", GameCliArgs.RequestedScene);
        }

        [Test]
        public void NetworkingMode_FromLegacyFlags()
        {
            GameCliArgs.SetArgsForTesting("exe", "-mlclient");
            Assert.IsTrue(GameCliArgs.IsClientFlag);
            Assert.AreEqual(NetworkSessionMode.Client, GameCliArgs.NetworkingModeOverride);

            GameCliArgs.SetArgsForTesting("exe", "--client");
            Assert.AreEqual(NetworkSessionMode.Client, GameCliArgs.NetworkingModeOverride);

            GameCliArgs.SetArgsForTesting("exe", "--host");
            Assert.AreEqual(NetworkSessionMode.Host, GameCliArgs.NetworkingModeOverride);
        }

        [Test]
        public void NetworkingMode_ExplicitModeWinsOverLegacy()
        {
            GameCliArgs.SetArgsForTesting("exe", "--mode", "host");
            Assert.AreEqual(NetworkSessionMode.Host, GameCliArgs.NetworkingModeOverride);

            GameCliArgs.SetArgsForTesting("exe", "--mode", "client");
            Assert.AreEqual(NetworkSessionMode.Client, GameCliArgs.NetworkingModeOverride);

            GameCliArgs.SetArgsForTesting("exe", "--mode", "auto");
            Assert.AreEqual(NetworkSessionMode.Auto, GameCliArgs.NetworkingModeOverride);
        }

        [Test]
        public void ConnectAddressAndPort_FromConnectAndSplitForms()
        {
            GameCliArgs.SetArgsForTesting("exe", "--connect", "192.168.1.10:8888");
            Assert.AreEqual("192.168.1.10", GameCliArgs.ConnectAddress);
            Assert.AreEqual(8888, GameCliArgs.ConnectPort);

            GameCliArgs.SetArgsForTesting("exe", "--address", "10.0.0.5", "--port", "7779");
            Assert.AreEqual("10.0.0.5", GameCliArgs.ConnectAddress);
            Assert.AreEqual(7779, GameCliArgs.ConnectPort);

            GameCliArgs.SetArgsForTesting("exe", "--connect", "127.0.0.1");
            Assert.AreEqual("127.0.0.1", GameCliArgs.ConnectAddress);
            Assert.IsNull(GameCliArgs.ConnectPort);
        }

        [Test]
        public void AutoQuit_AcceptsAllAliases()
        {
            GameCliArgs.SetArgsForTesting("exe", "--autoQuit", "30");
            Assert.AreEqual(30f, GameCliArgs.AutoQuitAfterSeconds);

            GameCliArgs.SetArgsForTesting("exe", "--quitAfter", "15");
            Assert.AreEqual(15f, GameCliArgs.AutoQuitAfterSeconds);

            GameCliArgs.SetArgsForTesting("exe", "--maxDuration", "10");
            Assert.AreEqual(10f, GameCliArgs.AutoQuitAfterSeconds);

            GameCliArgs.SetArgsForTesting("exe", "--exitAfter", "5");
            Assert.AreEqual(5f, GameCliArgs.AutoQuitAfterSeconds);
        }

        [Test]
        public void GameplayOverrides_ParsedCorrectly()
        {
            GameCliArgs.SetArgsForTesting("exe", "--noSpawning");
            Assert.IsTrue(GameCliArgs.NoSpawning);

            GameCliArgs.SetArgsForTesting("exe", "--godMode");
            Assert.IsTrue(GameCliArgs.GodMode);

            GameCliArgs.SetArgsForTesting("exe", "--infiniteAmmo");
            Assert.IsTrue(GameCliArgs.InfiniteAmmo);

            GameCliArgs.SetArgsForTesting("exe", "--maxZombies", "5");
            Assert.AreEqual(5, GameCliArgs.MaxZombiesOverride);

            GameCliArgs.SetArgsForTesting("exe", "--spawnInterval", "10");
            Assert.AreEqual(10f, GameCliArgs.SpawnIntervalOverride);

            GameCliArgs.SetArgsForTesting("exe", "--timeScale", "2.5");
            Assert.AreEqual(2.5f, GameCliArgs.TimeScaleOverride);

            GameCliArgs.SetArgsForTesting("exe", "--seed", "12345");
            Assert.AreEqual(12345, GameCliArgs.SeedOverride);
        }

        [Test]
        public void HelpFlag_AndHelpText()
        {
            GameCliArgs.SetArgsForTesting("exe", "--help");
            Assert.IsTrue(GameCliArgs.IsHelpRequested);
            Assert.IsNotEmpty(GameCliArgs.HelpText);
            Assert.IsTrue(GameCliArgs.HelpText.Contains("--scene"));

            GameCliArgs.SetArgsForTesting("exe", "-h");
            Assert.IsTrue(GameCliArgs.IsHelpRequested);
        }

        [Test]
        public void IsAutomated_FalseForPlainLaunch()
        {
            GameCliArgs.SetArgsForTesting("exe");
            // In EditMode, Application.isBatchMode is false, so only --automated triggers it.
            Assert.IsFalse(GameCliArgs.IsAutomated);

            GameCliArgs.SetArgsForTesting("exe", "--automated");
            Assert.IsTrue(GameCliArgs.IsAutomated);
        }

        [Test]
        public void UnknownFlag_DoesNotThrow()
        {
            GameCliArgs.SetArgsForTesting("exe", "--thisFlagDoesNotExistAtAll");
            Assert.IsFalse(GameCliArgs.HasFlag("scene"));
            Assert.IsNull(GameCliArgs.RequestedScene);
        }

        [Test]
        public void NetworkArenaBootstrap_IsCommandLineClient_UsesCli()
        {
            GameCliArgs.SetArgsForTesting("exe", "--mode", "client");
            Assert.IsTrue(NetworkArenaBootstrap.IsCommandLineClient());

            GameCliArgs.SetArgsForTesting("exe", "--mode", "host");
            Assert.IsFalse(NetworkArenaBootstrap.IsCommandLineClient());

            GameCliArgs.SetArgsForTesting("exe", "--mlclient");
            Assert.IsTrue(NetworkArenaBootstrap.IsCommandLineClient());
        }

        [Test]
        public void NetworkSession_EffectiveAddressAndPort_FallbackToConst()
        {
            NetworkSession.ResetOverrides();
            Assert.AreEqual(NetworkSession.ServerAddress, NetworkSession.EffectiveAddress);
            Assert.AreEqual(NetworkSession.ServerPort, NetworkSession.EffectivePort);

            NetworkSession.OverrideAddress = "10.0.0.1";
            NetworkSession.OverridePort = 9000;
            Assert.AreEqual("10.0.0.1", NetworkSession.EffectiveAddress);
            Assert.AreEqual(9000, NetworkSession.EffectivePort);
        }
    }
}
