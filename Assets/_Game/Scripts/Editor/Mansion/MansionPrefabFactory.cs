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
            var mirrorMeshMap = new Dictionary<Mesh, Mesh>();
            var unmappedMaterials = new SortedSet<string>();

            foreach (FbxSource source in sources)
            {
                GameObject wrapper = BuildWrapper(source, materials, unmappedMaterials, out WrapperInfo info);
                PrefabUtility.SaveAsPrefabAsset(wrapper, $"{PrefabsDir}/{source.Name}.prefab");
                UnityEngine.Object.DestroyImmediate(wrapper);

                MansionPartCatalog.Entry entry = info.ToEntry(source);
                catalog.entries.Add(entry);

                if (source.NeedsMirror)
                {
                    BuildMirroredWrapper(source, info, mirrorMeshMap);
                    entry.mirroredVariant = true;
                }
            }

            WriteCatalogAsset(catalog);
            WriteAudit(catalog, unmappedMaterials, manifest);
            Debug.Log($"[MansionFactory] Done: {sources.Count} wrappers, " +
                      $"{catalog.entries.Count(e => e.mirroredVariant)} mirrored variants, " +
                      $"{materials.Count} materials, {unmappedMaterials.Count} unmapped material names.");
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
                albedoPath = ResolveTexture(SourceDir + "/KitTextures", match.Groups["albedo"].Value),
                normalPath = ResolveTexture(SourceDir + "/KitTexturesUnity", match.Groups["normal"].Value),
                maskPath = ResolveTexture(SourceDir + "/KitTexturesUnity", match.Groups["orm"].Value),
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
                string param = cols[3].Trim();
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
                if (param == "BaseColor" || param == "Albedo")
                {
                    def.albedoPath = ResolveTexture(SourceDir + "/PropTextures", texture + ".png");
                }
                else if (param == "Normal" || param == "NRM")
                {
                    string unityVariant = $"{SourceDir}/PropTexturesUnity/{texture}_N_Unity.png";
                    def.normalPath = File.Exists(Path.GetFullPath(unityVariant))
                        ? unityVariant
                        : ResolveTexture(SourceDir + "/PropTextures", texture + ".png");
                }
                else if (param == "ORM")
                {
                    string pack = $"{SourceDir}/PropTexturesUnity/{texture}_ORM_Pack.png";
                    if (File.Exists(Path.GetFullPath(pack)))
                    {
                        def.maskPath = pack;
                    }
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
        string[] textureFolders = { SourceDir + "/PropTextures", SourceDir + "/KitTextures" };
        foreach (string name in slotNames.OrderBy(n => n))
        {
            if (defs.ContainsKey(name) || (!name.StartsWith("MI_") && !name.StartsWith("M_")))
            {
                continue;
            }
            string stem = name.StartsWith("MI_") ? name.Substring(3) : name.Substring(2);
            string albedo = null;
            foreach (string folder in textureFolders)
            {
                albedo = ResolveTexture(folder, $"T_{stem}_B.png") ?? ResolveTexture(folder, $"T_{stem}_BaseColor.png");
                if (albedo != null)
                {
                    break;
                }
            }
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
            bool isProp = albedo.Contains("/PropTextures/");
            string normalStem = $"T_{stem}_N";
            string normal = isProp && File.Exists(Path.GetFullPath($"{SourceDir}/PropTexturesUnity/{normalStem}_N_Unity.png"))
                ? $"{SourceDir}/PropTexturesUnity/{normalStem}_N_Unity.png"
                : ResolveTexture(isProp ? SourceDir + "/PropTextures" : SourceDir + "/KitTextures", normalStem + ".png");
            string mask = isProp && File.Exists(Path.GetFullPath($"{SourceDir}/PropTexturesUnity/T_{stem}_ORM_Pack.png"))
                ? $"{SourceDir}/PropTexturesUnity/T_{stem}_ORM_Pack.png"
                : null;
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
            EditorUtility.SetDirty(material);
            materials[name] = material;
        }
        AssetDatabase.SaveAssets();
        return materials;
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

        // Bounds + triangle count from the imported geometry.
        Bounds bounds = ComputeBounds(instance, out int triangles);
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
                meshColliderComponent.sharedMesh = LargestMesh(instance);
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
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
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
        }
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            if (!found)
            {
                bounds = renderer.bounds;
                found = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
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

    private static void BuildMirroredWrapper(FbxSource source, WrapperInfo info, Dictionary<Mesh, Mesh> mirrorMeshMap)
    {
        // Mirror every distinct mesh of the source FBX once.
        var localMap = new Dictionary<Mesh, Mesh>();
        foreach (Mesh mesh in AssetDatabase.LoadAllAssetsAtPath(source.Path).OfType<Mesh>())
        {
            if (!mirrorMeshMap.TryGetValue(mesh, out Mesh mirrored))
            {
                mirrored = CreateMirroredMesh(mesh);
                mirrorMeshMap[mesh] = mirrored;
                string assetPath = $"{GeneratedMeshesDir}/{source.Name}_M.asset";
                if (!File.Exists(Path.GetFullPath(assetPath)))
                {
                    AssetDatabase.CreateAsset(mirrored, assetPath);
                }
                else
                {
                    AssetDatabase.AddObjectToAsset(mirrored, assetPath);
                }
            }
            localMap[mesh] = mirrored;
        }
        AssetDatabase.SaveAssets();

        var wrapperAsset = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabsDir}/{source.Name}.prefab");
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(wrapperAsset);
        instance.name = source.Name + "_M";
        foreach (MeshFilter filter in instance.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh != null && localMap.TryGetValue(filter.sharedMesh, out Mesh mirrored))
            {
                filter.sharedMesh = mirrored;
            }
        }
        foreach (MeshCollider collider in instance.GetComponentsInChildren<MeshCollider>(true))
        {
            if (collider.sharedMesh != null && localMap.TryGetValue(collider.sharedMesh, out Mesh mirrored))
            {
                collider.sharedMesh = mirrored;
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
        UnityEngine.Object.DestroyImmediate(instance);
    }

    private static Mesh CreateMirroredMesh(Mesh source)
    {
        var mirrored = new Mesh
        {
            name = source.name + "_M",
            indexFormat = source.indexFormat,
        };
        Vector3[] vertices = source.vertices;
        for (int i = 0; i < vertices.Length; i++)
        {
            vertices[i].x = -vertices[i].x;
        }
        mirrored.vertices = vertices;

        Vector3[] normals = source.normals;
        for (int i = 0; i < normals.Length; i++)
        {
            normals[i].x = -normals[i].x;
        }
        mirrored.normals = normals;

        if (source.tangents != null && source.tangents.Length == vertices.Length)
        {
            Vector4[] tangents = source.tangents;
            for (int i = 0; i < tangents.Length; i++)
            {
                tangents[i].x = -tangents[i].x;
                tangents[i].w = -tangents[i].w;
            }
            mirrored.tangents = tangents;
        }
        mirrored.uv = source.uv;
        mirrored.uv2 = source.uv2;
        mirrored.colors = source.colors;

        mirrored.subMeshCount = source.subMeshCount;
        for (int sub = 0; sub < source.subMeshCount; sub++)
        {
            int[] triangles = source.GetTriangles(sub);
            for (int i = 0; i < triangles.Length; i += 3)
            {
                (triangles[i + 1], triangles[i + 2]) = (triangles[i + 2], triangles[i + 1]);
            }
            mirrored.SetTriangles(triangles, sub);
        }
        mirrored.RecalculateBounds();
        return mirrored;
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
        MansionPartCatalog catalog, SortedSet<string> unmappedMaterials, ManifestData manifest)
    {
        string json = JsonUtility.ToJson(catalog, true);
        File.WriteAllText(Path.GetFullPath(AuditDir + "/part-audit.json"), json);

        var report = new StringBuilder();
        report.AppendLine("# Mansion part audit (generated by MansionPrefabFactory)");
        report.AppendLine();
        report.AppendLine($"- Parts: {catalog.entries.Count} " +
                          $"({catalog.entries.Count(e => e.partClass == "KitStructural")} kit, " +
                          $"{catalog.entries.Count(e => e.partClass != "KitStructural")} props)");
        report.AppendLine($"- Total placed triangles (all LODs, per part): {catalog.entries.Sum(e => e.triangleCount):N0}");
        report.AppendLine($"- Mirrored variants baked: {catalog.entries.Count(e => e.mirroredVariant)} " +
                          $"({manifest.MirroredMeshes.Count} meshes have negative-determinant placements)");
        report.AppendLine($"- MeshCollider exceptions (Read/Write kept ON): " +
                          $"{string.Join(", ", catalog.entries.Where(e => e.meshCollider).Select(e => e.partName))}");
        report.AppendLine($"- Manifest actors with no mesh (UE volumes/barriers, skipped by the builder): {manifest.MeshlessStaticActors}");
        report.AppendLine($"- Unmapped material names (rendered magenta, fix the defs): {unmappedMaterials.Count}");
        foreach (string name in unmappedMaterials)
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
