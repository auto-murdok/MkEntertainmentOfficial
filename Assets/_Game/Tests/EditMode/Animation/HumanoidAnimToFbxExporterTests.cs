using System;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Game.Editor.Animation;

namespace Game.Tests.EditMode.Animation
{
    [TestFixture]
    public class HumanoidAnimToFbxExporterTests
    {
        private const string TestClipPath = "Assets/_Game/Animations/Characters/Survivor/Animations/idle.anim";
        private const string TestCharacterPath = "Assets/_Game/Prefabs/Characters/Survivor/FemaleCharacter.prefab";
        private const string TestOutputFolder = "Assets/_Game/Animations/ExportedFBX/Tests";

        private AnimationClip _testClip;
        private GameObject _testCharacter;

        [SetUp]
        public void SetUp()
        {
            _testClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(TestClipPath);
            _testCharacter = AssetDatabase.LoadAssetAtPath<GameObject>(TestCharacterPath);

            if (!Directory.Exists(TestOutputFolder))
            {
                Directory.CreateDirectory(TestOutputFolder);
            }
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(TestOutputFolder))
            {
                Directory.Delete(TestOutputFolder, true);
                AssetDatabase.Refresh();
            }
        }

        [Test]
        public void ExportClip_NullClip_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                HumanoidAnimToFbxExporter.ExportClip(null, _testCharacter);
            });
        }

        [Test]
        public void ExportClip_ValidClip_GeneratesFbxFileOnDisk()
        {
            Assert.IsNotNull(_testClip, "Test animation clip must exist.");
            Assert.IsNotNull(_testCharacter, "Test character prefab must exist.");

            string outPath = Path.Combine(TestOutputFolder, "test_idle.fbx").Replace('\\', '/');
            var options = new HumanoidAnimToFbxExporter.ExportOptions
            {
                outputPath = outPath,
                includeSkinnedMesh = false,
                exportBinary = true
            };

            string exported = HumanoidAnimToFbxExporter.ExportClip(_testClip, _testCharacter, options);

            Assert.IsFalse(string.IsNullOrEmpty(exported), "Exported path must not be null or empty.");
            Assert.IsTrue(File.Exists(outPath), $"Exported FBX file must exist on disk at {outPath}.");

            var fileInfo = new FileInfo(outPath);
            Assert.Greater(fileInfo.Length, 1024, "Exported FBX should have a non-trivial file size (> 1KB).");

            var assets = AssetDatabase.LoadAllAssetsAtPath(outPath);
            bool foundAnimClip = false;
            foreach (var asset in assets)
            {
                if (asset is AnimationClip && !asset.name.StartsWith("__preview__"))
                {
                    foundAnimClip = true;
                    break;
                }
            }
            Assert.IsTrue(foundAnimClip, "Exported FBX must contain a baked AnimationClip sub-asset.");
        }

        [Test]
        public void ExportClip_GuaranteesCleanup_NoLeakedGameObjects()
        {
            int beforeCount = Resources.FindObjectsOfTypeAll<GameObject>().Length;

            string outPath = Path.Combine(TestOutputFolder, "test_cleanup.fbx").Replace('\\', '/');
            var options = new HumanoidAnimToFbxExporter.ExportOptions
            {
                outputPath = outPath,
                includeSkinnedMesh = false,
                exportBinary = true
            };

            HumanoidAnimToFbxExporter.ExportClip(_testClip, _testCharacter, options);

            var tempBakeObjects = GameObject.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var go in tempBakeObjects)
            {
                Assert.IsFalse(go.name.StartsWith("Temp_Bake_"), $"Temporary bake object '{go.name}' was leaked in the scene!");
            }
        }

        [Test]
        public void ExportClips_Batch_ExportsAllClips()
        {
            var clips = new[] { _testClip };
            var options = new HumanoidAnimToFbxExporter.ExportOptions
            {
                outputPath = TestOutputFolder,
                includeSkinnedMesh = false,
                exportBinary = true
            };

            string[] results = HumanoidAnimToFbxExporter.ExportClips(clips, _testCharacter, options);

            Assert.AreEqual(1, results.Length);
            Assert.IsTrue(File.Exists(results[0]));
        }
    }
}
