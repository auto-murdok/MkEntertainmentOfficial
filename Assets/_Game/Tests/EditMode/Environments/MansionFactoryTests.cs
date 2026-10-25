using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.EditMode
{
    // Verifies the prefab factory's output contract: one wrapper prefab
    // per source mesh, mirrored variants exactly for the manifest's
    // negative-determinant meshes, and consolidated materials only.
    // Regenerate via Tools > Mansion > Generate Prefabs (Factory).
    public class MansionFactoryTests
    {
        private const string MansionDir = "Assets/_Game/Environments/Mansion";
        private const string PrefabsDir = MansionDir + "/Prefabs";
        private const string MaterialsDir = MansionDir + "/Materials";

        [Serializable]
        private class Manifest
        {
            public Actor[] actors;
        }

        [Serializable]
        private class Actor
        {
            public string kind;
            public string mesh;
            public float[] scale;
        }

        private static Manifest LoadManifest()
        {
            string path = Path.Combine(Application.dataPath,
                "_Game/Environments/Mansion/Source/level_actors.json");
            return JsonUtility.FromJson<Manifest>(File.ReadAllText(path));
        }

        private static MansionPartCatalog LoadCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<MansionPartCatalog>(
                MansionDir + "/MansionPartCatalog.asset");
            Assert.IsNotNull(catalog, "Part catalog missing — run the prefab factory.");
            return catalog;
        }

        [Test]
        public void Catalog_CoversEverySourceFbx()
        {
            MansionPartCatalog catalog = LoadCatalog();
            Assert.AreEqual(211, catalog.entries.Count,
                "Catalog must hold one entry per source FBX (50 kit + 161 props).");
            Assert.IsTrue(catalog.entries.All(e => e.triangleCount > 0),
                "Every part must report a positive triangle count.");
        }

        [Test]
        public void WrapperPrefabs_ExistForEveryManifestMesh()
        {
            Manifest manifest = LoadManifest();
            var meshes = new HashSet<string>(manifest.actors
                .Where(a => a.kind == "staticmesh" && !string.IsNullOrEmpty(a.mesh) && a.mesh != "Plane")
                .Select(a => a.mesh));

            var missing = meshes
                .Where(m => AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabsDir}/{m}.prefab") == null)
                .ToList();
            Assert.IsEmpty(missing, "Wrapper prefabs missing for: " + string.Join(", ", missing.Take(10)));
        }

        [Test]
        public void MirroredVariants_MatchManifestNegativeDeterminantSet()
        {
            Manifest manifest = LoadManifest();
            var mirrored = new HashSet<string>(manifest.actors
                .Where(a => a.kind == "staticmesh" && !string.IsNullOrEmpty(a.mesh)
                            && a.scale != null && a.scale.Length == 3
                            && a.scale[0] * a.scale[1] * a.scale[2] < 0f)
                .Select(a => a.mesh));

            MansionPartCatalog catalog = LoadCatalog();
            var catalogMirrored = new HashSet<string>(catalog.entries
                .Where(e => e.mirroredVariant).Select(e => e.partName));
            // Set equality (AreEqual on HashSet compares iteration
            // order, not membership — wrong tool for this contract).
            CollectionAssert.AreEquivalent(mirrored, catalogMirrored,
                "Catalog mirrored set must equal the manifest's negative-determinant set.");

            foreach (string mesh in mirrored)
            {
                Assert.IsNotNull(
                    AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabsDir}/{mesh}_M.prefab"),
                    $"Mirrored wrapper missing: {mesh}_M");
            }
        }

        [Test]
        public void WrapperPrefabs_NeverUseTheUnmappedPlaceholder()
        {
            // Regression pin for the first visual tour (2026-10-25):
            // UCX collision hulls and unresolved slots put 139 wrappers
            // on the magenta placeholder. Zero tolerance now.
            MansionPartCatalog catalog = LoadCatalog();
            var offenders = new List<string>();
            foreach (MansionPartCatalog.Entry entry in catalog.entries)
            {
                foreach (string suffix in entry.mirroredVariant ? new[] { "", "_M" } : new[] { "" })
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                        $"{PrefabsDir}/{entry.partName}{suffix}.prefab");
                    if (prefab == null)
                    {
                        continue;
                    }
                    if (prefab.GetComponentsInChildren<Renderer>(true)
                        .SelectMany(r => r.sharedMaterials)
                        .Any(m => m != null && m.name == "UNMAPPED"))
                    {
                        offenders.Add(entry.partName + suffix);
                    }
                }
            }
            Assert.IsEmpty(offenders,
                "Wrappers rendering on the UNMAPPED placeholder: " + string.Join(", ", offenders.Take(10)));
        }

        [Test]
        public void ConsolidatedMaterials_AllHaveBaseMaps()
        {
            // Glass is textureless by design; the neutral default
            // surface and the placeholder are colour-only too.
            string[] guids = AssetDatabase.FindAssets("t:Material", new[] { MaterialsDir });
            var missing = new List<string>();
            foreach (string guid in guids)
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (material == null
                    || material.name == "UNMAPPED"
                    || material.name == "M_DefaultSurface"
                    || material.name.IndexOf("Glass", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    continue;
                }
                if (material.GetTexture("_BaseMap") == null)
                {
                    missing.Add(material.name);
                }
            }
            Assert.IsEmpty(missing,
                "Consolidated materials with no base map: " + string.Join(", ", missing.Take(10)));
        }

        [Test]
        public void WrapperPrefabs_UseOnlyConsolidatedMaterials()
        {
            MansionPartCatalog catalog = LoadCatalog();
            foreach (MansionPartCatalog.Entry entry in catalog.entries.Take(211))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    $"{PrefabsDir}/{entry.partName}.prefab");
                Assert.IsNotNull(prefab, $"Wrapper missing: {entry.partName}");
                foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>(true))
                {
                    foreach (Material material in renderer.sharedMaterials)
                    {
                        if (material == null)
                        {
                            continue;
                        }
                        string path = AssetDatabase.GetAssetPath(material);
                        Assert.IsTrue(path.StartsWith(MaterialsDir),
                            $"{entry.partName}: material {material.name} is not consolidated (at {path}).");
                    }
                }
            }
        }
    }
}
