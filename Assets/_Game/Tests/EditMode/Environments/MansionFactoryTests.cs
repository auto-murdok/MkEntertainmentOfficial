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
        public void WrapperBounds_MatchBlenderGroundTruth()
        {
            // Pins the import-frame correction (2026-10-25 gap hunt):
            // Unity's FBX importer composes UE geometry into wrapper
            // space X-flipped vs the actor frame; the factory bakes
            // the inverse flip into every wrapper. Expected values
            // are the Blender reconstruction's measured mesh bounds
            // mapped to the Unity actor frame (x = Blender x,
            // y = Blender z-up, z = -Blender y-depth) — the mapping
            // the scene placements were numerically verified against,
            // docs/mansion-rebuild.md in the playground repo.
            var expected = new Dictionary<string, (Vector3 center, Vector3 size, float tol)>
            {
                { "SM_ExternalWall_SecondFloor_wall01",
                    (new Vector3(1.00f, 2.75f, -0.23f), new Vector3(2.00f, 5.50f, 0.46f), 0.06f) },
                { "SM_ExternalWall_SecondFloor_WindowFrame01",
                    (new Vector3(2.00f, 2.75f, -0.23f), new Vector3(4.00f, 5.50f, 0.46f), 0.06f) },
                { "SM_Roof01",
                    (new Vector3(2.00f, 2.40f, -7.25f), new Vector3(4.01f, 6.67f, 15.51f), 0.35f) },
                { "SM_Window01",
                    (new Vector3(0.00f, 1.92f, 0.00f), new Vector3(2.50f, 3.83f, 1.18f), 0.35f) },
            };
            MansionPartCatalog catalog = LoadCatalog();
            foreach ((string part, (Vector3 center, Vector3 size, float tol)) in expected)
            {
                MansionPartCatalog.Entry entry = catalog.entries.FirstOrDefault(e => e.partName == part);
                Assert.IsNotNull(entry, $"Catalog entry missing: {part}");
                Assert.Less(Vector3.Distance(entry.boundsCenter, center), tol,
                    $"{part}: wrapper bounds centre {entry.boundsCenter} != ground truth {center} " +
                    "(import-frame correction missing or wrong).");
                Assert.Less(Vector3.Distance(entry.boundsSize, size), tol,
                    $"{part}: wrapper bounds size {entry.boundsSize} != ground truth {size}.");
            }
        }

        [Test]
        public void MirroredWrappers_GeometryIsTheBaseMirroredInWrapperSpace()
        {
            // Geometric pin for the v1 mirror bug (2026-10-25): the
            // mirror was baked in mesh-local space, so pieces whose
            // FBX node transforms rotate the axes came out flipped
            // across their depth — shells swapped, seams open. The
            // _M wrapper's aggregate bounds must equal the base
            // wrapper's bounds mirrored by the manifest sign pattern.
            Manifest manifest = LoadManifest();
            var signsByMesh = new Dictionary<string, Vector3>();
            foreach (Actor actor in manifest.actors)
            {
                if (actor.kind == "staticmesh" && !string.IsNullOrEmpty(actor.mesh)
                    && actor.scale != null && actor.scale.Length == 3
                    && actor.scale[0] * actor.scale[1] * actor.scale[2] < 0f)
                {
                    // Unity axis order: (UE sx, UE sz, UE sy).
                    signsByMesh[actor.mesh] = new Vector3(
                        Mathf.Sign(actor.scale[0]), Mathf.Sign(actor.scale[2]), Mathf.Sign(actor.scale[1]));
                }
            }

            MansionPartCatalog catalog = LoadCatalog();
            foreach (MansionPartCatalog.Entry entry in catalog.entries.Where(e => e.mirroredVariant))
            {
                Bounds baseBounds = AggregateBounds($"{PrefabsDir}/{entry.partName}.prefab");
                Bounds mirroredBounds = AggregateBounds($"{PrefabsDir}/{entry.partName}_M.prefab");
                Vector3 signs = signsByMesh[entry.partName];
                var expectedCenter = new Vector3(
                    baseBounds.center.x * signs.x,
                    baseBounds.center.y * signs.y,
                    baseBounds.center.z * signs.z);
                Assert.Less(Vector3.Distance(mirroredBounds.size, baseBounds.size), 0.05f,
                    $"{entry.partName}_M: bounds size drifted from the base wrapper.");
                Assert.Less(Vector3.Distance(mirroredBounds.center, expectedCenter), 0.05f,
                    $"{entry.partName}_M: bounds centre is not the base centre mirrored by {signs} " +
                    $"(got {mirroredBounds.center}, expected {expectedCenter}).");
            }
        }

        private static Bounds AggregateBounds(string prefabPath)
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                contents.transform.position = Vector3.zero;
                contents.transform.rotation = Quaternion.identity;
                Bounds bounds = default;
                bool first = true;
                foreach (Renderer renderer in contents.GetComponentsInChildren<Renderer>(true))
                {
                    if (first)
                    {
                        bounds = renderer.bounds;
                        first = false;
                    }
                    else
                    {
                        bounds.Encapsulate(renderer.bounds);
                    }
                }
                return bounds;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
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
