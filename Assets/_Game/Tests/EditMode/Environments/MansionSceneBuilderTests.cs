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

        // The 5 manifest 'Plane' actors (pond sheet + fog cards) are
        // recreated natively. UE's Plane is 1x1 m, Unity's primitive
        // is 10x10 m — the builder divides the manifest scale by 10.
        // Pinned world AABBs are the Blender ground truth (exact
        // vertices, converted to the Unity frame), tolerance 0.15 m.
        private static readonly Dictionary<string, float[]> PlaneFootprints =
            new Dictionary<string, float[]>
            {
                { "Watter", new[] { 60.349f, 0.152f, -29.761f, 96.059f, 0.152f, 10.558f } },
                { "BP_FXFogCard2", new[] { -11.599f, 0.489f, 0.405f, -6.823f, 0.489f, 2.889f } },
                { "BP_FXFogCard3", new[] { -1.638f, 34.888f, -17.970f, 3.138f, 34.888f, -15.486f } },
                { "BP_FXFogCard4", new[] { -18.591f, 2.771f, -0.194f, -13.815f, 2.771f, 2.290f } },
                { "BP_FXFogCard5", new[] { 20.538f, 2.771f, 2.468f, 25.314f, 2.771f, 4.952f } },
            };

        [Test]
        public void PlaneActors_MatchGroundTruthFootprints()
        {
            string path = $"{ScenesDir}/Mansion_Grounds.unity";
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                GameObject generated = scene.GetRootGameObjects()
                    .FirstOrDefault(go => go.name == "Generated");
                Assert.IsNotNull(generated, "Mansion_Grounds: 'Generated' root missing.");
                foreach ((string label, float[] expected) in PlaneFootprints)
                {
                    Transform instance = generated.transform.Cast<Transform>()
                        .FirstOrDefault(t => t.name == label);
                    Assert.IsNotNull(instance, $"Plane actor missing from Grounds: {label}");
                    var filter = instance.GetComponent<MeshFilter>();
                    Assert.IsNotNull(filter, $"{label}: no MeshFilter.");
                    Bounds world = WorldBounds(filter);
                    var min = new Vector3(expected[0], expected[1], expected[2]);
                    var max = new Vector3(expected[3], expected[4], expected[5]);
                    Assert.LessOrEqual(Vector3.Distance(world.min, min), 0.15f,
                        $"{label}: min {world.min} vs ground truth {min}");
                    Assert.LessOrEqual(Vector3.Distance(world.max, max), 0.15f,
                        $"{label}: max {world.max} vs ground truth {max}");
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static Bounds WorldBounds(MeshFilter filter)
        {
            Matrix4x4 matrix = Matrix4x4.identity;
            Transform current = filter.transform;
            while (current != null)
            {
                matrix = Matrix4x4.TRS(
                    current.localPosition, current.localRotation, current.localScale) * matrix;
                current = current.parent;
            }
            Bounds bounds = default;
            bool found = false;
            foreach (Vector3 vertex in filter.sharedMesh.vertices)
            {
                Vector3 world = matrix.MultiplyPoint3x4(vertex);
                if (!found)
                {
                    bounds = new Bounds(world, Vector3.zero);
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(world);
                }
            }
            return bounds;
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
