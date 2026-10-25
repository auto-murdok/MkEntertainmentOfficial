using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Generates the Mansion wrapper prefabs from the source FBX (the
// "prefab factory" of the stackable-prefabs plan). Idempotent: re-running
// regenerates Materials/, Prefabs/, GeneratedMeshes/ and the part
// catalog from Source/ + the placement manifest, overwriting its own
// previous output. Never hand-edit the generated prefabs — change the
// rules here and regenerate.
//
// Per unique source mesh, one wrapper prefab:
//   <Part>                  root at the FBX origin == the manifest's
//     └─ PivotOffset        actor transform (reproduction contract).
//          └─ <model>       Reserved node carrying the imported model
//     └─ Collider           with consolidated materials.
//   Proxy collider fitted to the measured bounds (Box by policy,
//   Mesh for the explicit concave-fixture exceptions), LODGroup
//   preserved from FbxLODGroup where authored, static flags set for
//   lightmap/occlusion/navigation baking (NOT BatchingStatic — static
//   batching is disabled project-wide by design).
//
// Mirrored placements (negative-determinant manifest scales) get real
// mirrored mesh assets + <Part>_M wrapper prefabs, so no shipped
// transform ever carries a negative scale.
public static class MansionPrefabFactory
{
    private const string MansionRoot = "Assets/_Game/Environments/Mansion";
    private const string SourceDir = MansionRoot + "/Source";
    private const string MaterialsDir = MansionRoot + "/Materials";
    private const string PrefabsDir = MansionRoot + "/Prefabs";
    private const string GeneratedMeshesDir = MansionRoot + "/GeneratedMeshes";
    private const string AuditDir = MansionRoot + "/Audit";
    private const string CatalogPath = MansionRoot + "/MansionPartCatalog.asset";

    // Concave fixtures where a box proxy would seal the usable volume.
    // These get MeshColliders — and therefore Read/Write-enabled source
    // meshes (Unity 6.6+ fails builds otherwise; explicit by design).
    private static readonly string[] MeshColliderExceptions = { "SM_Bathtub", "SM_Sink" };

    private static readonly string[] FoliageTokens =
        { "Tree", "Beech", "Bush", "Shrub", "Plant", "Grass", "Ivy", "Vine", "Hedge", "Fern", "Leaf" };

    private static readonly string[] SmallDecorTokens =
        { "Book", "Bottle", "Cup", "Plate", "Paper", "Candle", "Fork", "Spoon", "Apple", "Bread", "Jar" };

    private class MaterialDef
    {
        public string albedoPath;
        public string normalPath;
        public string maskPath;
        public bool twoSided;
    }

    [MenuItem("Tools/Mansion/Generate Prefabs (Factory)")]
    public static void GenerateAll()
    {
        Debug.Log("[MansionFactory] Starting generation...");
        // Prefab instantiation needs an active scene in batchmode.
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EnsureFolders();

        ManifestData manifest = LoadManifest();
        List<FbxSource> sources = CollectSources(manifest);

        Dictionary<string, MaterialDef> materialDefs = BuildMaterialDefinitions(sources);
        Dictionary<string, Material> materials = ConsolidateMaterials(materialDefs);

        // Mesh reads (vertices/indices for bounds is fine without it, but
        // triangles + mirroring need CPU access): flip Read/Write on for
        // the whole pass, restore at the end except collider exceptions.
        SetReadWrite(sources, true);
        try
        {
            var catalog = ScriptableObject.CreateInstance<MansionPartCatalog>();
            var unmappedMaterials = new SortedSet<string>();
            int strippedTotal = 0;

            foreach (FbxSource source in sources)
            {
                GameObject wrapper = BuildWrapper(source, materials, unmappedMaterials, out WrapperInfo info);
                strippedTotal += info.StrippedCollisionMeshes;
                PrefabUtility.SaveAsPrefabAsset(wrapper, $"{PrefabsDir}/{source.Name}.prefab");
                UnityEngine.Object.DestroyImmediate(wrapper);

                MansionPartCatalog.Entry entry = info.ToEntry(source);
                catalog.entries.Add(entry);

                if (source.NeedsMirror)
                {
                    BuildMirroredWrapper(source, manifest.MirrorSignByMesh[source.Name]);
                    entry.mirroredVariant = true;
                }
            }

            WriteCatalogAsset(catalog);
            WriteAudit(catalog, unmappedMaterials, manifest, materials, strippedTotal);
            Debug.Log($"[MansionFactory] Done: {sources.Count} wrappers, " +
                      $"{catalog.entries.Count(e => e.mirroredVariant)} mirrored variants, " +
                      $"{materials.Count} materials, {unmappedMaterials.Count} unmapped material names, " +
                      $"{strippedTotal} collision meshes stripped.");
        }
        finally
        {
            SetReadWrite(sources, false, MeshColliderExceptions);
            AssetDatabase.Refresh();
        }
    }

    // ---------- sources & manifest ----------

    private class FbxSource
    {
        public string Name;
        public string Path;
        public bool IsKit;
        public bool NeedsMirror;
        public int Placements;
    }

    private class ManifestData
    {
        public readonly Dictionary<string, int> Placements = new Dictionary<string, int>();
        public readonly HashSet<string> MirroredMeshes = new HashSet<string>();

        // Per-mesh mirror sign pattern in UNITY axes (x: UE sx,
        // y: UE sz, z: UE sy). Verified uniform per mesh across all
        // its negative-determinant placements (140x (-,+,+),
        // 13x (-,-,-) — all SM_Ivy_Wall_C); LoadManifest errors if a
        // mesh ever shows mixed patterns.
        public readonly Dictionary<string, Vector3> MirrorSignByMesh = new Dictionary<string, Vector3>();
        public int MeshlessStaticActors;
    }

    [Serializable]
    private class ManifestJson
    {
        public ManifestActor[] actors;
    }

    [Serializable]
    private class ManifestActor
    {
        public string kind;
        public string mesh;
        public float[] scale;
    }

    private static ManifestData LoadManifest()
    {
        var data = new ManifestData();
        string json = File.ReadAllText(Path.GetFullPath(SourceDir + "/level_actors.json"));
        ManifestJson manifest = JsonUtility.FromJson<ManifestJson>(json);
        foreach (ManifestActor actor in manifest.actors)
        {
            if (actor.kind != "staticmesh")
            {
                continue;
            }
            if (string.IsNullOrEmpty(actor.mesh))
            {
                data.MeshlessStaticActors++;
                continue;
            }
            data.Placements.TryGetValue(actor.mesh, out int count);
            data.Placements[actor.mesh] = count + 1;
            if (actor.scale != null && actor.scale.Length == 3 &&
                actor.scale[0] * actor.scale[1] * actor.scale[2] < 0f)
            {
                data.MirroredMeshes.Add(actor.mesh);
                var signs = new Vector3(
                    Mathf.Sign(actor.scale[0]), Mathf.Sign(actor.scale[2]), Mathf.Sign(actor.scale[1]));
                if (data.MirrorSignByMesh.TryGetValue(actor.mesh, out Vector3 existing) && existing != signs)
                {
                    Debug.LogError($"[MansionFactory] Mesh {actor.mesh} has MIXED mirror sign patterns " +
                                   $"({existing} vs {signs}) — one _M variant cannot serve both.");
                }
                data.MirrorSignByMesh[actor.mesh] = signs;
            }
        }
        return data;
    }

    private static List<FbxSource> CollectSources(ManifestData manifest)
    {
        var sources = new List<FbxSource>();
        foreach ((string folder, bool isKit) in new[] { ("Kit", true), ("Props", false) })
        {
            string dir = Path.Combine(Application.dataPath, "_Game/Environments/Mansion/Source", folder);
            foreach (string file in Directory.GetFiles(dir, "*.fbx").OrderBy(f => f))
            {
                string name = Path.GetFileNameWithoutExtension(file);
                sources.Add(new FbxSource
                {
                    Name = name,
                    Path = $"{SourceDir}/{folder}/{name}.fbx",
                    IsKit = isKit,
                    NeedsMirror = manifest.MirroredMeshes.Contains(name),
                    Placements = manifest.Placements.TryGetValue(name, out int c) ? c : 0,
                });
            }
        }
        return sources;
    }

    private static void SetReadWrite(List<FbxSource> sources, bool readable, string[] keepReadable = null)
    {
        var keep = new HashSet<string>(keepReadable ?? Array.Empty<string>());
        bool dirty = false;
        foreach (FbxSource source in sources)
        {
            bool target = readable || keep.Contains(source.Name);
            var importer = (ModelImporter)AssetImporter.GetAtPath(source.Path);
            if (importer != null && importer.isReadable != target)
            {
                importer.isReadable = target;
                importer.SaveAndReimport();
                dirty = true;
            }
        }
        if (dirty)
        {
            AssetDatabase.Refresh();
        }
    }

    // ---------- materials ----------

    private static Dictionary<string, MaterialDef> BuildMaterialDefinitions(List<FbxSource> sources)
    {
        var defs = new Dictionary<string, MaterialDef>();

        // Kit: matlib maps MI name -> texture paths (old project layout;
        // only the basenames matter, resolved against the new folders).
        string matlib = File.ReadAllText(Path.GetFullPath(SourceDir + "/matlib_building_kit.json"));
        var entryRegex = new Regex(
            "\"(?<name>MI_[^\"]+)\":\\s*\\{\\s*\"albedo\":\\s*\"(?<albedo>[^\"]*)\",\\s*\"normal\":\\s*\"(?<normal>[^\"]*)\",\\s*\"orm\":\\s*\"(?<orm>[^\"]*)\"");
        foreach (Match match in entryRegex.Matches(matlib))
        {
            defs[match.Groups["name"].Value] = new MaterialDef
            {
                albedoPath = ResolveTextureAny(match.Groups["albedo"].Value, RawTextureFolders),
                normalPath = ResolveTextureAny(match.Groups["normal"].Value,
                    UnityTextureFolders.Concat(RawTextureFolders).ToArray()),
                maskPath = ResolveTextureAny(match.Groups["orm"].Value,
                    UnityTextureFolders.Concat(RawTextureFolders).ToArray()),
            };
        }

        // Props: the pack manifests map mesh+slot -> MI material with one
        // row per texture param (BaseColor/Albedo, Normal/NRM, ORM).
        foreach (string csvName in new[] { "MansionProps_manifest.csv", "MansionProps2_manifest.csv" })
        {
            string csvPath = Path.GetFullPath($"{SourceDir}/{csvName}");
            if (!File.Exists(csvPath))
            {
                continue;
            }
            foreach (string line in File.ReadAllLines(csvPath).Skip(1))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }
                string[] cols = line.Split(',');
                if (cols.Length < 7)
                {
                    continue;
                }
                string materialName = cols[2].Trim();
                // Param names arrive in two dialects: 'BaseColor' and
                // the numbered '00_BaseColor' (the dialect of exactly
                // the materials that failed to resolve in pass 1/2).
                // Normalise by stripping the numeric prefix.
                string param = Regex.Replace(cols[3].Trim(), @"^\d+_", string.Empty);
                string texture = cols[4].Trim();
                if (string.IsNullOrEmpty(materialName) || string.IsNullOrEmpty(texture))
                {
                    continue;
                }
                if (!defs.TryGetValue(materialName, out MaterialDef def))
                {
                    def = new MaterialDef();
                    defs[materialName] = def;
                }
                if (cols[6].Trim() == "1")
                {
                    def.twoSided = true;
                }
                // First non-empty wins per slot: the CSVs repeat a
                // material once per mesh that uses it (and layered
                // materials list a generic T_Base_* after their
                // specific map) — the specific map is listed first.
                if (param == "BaseColor" || param == "Albedo")
                {
                    def.albedoPath ??= ResolveTextureAny(texture + ".png", RawTextureFolders);
                }
                else if (param == "Normal" || param == "NRM")
                {
                    def.normalPath ??=
                        ResolveTextureAny($"{texture}_N_Unity.png", UnityTextureFolders)
                        ?? ResolveTextureAny(texture + ".png", RawTextureFolders);
                }
                else if (param == "ORM")
                {
                    def.maskPath ??= ResolveTextureAny($"{texture}_ORM_Pack.png", UnityTextureFolders);
                }
            }
        }

        // Stem fallback: any material name actually used by an FBX slot
        // but missing from matlib/CSVs gets a def from same-stem textures
        // (MI_Stove -> T_Stove_B/_N/_ORM). Names are discovered from the
        // FBX sub-assets, so this stays data-driven like the rest.
        var slotNames = new HashSet<string>();
        foreach (FbxSource source in sources)
        {
            foreach (Material slot in AssetDatabase.LoadAllAssetsAtPath(source.Path).OfType<Material>())
            {
                slotNames.Add(CanonicalMaterialName(slot.name));
            }
        }
        foreach (string name in slotNames.OrderBy(n => n))
        {
            if (defs.ContainsKey(name) || (!name.StartsWith("MI_") && !name.StartsWith("M_")))
            {
                continue;
            }
            string stem = name.StartsWith("MI_") ? name.Substring(3) : name.Substring(2);
            string albedo = ResolveTextureAny($"T_{stem}_B.png", RawTextureFolders)
                            ?? ResolveTextureAny($"T_{stem}_BaseColor.png", RawTextureFolders);
            if (albedo == null)
            {
                // Textureless glass (e.g. MI_Glass_Window_Roof) still gets
                // a def so the glass surface rule in ConsolidateMaterials
                // can build it — a plain tinted transparent material.
                if (name.IndexOf("Glass", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    defs[name] = new MaterialDef();
                }
                continue;
            }
            string normal = ResolveTextureAny($"T_{stem}_N_N_Unity.png", UnityTextureFolders)
                            ?? ResolveTextureAny($"T_{stem}_N.png", RawTextureFolders);
            string mask = ResolveTextureAny($"T_{stem}_ORM_Pack.png", UnityTextureFolders);
            defs[name] = new MaterialDef { albedoPath = albedo, normalPath = normal, maskPath = mask };
            Debug.Log($"[MansionFactory] Stem-fallback material def: {name} (stem {stem})");
        }
        return defs;
    }

    private static string ResolveTexture(string folder, string pathOrName)
    {
        string fileName = Path.GetFileName(pathOrName);
        string candidate = $"{folder}/{fileName}";
        return File.Exists(Path.GetFullPath(candidate)) ? candidate : null;
    }

    // Texture lookup searches across the packs: several kit pieces'
    // textures physically live in the props pack (and vice versa) —
    // resolving only within one folder left 22 materials with no base
    // map in the first generation pass.
    private static string ResolveTextureAny(string pathOrName, params string[] folders)
    {
        foreach (string folder in folders)
        {
            string resolved = ResolveTexture(folder, pathOrName);
            if (resolved != null)
            {
                return resolved;
            }
        }
        return null;
    }

    private static readonly string[] RawTextureFolders =
        { SourceDir + "/KitTextures", SourceDir + "/PropTextures" };

    private static readonly string[] UnityTextureFolders =
        { SourceDir + "/KitTexturesUnity", SourceDir + "/PropTexturesUnity" };

    private static Dictionary<string, Material> ConsolidateMaterials(Dictionary<string, MaterialDef> defs)
    {
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        var materials = new Dictionary<string, Material>();
        foreach ((string name, MaterialDef def) in defs.OrderBy(kv => kv.Key))
        {
            string path = $"{MaterialsDir}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(lit) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = lit;
            SetMap(material, "_BaseMap", def.albedoPath);
            material.SetColor("_BaseColor", Color.white);
            SetMap(material, "_BumpMap", def.normalPath);
            if (def.normalPath != null)
            {
                material.EnableKeyword("_NORMALMAP");
                material.SetFloat("_BumpScale", 1f);
            }
            // The repacked mask texture serves both readers: R=metallic,
            // G=occlusion, A=smoothness (Unity mask-map layout).
            SetMap(material, "_MetallicGlossMap", def.maskPath);
            SetMap(material, "_OcclusionMap", def.maskPath);
            if (def.maskPath != null)
            {
                material.EnableKeyword("_METALLICSPECGLOSSMAP");
                material.EnableKeyword("_OCCLUSIONMAP");
                material.SetFloat("_Metallic", 1f);
                material.SetFloat("_Smoothness", 1f);
                material.SetFloat("_OcclusionStrength", 1f);
            }
            if (def.twoSided)
            {
                material.SetFloat("_Cull", 0f);
                material.doubleSidedGI = true;
            }
            if (name.IndexOf("Glass", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                // Glass is a surface type, not a texture set: transparent
                // URP Lit, no metallic/occlusion response, tinted alpha.
                // Covers the def'd glass MIs and MI_Glass_Window_Roof,
                // which has no textures anywhere.
                material.SetTexture("_MetallicGlossMap", null);
                material.SetTexture("_OcclusionMap", null);
                material.DisableKeyword("_METALLICSPECGLOSSMAP");
                material.DisableKeyword("_OCCLUSIONMAP");
                material.SetFloat("_Metallic", 0f);
                material.SetFloat("_Smoothness", 0.9f);
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_Blend", 0f);
                material.SetFloat("_ZWrite", 0f);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.SetColor("_BaseColor",
                    def.albedoPath != null
                        ? new Color(1f, 1f, 1f, 0.35f)
                        : new Color(0.72f, 0.83f, 0.9f, 0.3f));
                material.renderQueue = 3000;
            }
            if (IsFoliageMaterial(name))
            {
                // Leaf/ivy atlases are cutout cards: without alpha
                // clipping the whole quad renders (the white trees of
                // the first tour). Double-sided — cards have no back.
                material.SetFloat("_AlphaClip", 1f);
                material.SetFloat("_Cutoff", 0.4f);
                material.EnableKeyword("_ALPHATEST_ON");
                material.SetFloat("_Cull", 0f);
                material.doubleSidedGI = true;
            }
            EditorUtility.SetDirty(material);
            materials[name] = material;
        }

        // Submeshes the UE export left material-less ('Fbx Default
        // Material' — e.g. the chandelier's 54-tri second submesh)
        // get a real neutral surface instead of the magenta flag.
        const string defaultName = "M_DefaultSurface";
        string defaultPath = $"{MaterialsDir}/{defaultName}.mat";
        var defaultMaterial = AssetDatabase.LoadAssetAtPath<Material>(defaultPath);
        if (defaultMaterial == null)
        {
            defaultMaterial = new Material(lit) { name = defaultName };
            AssetDatabase.CreateAsset(defaultMaterial, defaultPath);
        }
        defaultMaterial.shader = lit;
        defaultMaterial.SetColor("_BaseColor", new Color(0.82f, 0.80f, 0.76f));
        defaultMaterial.SetFloat("_Smoothness", 0.4f);
        defaultMaterial.SetFloat("_Metallic", 0f);
        EditorUtility.SetDirty(defaultMaterial);
        materials["Fbx Default Material"] = defaultMaterial;

        AssetDatabase.SaveAssets();
        return materials;
    }

    private static readonly string[] FoliageMaterialTokens =
        { "Beech", "Leaf", "Ivy", "Fern", "Plant", "Tree", "Grass", "Bush", "Foliage", "Vine" };

    private static bool IsFoliageMaterial(string name)
    {
        return FoliageMaterialTokens.Any(t =>
            name.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private static void SetMap(Material material, string property, string texturePath)
    {
        var texture = texturePath == null
            ? null
            : AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        material.SetTexture(property, texture);
    }

    // ---------- wrapper construction ----------

    private class WrapperInfo
    {
        public Bounds Bounds;
        public int TriangleCount;
        public int LodLevels;
        public string PartClass;
        public bool HasCollider;
        public bool MeshCollider;
        public int StrippedCollisionMeshes;

        public MansionPartCatalog.Entry ToEntry(FbxSource source)
        {
            return new MansionPartCatalog.Entry
            {
                partName = source.Name,
                sourceFbx = source.Path,
                partClass = PartClass,
                boundsCenter = Bounds.center,
                boundsSize = Bounds.size,
                triangleCount = TriangleCount,
                lodLevels = LodLevels,
                hasCollider = HasCollider,
                meshCollider = MeshCollider,
                mirroredVariant = false,
                placements = source.Placements,
            };
        }
    }

    private static string Classify(FbxSource source, out bool boxCollider, out bool meshCollider, out float lodCullHeight)
    {
        boxCollider = false;
        meshCollider = false;
        lodCullHeight = 0f;
        if (source.IsKit)
        {
            boxCollider = true;
            return "KitStructural";
        }
        if (MeshColliderExceptions.Contains(source.Name))
        {
            meshCollider = true;
            return "FixtureMesh";
        }
        if (FoliageTokens.Any(t => source.Name.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0))
        {
            lodCullHeight = 0.01f;
            return "Foliage";
        }
        if (SmallDecorTokens.Any(t => source.Name.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0))
        {
            lodCullHeight = 0.015f;
            return "SmallDecor";
        }
        boxCollider = true;
        return "Fixture";
    }

    private static GameObject BuildWrapper(
        FbxSource source,
        Dictionary<string, Material> materials,
        SortedSet<string> unmappedMaterials,
        out WrapperInfo info)
    {
        info = new WrapperInfo();
        info.PartClass = Classify(source, out bool boxCollider, out bool meshCollider, out float lodCullHeight);
        info.MeshCollider = meshCollider;

        var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(source.Path);
        if (modelAsset == null)
        {
            throw new InvalidOperationException($"Model asset failed to load: {source.Path}");
        }

        var wrapper = new GameObject(source.Name);
        var pivot = new GameObject("PivotOffset");
        pivot.transform.SetParent(wrapper.transform, false);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
        instance.transform.SetParent(pivot.transform, false);
        // Unpack completely: the factory restructures the model
        // (strips hulls, swaps and bakes meshes, flattens renderers
        // under PivotOffset). Inside a nested prefab instance none
        // of that survives SaveAsPrefabAsset — the saved prefab
        // keeps the model's original node hierarchy and records
        // only property modifications, so node-composed baked
        // geometry gets its node transform applied a second time
        // (the double-transform bug behind the persistent gaps:
        // in-memory bakes verified correct, saved output was
        // L * F_x * L). Plain objects serialize literally.
        PrefabUtility.UnpackPrefabInstance(
            instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

        // UE collision hulls ride inside the FBX as UCX_/UBX_/USP_/UCP_
        // meshes wearing a dummy 'Lit' material. They are collision
        // data, not visuals — the wrapper's proxy collider replaces
        // them. Leaving them in renders smooth hulls over the real
        // geometry (found by the first visual tour, 2026-10-25: the
        // "magenta panels" on windows, banisters and bushes were UCX
        // hulls on the UNMAPPED placeholder).
        info.StrippedCollisionMeshes = StripCollisionMeshes(instance);

        // Consolidated materials, by FBX material name.
        foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
        {
            Material[] slots = renderer.sharedMaterials;
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null)
                {
                    continue;
                }
                string key = CanonicalMaterialName(slots[i].name);
                if (materials.TryGetValue(key, out Material consolidated))
                {
                    slots[i] = consolidated;
                }
                else
                {
                    unmappedMaterials.Add(key);
                    slots[i] = GetUnmappedMaterial(materials);
                }
            }
            renderer.sharedMaterials = slots;
        }

        // Import-frame correction (the gap hunt's root cause,
        // 2026-10-25): Unity's FBX importer composes the UE geometry
        // into wrapper space X-flipped relative to the actor frame
        // the manifest transforms assume — measured against the
        // Blender reconstruction's mesh data (its world is the
        // level's verified reference; AABB rule verified on 177
        // parts). Every piece therefore sat mirrored on its pivot:
        // straight runs still tiled, but corners, junctions and
        // openings misaligned into visible gaps. Bake F_x x (node
        // local matrix) into the vertices and flatten each renderer
        // under PivotOffset, landing the geometry in the true actor
        // frame. (An earlier RotY(180) attempt also flipped Z and
        // inverted wall depths / window heights — the difference is
        // pinned by the ground-truth bounds test.)
        BakeWrapperGeometry(wrapper, pivot.transform, source.Name);

        // Bounds + triangle count from the corrected geometry
        // (renderers now live under the wrapper, post-flattening).
        Bounds bounds = ComputeBounds(wrapper, out int triangles);
        info.Bounds = bounds;
        info.TriangleCount = triangles;

        // Collider proxy on its own node, fitted to the measured bounds.
        if (boxCollider || meshCollider)
        {
            var colliderNode = new GameObject("Collider");
            colliderNode.transform.SetParent(wrapper.transform, false);
            if (boxCollider)
            {
                var box = colliderNode.AddComponent<BoxCollider>();
                box.center = bounds.center;
                box.size = bounds.size;
            }
            else
            {
                var meshColliderComponent = colliderNode.AddComponent<MeshCollider>();
                meshColliderComponent.sharedMesh = LargestMesh(wrapper);
                meshColliderComponent.convex = false;
            }
            info.HasCollider = true;
        }

        // LOD: preserve an authored FbxLODGroup; otherwise a single-level
        // group whose cull height follows the part class (0 = never cull).
        LODGroup authored = instance.GetComponentInChildren<LODGroup>(true);
        if (authored != null)
        {
            info.LodLevels = authored.GetLODs().Length;
        }
        else
        {
            var lodGroup = wrapper.AddComponent<LODGroup>();
            Renderer[] renderers = wrapper.GetComponentsInChildren<Renderer>(true);
            lodGroup.SetLODs(new[] { new LOD(lodCullHeight, renderers) });
            lodGroup.RecalculateBounds();
            info.LodLevels = 1;
        }

        const StaticEditorFlags flags = StaticEditorFlags.ContributeGI
                                        | StaticEditorFlags.OccluderStatic
                                        | StaticEditorFlags.OccludeeStatic
                                        | StaticEditorFlags.NavigationStatic
                                        | StaticEditorFlags.OffMeshLinkGeneration
                                        | StaticEditorFlags.ReflectionProbeStatic;
        foreach (Transform child in wrapper.GetComponentsInChildren<Transform>(true))
        {
            GameObjectUtility.SetStaticEditorFlags(child.gameObject, flags);
        }
        return wrapper;
    }

    // Bakes the import-frame correction (see BuildWrapper) into
    // every renderer mesh of the wrapper: new geometry =
    // RotY(180) * (renderer local matrix in wrapper space) * source
    // vertices, saved as assets under GeneratedMeshes, with the
    // renderer flattened under PivotOffset at identity. Returns the
    // source->baked map (not currently consumed; kept for callers
    // that need to retarget mesh references).
    private static Dictionary<Mesh, Mesh> BakeWrapperGeometry(
        GameObject wrapper, Transform pivot, string partName)
    {
        string assetPath = $"{GeneratedMeshesDir}/{partName}.asset";
        if (File.Exists(Path.GetFullPath(assetPath)))
        {
            AssetDatabase.DeleteAsset(assetPath);
        }
        bool assetCreated = false;
        var flipX = Matrix4x4.Scale(new Vector3(-1f, 1f, 1f));
        var bakedBySource = new Dictionary<Mesh, Mesh>();

        foreach (MeshFilter filter in wrapper.GetComponentsInChildren<MeshFilter>(true).ToArray())
        {
            if (filter.sharedMesh == null)
            {
                continue;
            }
            if (!bakedBySource.TryGetValue(filter.sharedMesh, out Mesh baked))
            {
                Matrix4x4 local = LocalChainMatrix(filter.transform, wrapper.transform);
                baked = BakeTransformedMesh(filter.sharedMesh, flipX * local, filter.sharedMesh.name);
                if (!assetCreated)
                {
                    AssetDatabase.CreateAsset(baked, assetPath);
                    assetCreated = true;
                }
                else
                {
                    AssetDatabase.AddObjectToAsset(baked, assetPath);
                }
                bakedBySource[filter.sharedMesh] = baked;
            }
            filter.transform.SetParent(pivot, false);
            filter.transform.localPosition = Vector3.zero;
            filter.transform.localRotation = Quaternion.identity;
            filter.transform.localScale = Vector3.one;
            filter.sharedMesh = baked;
        }
        AssetDatabase.SaveAssets();
        return bakedBySource;
    }

    private static readonly string[] CollisionMeshPrefixes = { "UCX_", "UBX_", "USP_", "UCP_" };

    private static int StripCollisionMeshes(GameObject instance)
    {
        int stripped = 0;
        foreach (MeshFilter filter in instance.GetComponentsInChildren<MeshFilter>(true).ToArray())
        {
            string meshName = filter.sharedMesh != null ? filter.sharedMesh.name : filter.gameObject.name;
            if (CollisionMeshPrefixes.Any(p => meshName.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            {
                UnityEngine.Object.DestroyImmediate(filter.gameObject);
                stripped++;
            }
        }
        return stripped;
    }

    private static string CanonicalMaterialName(string raw)
    {
        string name = raw.Replace(" (Instance)", string.Empty).Trim();
        name = Regex.Replace(name, @" \d+$", string.Empty);
        return name;
    }

    private static Material GetUnmappedMaterial(Dictionary<string, Material> materials)
    {
        if (!materials.TryGetValue("__UNMAPPED__", out Material material))
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            material = new Material(lit) { name = "UNMAPPED" };
            material.SetColor("_BaseColor", Color.magenta);
            AssetDatabase.CreateAsset(material, $"{MaterialsDir}/UNMAPPED.mat");
            materials["__UNMAPPED__"] = material;
        }
        return material;
    }

    // Bounds from mesh data composed through serialized local TRS
    // chains — never renderer.bounds: in a batchmode factory run
    // the live world matrices lag the restructuring (reparented
    // renderers still measure under their old parents).
    private static Bounds ComputeBounds(GameObject root, out int triangleCount)
    {
        triangleCount = 0;
        Bounds bounds = default;
        bool found = false;
        foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null)
            {
                continue;
            }
            triangleCount += filter.sharedMesh.triangles.Length / 3;
            Matrix4x4 matrix = LocalChainMatrix(filter.transform, root.transform);
            Bounds meshBounds = filter.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3(
                    meshBounds.center.x + ((i & 1) == 0 ? -meshBounds.extents.x : meshBounds.extents.x),
                    meshBounds.center.y + ((i & 2) == 0 ? -meshBounds.extents.y : meshBounds.extents.y),
                    meshBounds.center.z + ((i & 4) == 0 ? -meshBounds.extents.z : meshBounds.extents.z));
                Vector3 world = matrix.MultiplyPoint3x4(corner);
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
        }
        return bounds;
    }

    private static Mesh LargestMesh(GameObject root)
    {
        Mesh best = null;
        foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh != null &&
                (best == null || filter.sharedMesh.triangles.Length > best.triangles.Length))
            {
                best = filter.sharedMesh;
            }
        }
        return best;
    }

    // ---------- mirrored variants ----------

    // The mirror MUST be baked in WRAPPER space, not mesh-local
    // space (factory v1's bug, found by the gap hunt 2026-10-25):
    // FBX node transforms between the wrapper root and each mesh
    // rotate the local axes, so negating mesh-local X flipped some
    // pieces across their DEPTH instead — exterior/interior shells
    // swapped and seams opened across the facade. Baking
    // (mirrorSigns * rendererLocalMatrix) into the vertices and
    // flattening the renderer under PivotOffset is frame-proof:
    // the composed instance transform then equals the UE original
    // exactly, for any sign pattern (single-axis or the ivy
    // (-,-,-) full negation).
    private static void BuildMirroredWrapper(FbxSource source, Vector3 mirrorSigns)
    {
        var instance = PrefabUtility.LoadPrefabContents($"{PrefabsDir}/{source.Name}.prefab");
        instance.name = source.Name + "_M";
        Transform root = instance.transform;
        root.position = Vector3.zero;
        root.rotation = Quaternion.identity;
        root.localScale = Vector3.one;
        Transform pivot = root.Find("PivotOffset");
        var flip = Matrix4x4.Scale(mirrorSigns);

        string assetPath = $"{GeneratedMeshesDir}/{source.Name}_M.asset";
        // Regenerate the baked-mesh file from scratch each run so
        // stale sub-assets from older factory versions cannot linger.
        if (File.Exists(Path.GetFullPath(assetPath)))
        {
            AssetDatabase.DeleteAsset(assetPath);
        }
        bool assetCreated = false;
        var bakedBySource = new Dictionary<Mesh, Mesh>();

        foreach (MeshFilter filter in instance.GetComponentsInChildren<MeshFilter>(true).ToArray())
        {
            if (filter.sharedMesh == null)
            {
                continue;
            }
            if (!bakedBySource.TryGetValue(filter.sharedMesh, out Mesh baked))
            {
                Matrix4x4 local = LocalChainMatrix(filter.transform, root);
                baked = BakeTransformedMesh(filter.sharedMesh, flip * local, filter.sharedMesh.name + "_M");
                if (!assetCreated)
                {
                    AssetDatabase.CreateAsset(baked, assetPath);
                    assetCreated = true;
                }
                else
                {
                    AssetDatabase.AddObjectToAsset(baked, assetPath);
                }
                bakedBySource[filter.sharedMesh] = baked;
            }
            filter.transform.SetParent(pivot, false);
            filter.transform.localPosition = Vector3.zero;
            filter.transform.localRotation = Quaternion.identity;
            filter.transform.localScale = Vector3.one;
            filter.sharedMesh = baked;
        }
        AssetDatabase.SaveAssets();

        // MeshCollider exceptions reference the LOD0 mesh — swap to
        // its baked counterpart.
        foreach (MeshCollider collider in instance.GetComponentsInChildren<MeshCollider>(true))
        {
            if (collider.sharedMesh != null && bakedBySource.TryGetValue(collider.sharedMesh, out Mesh baked))
            {
                collider.sharedMesh = baked;
            }
        }
        // Refit the box proxy from the mirrored geometry.
        BoxCollider box = instance.GetComponentInChildren<BoxCollider>(true);
        if (box != null)
        {
            Bounds bounds = ComputeBounds(instance, out _);
            box.center = bounds.center;
            box.size = bounds.size;
        }
        PrefabUtility.SaveAsPrefabAsset(instance, $"{PrefabsDir}/{source.Name}_M.prefab");
        PrefabUtility.UnloadPrefabContents(instance);
    }

    // Composes a node's local matrix relative to an ancestor from
    // the serialized local TRS fields ONLY. Never use
    // localToWorldMatrix for this in a batchmode factory run: for
    // hierarchies whose rotation sits on the asset root (SM_Roof01)
    // or mid-chain (SM_Window01), the live matrices are not yet
    // settled when the bake reads them and the composed matrix
    // silently loses the RotX(270) node — the geometry then bakes
    // y/z-swapped. Local fields are data; they cannot be stale.
    private static Matrix4x4 LocalChainMatrix(Transform node, Transform ancestor)
    {
        Matrix4x4 matrix = Matrix4x4.identity;
        Transform current = node;
        while (current != null && current != ancestor)
        {
            matrix = Matrix4x4.TRS(current.localPosition, current.localRotation, current.localScale) * matrix;
            current = current.parent;
        }
        return matrix;
    }

    private static Mesh BakeTransformedMesh(Mesh source, Matrix4x4 matrix, string name)
    {
        var baked = new Mesh
        {
            name = name,
            indexFormat = source.indexFormat,
        };
        Vector3[] vertices = source.vertices;
        for (int i = 0; i < vertices.Length; i++)
        {
            vertices[i] = matrix.MultiplyPoint3x4(vertices[i]);
        }
        baked.vertices = vertices;

        if (source.normals != null && source.normals.Length == vertices.Length)
        {
            Matrix4x4 normalMatrix = matrix.inverse.transpose;
            Vector3[] normals = source.normals;
            for (int i = 0; i < normals.Length; i++)
            {
                normals[i] = normalMatrix.MultiplyVector(normals[i]).normalized;
            }
            baked.normals = normals;
        }
        baked.uv = source.uv;
        baked.uv2 = source.uv2;
        baked.colors = source.colors;

        // Reversing each submesh's whole index list flips every
        // triangle's winding while keeping the triangles intact —
        // required when the baked matrix has a negative determinant.
        bool flipTriangles = matrix.determinant < 0f;
        baked.subMeshCount = source.subMeshCount;
        for (int sub = 0; sub < source.subMeshCount; sub++)
        {
            int[] triangles = source.GetTriangles(sub);
            if (flipTriangles)
            {
                Array.Reverse(triangles);
            }
            baked.SetTriangles(triangles, sub);
        }
        baked.RecalculateBounds();
        if (baked.normals == null || baked.normals.Length == 0)
        {
            baked.RecalculateNormals();
        }
        baked.RecalculateTangents();
        return baked;
    }

    // ---------- catalog & audit ----------

    private static void WriteCatalogAsset(MansionPartCatalog catalog)
    {
        catalog.entries.Sort((a, b) => string.CompareOrdinal(a.partName, b.partName));
        var existing = AssetDatabase.LoadAssetAtPath<MansionPartCatalog>(CatalogPath);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }
        else
        {
            existing.entries = catalog.entries;
            EditorUtility.SetDirty(existing);
        }
        AssetDatabase.SaveAssets();
    }

    private static void WriteAudit(
        MansionPartCatalog catalog, SortedSet<string> unmappedMaterials, ManifestData manifest,
        Dictionary<string, Material> materials, int strippedCollisionMeshes)
    {
        string json = JsonUtility.ToJson(catalog, true);
        File.WriteAllText(Path.GetFullPath(AuditDir + "/part-audit.json"), json);

        // Materials that still render without a base map. Glass and the
        // neutral default surface are textureless BY DESIGN; anything
        // else here means a def's texture failed to resolve anywhere.
        var missingBaseMap = materials
            .Where(kv => kv.Key != "__UNMAPPED__"
                         && kv.Value.GetTexture("_BaseMap") == null
                         && kv.Key.IndexOf("Glass", StringComparison.OrdinalIgnoreCase) < 0
                         && kv.Key != "Fbx Default Material")
            .Select(kv => kv.Key)
            .OrderBy(n => n)
            .ToList();

        var report = new StringBuilder();
        report.AppendLine("# Mansion part audit (generated by MansionPrefabFactory)");
        report.AppendLine();
        report.AppendLine($"- Parts: {catalog.entries.Count} " +
                          $"({catalog.entries.Count(e => e.partClass == "KitStructural")} kit, " +
                          $"{catalog.entries.Count(e => e.partClass != "KitStructural")} props)");
        report.AppendLine($"- Total placed triangles (all LODs, per part): {catalog.entries.Sum(e => e.triangleCount):N0}");
        report.AppendLine($"- Mirrored variants baked: {catalog.entries.Count(e => e.mirroredVariant)} " +
                          $"({manifest.MirroredMeshes.Count} meshes have negative-determinant placements)");
        report.AppendLine($"- UE collision meshes stripped (UCX_/UBX_/USP_/UCP_): {strippedCollisionMeshes}");
        report.AppendLine($"- MeshCollider exceptions (Read/Write kept ON): " +
                          $"{string.Join(", ", catalog.entries.Where(e => e.meshCollider).Select(e => e.partName))}");
        report.AppendLine($"- Manifest actors with no mesh (UE volumes/barriers, skipped by the builder): {manifest.MeshlessStaticActors}");
        report.AppendLine($"- Unmapped material names (rendered magenta, fix the defs): {unmappedMaterials.Count}");
        foreach (string name in unmappedMaterials)
        {
            report.AppendLine($"  - {name}");
        }
        report.AppendLine($"- Consolidated materials with NO base map (glass/default excluded): {missingBaseMap.Count}");
        foreach (string name in missingBaseMap)
        {
            report.AppendLine($"  - {name}");
        }
        File.WriteAllText(Path.GetFullPath(AuditDir + "/AUDIT.md"), report.ToString());
        AssetDatabase.Refresh();
    }

    private static void EnsureFolders()
    {
        foreach (string folder in new[] { MaterialsDir, PrefabsDir, GeneratedMeshesDir, AuditDir })
        {
            if (AssetDatabase.IsValidFolder(folder))
            {
                continue;
            }
            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
