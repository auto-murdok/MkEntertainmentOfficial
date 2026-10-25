using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Tests.EditMode
{
    // Verifies the scene builder's output: chunk scenes exist with the
    // pinned placement populations, and the Core scene carries a
    // loader wired to exactly those chunks. Populations derive from
    // the manifest + catalog classes via the builder's chunk rule
    // (Shell 518 / Interior 1465 / Grounds 632 at the pinned manifest).
    public class MansionSceneBuilderTests
    {
        private const string ScenesDir = "Assets/_Game/Environments/Mansion/Scenes";

        private static readonly Dictionary<string, int> ExpectedChunks = new Dictionary<string, int>
        {
            { "Mansion_Shell", 518 },
            { "Mansion_Interior", 1465 },
            { "Mansion_Grounds", 632 },
        };

        [Test]
        public void ChunkScenes_ExistWithPinnedPopulations()
        {
            foreach ((string chunk, int expected) in ExpectedChunks)
            {
                string path = $"{ScenesDir}/{chunk}.unity";
                Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<SceneAsset>(path),
                    $"Chunk scene missing: {path}");

                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    GameObject generated = scene.GetRootGameObjects()
                        .FirstOrDefault(go => go.name == "Generated");
                    Assert.IsNotNull(generated, $"{chunk}: 'Generated' root missing.");
                    Assert.AreEqual(expected, generated.transform.childCount,
                        $"{chunk}: placed instance count drifted from the manifest.");
                }
                finally
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        [Test]
        public void CoreScene_LoaderWiredToChunkScenes()
        {
            string path = $"{ScenesDir}/Mansion_Core.unity";
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var loader = scene.GetRootGameObjects()
                    .SelectMany(go => go.GetComponentsInChildren<Game.Core.Mansion.MansionSceneLoader>(true))
                    .FirstOrDefault();
                Assert.IsNotNull(loader, "Mansion_Core has no MansionSceneLoader.");
                CollectionAssert.AreEquivalent(
                    ExpectedChunks.Keys.ToArray(), loader.ChunkScenes.ToArray(),
                    "Loader chunk list must equal the generated chunk scenes.");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void MansionScenes_AreRegisteredInBuildSettings()
        {
            var registered = EditorBuildSettings.scenes.Select(s => s.path).ToHashSet();
            foreach (string chunk in ExpectedChunks.Keys.Concat(new[] { "Mansion_Core" }))
            {
                Assert.IsTrue(registered.Contains($"{ScenesDir}/{chunk}.unity"),
                    $"{chunk} is not in Build Settings — the loader cannot load it in a build.");
            }
        }
    }
}
