using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// On-demand import pipeline for the UE5 "Mansion" pack conversion.
    /// Source data lives OUTSIDE Assets (project root "Mansion/" folder) and is therefore
    /// never part of the game or builds. "1. Import Assets Into Game" copies it into
    /// Assets/_Game/Art/Mansion and imports it; "6. Remove From Game" strips it again.
    /// Steps 2-5 then rebuild FBX settings, URP materials and level prefabs.
    /// </summary>
    public static class MansionLevelImporter
    {
        private const string SourceFolderName = "Mansion";              // project root, outside Assets
        private const string ImportedRoot = "Assets/_Game/Art/Mansion"; // inside the game (on demand)

        private static string SourceRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "..", SourceFolderName));
        private static string SourceModels => Path.Combine(SourceRoot, "Models");
        private static string SourceTextures => Path.Combine(SourceRoot, "Textures");
        private static string SourceLevels => Path.Combine(SourceRoot, "Levels");

        private static string ImportedModels => ImportedRoot + "/Models";
        private static string ImportedTextures => ImportedRoot + "/Textures";
        private static string ImportedMaterials => ImportedRoot + "/Materials";
        private static string ImportedLevelPrefabs => ImportedRoot + "/Levels";

        private static string ManifestPath => Path.Combine(SourceLevels, "manifest_unity.json");
        private static string ExportMapPath => Path.Combine(SourceLevels, "export_map_unity.json");

        [Serializable]
        private class MansionManifest
        {
            public List<MansionLevel> levels;
        }

        [Serializable]
        private class MansionLevel
        {
            public string path;
            public string name;
            public int count;
            public List<MansionInstance> instances;
        }

        [Serializable]
        private class MansionInstance
        {
            public string actor;
            public string mesh;
            public float[] position;
            public float[] basis;
        }

        [Serializable]
        private class ExportMap
        {
            public List<ExportEntry> entries;
        }

        [Serializable]
        private class ExportEntry
        {
            public string pkg;
            public string file;
        }

        // ------------------------------------------------------------------
        // 1. Import (copy source -> Assets, import incrementally)
        // ------------------------------------------------------------------

        private static Queue<string> _importQueue;
        private static int _importTotal;
        private static bool _importRunning;

        [MenuItem("Mansion/1. Import Assets Into Game")]
        public static void ImportAssetsIntoGame()
        {
            if (!Directory.Exists(SourceModels) || !Directory.Exists(SourceTextures))
            {
                Debug.LogError($"Mansion: source folder not found at {SourceRoot}");
                return;
            }
            if (_importRunning)
            {
                Debug.Log("Mansion: import already running.");
                return;
            }

            Directory.CreateDirectory(ImportedModels);
            Directory.CreateDirectory(ImportedTextures);

            _importQueue = new Queue<string>();
            foreach (var f in Directory.GetFiles(SourceModels, "*.fbx"))
            {
                string dst = Path.Combine(ImportedModels, Path.GetFileName(f)).Replace("\\", "/");
                if (!File.Exists(dst))
                    File.Copy(f, dst, true);
                _importQueue.Enqueue(dst);
            }
            foreach (var f in Directory.GetFiles(SourceTextures, "*.png"))
            {
                string dst = Path.Combine(ImportedTextures, Path.GetFileName(f)).Replace("\\", "/");
                if (!File.Exists(dst))
                    File.Copy(f, dst, true);
                _importQueue.Enqueue(dst);
            }

            _importTotal = _importQueue.Count;
            if (_importTotal == 0)
            {
                Debug.Log("Mansion: all assets already imported.");
                return;
            }

            _importRunning = true;
            EditorApplication.update += StepImport;
            Debug.Log($"Mansion: importing {_importTotal} assets into {ImportedRoot} (incremental)...");
        }

        private static void StepImport()
        {
            int budget = 6;
            while (budget-- > 0 && _importQueue.Count > 0)
                AssetDatabase.ImportAsset(_importQueue.Dequeue(), ImportAssetOptions.ForceSynchronousImport);

            int done = _importTotal - _importQueue.Count;
            if (_importQueue.Count == 0)
            {
                EditorApplication.update -= StepImport;
                _importRunning = false;
                AssetDatabase.Refresh();
                Debug.Log($"Mansion: imported {_importTotal} assets. Run 'Mansion/2' next.");
            }
            else if (done % 100 == 0)
            {
                Debug.Log($"Mansion: import {done}/{_importTotal}");
            }
        }

        // ------------------------------------------------------------------
        // 2. Configure FBX importers
        // ------------------------------------------------------------------

        private static Queue<string> _fbxQueue;
        private static int _fbxTotal;
        private static bool _fbxRunning;

        [MenuItem("Mansion/2. Configure FBX Importers")]
        public static void ConfigureFbxImporters()
        {
            if (_fbxRunning)
            {
                Debug.Log("Mansion: FBX configuration already running.");
                return;
            }

            var guids = AssetDatabase.FindAssets("t:model", new[] { ImportedModels });
            _fbxQueue = new Queue<string>(guids.Select(AssetDatabase.GUIDToAssetPath).Where(p => AssetImporter.GetAtPath(p) is ModelImporter));
            _fbxTotal = _fbxQueue.Count;
            if (_fbxTotal == 0)
            {
                Debug.Log("Mansion: no FBX importers to configure.");
                return;
            }
            _fbxRunning = true;
            EditorApplication.update += StepFbxImporters;
            Debug.Log($"Mansion: configuring {_fbxTotal} FBX importers (incremental)...");
        }

        private static void StepFbxImporters()
        {
            int budget = 4;
            while (budget-- > 0 && _fbxQueue.Count > 0)
            {
                string path = _fbxQueue.Dequeue();
                if (AssetImporter.GetAtPath(path) is ModelImporter importer &&
                    (importer.materialName != ModelImporterMaterialName.BasedOnMaterialName ||
                     importer.materialLocation != ModelImporterMaterialLocation.External ||
                     importer.materialImportMode == ModelImporterMaterialImportMode.None))
                {
                    importer.materialName = ModelImporterMaterialName.BasedOnMaterialName;
                    importer.materialLocation = ModelImporterMaterialLocation.External;
                    importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                    importer.SaveAndReimport();
                }
            }

            int done = _fbxTotal - _fbxQueue.Count;
            if (_fbxQueue.Count == 0)
            {
                EditorApplication.update -= StepFbxImporters;
                _fbxRunning = false;
                Debug.Log($"Mansion: configured {_fbxTotal} FBX importers (material naming = slot name).");
            }
            else if (done % 50 == 0)
            {
                Debug.Log($"Mansion: FBX importer config {done}/{_fbxTotal}");
            }
        }

        // ------------------------------------------------------------------
        // 3. Generate URP materials from the texture set
        // ------------------------------------------------------------------

        [MenuItem("Mansion/3. Generate URP Materials")]
        public static void GenerateMaterials()
        {
            if (!AssetDatabase.IsValidFolder(ImportedTextures))
            {
                Debug.LogError("Mansion: textures not imported. Run 'Mansion/1. Import Assets Into Game' first.");
                return;
            }
            Directory.CreateDirectory(ImportedMaterials);
            // Primary: generic material JSON import (from UE MaterialEditingLibrary) - handles any material in one shot
            int fromJson = GenerateMaterialsFromJson();
            // Fallback: heuristic for materials without JSON or unmatched slots
            var texturesByStem = IndexTextures();
            var slotStems = CollectSlotStems();
            int created = 0, matched = 0;
            foreach (var slotStem in slotStems)
            {
                string matPath = $"{ImportedMaterials}/{slotStem}.mat";
                if (AssetDatabase.LoadAssetAtPath<Material>(matPath) != null)
                {
                    matched++;
                    continue;
                }
                var texSet = ResolveTexSet(slotStem, texturesByStem);
                if (texSet == null) continue;
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                if (texSet.Albedo != null) mat.SetTexture("_BaseMap", texSet.Albedo);
                if (texSet.Normal != null) { mat.SetTexture("_BumpMap", texSet.Normal); mat.EnableKeyword("_NORMALMAP"); mat.SetFloat("_BumpScale", 1f); }
                if (texSet.Orm != null)
                {
                    var mask = EnsureMaskTexture(texSet.Orm.name + ".png", 0f, 1f, 1f);
                    if (mask != null) { mat.SetTexture("_MetallicGlossMap", mask); mat.SetTexture("_OcclusionMap", mask); mat.EnableKeyword("_METALLICSPECGLOSSMAP"); mat.EnableKeyword("_OCCLUSIONMAP"); mat.SetFloat("_Metallic", 1f); mat.SetFloat("_Smoothness", 1f); mat.SetFloat("_OcclusionStrength", 1f); mat.SetFloat("_WorkflowMode", 1f); }
                    else { mat.SetFloat("_Metallic", 0f); mat.SetFloat("_Smoothness", 0.65f); }
                }
                AssetDatabase.CreateAsset(mat, matPath);
                created++;
                matched++;
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Mansion: generated {created} heuristic materials ({matched}/{slotStems.Count} slots matched) + {fromJson} from JSON. Total stems {texturesByStem.Count}.");
        }

        private static int GenerateMaterialsFromJson()
        {
            // UE -> Unity fidelity pipeline (see docs/mansion_uasset_import.md):
            // - BaseColor/Normal/ORM mapped by parameter name (not filename heuristic)
            // - ORM (R=AO,G=Rough,B=Metal) swizzled to Unity Mask (R=Metal,G=AO,A=Smoothness=1-Rough)
            //   Scalars Metallic/Roughness/AO_Intensity baked into mask channels.
            // - Brightness/Desaturation/HueShift applied as _BaseColor tint.
            string jsonDir = Path.Combine(SourceRoot, "Materials");
            if (!Directory.Exists(jsonDir)) return 0;
            int created = 0;
            foreach (var jsonPath in Directory.GetFiles(jsonDir, "MAT_*.json"))
            {
                string json = File.ReadAllText(jsonPath);
                var matInfo = JsonUtility.FromJson<MaterialJson>(json);
                if (matInfo == null || string.IsNullOrEmpty(matInfo.name)) continue;
                string stem = NormalizeSlotName(matInfo.name);
                string matPath = $"{ImportedMaterials}/{stem}.mat";
                if (AssetDatabase.LoadAssetAtPath<Material>(matPath) != null) continue;
                var scalars = ParseScalars(json); // robust parse for UE scalar overrides
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                bool hasTex = false;
                Texture2D baseTex = null, normalTex = null;
                string ormTexName = null;
                foreach (var t in matInfo.textures)
                {
                    string texFileName = Path.GetFileNameWithoutExtension(t.texture.Split('/').Last()) + ".png";
                    string texPath = $"{ImportedTextures}/{texFileName}";
                    var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
                    if (tex == null) continue;
                    string param = t.parameter.ToLowerInvariant();
                    if (param.Contains("basecolor") || param.Contains("base_color") || param == "basecolor")
                    {
                        baseTex = tex; hasTex = true;
                    }
                    else if (param.Contains("normal"))
                    {
                        normalTex = tex; hasTex = true;
                    }
                    else if (param.Contains("opacity"))
                    {
                        mat.SetFloat("_Surface", 1f);
                        mat.SetFloat("_Blend", 0f);
                        mat.SetOverrideTag("RenderType", "Transparent");
                        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                        mat.SetFloat("_AlphaClip", 0f);
                        hasTex = true;
                    }
                    else if (param.Contains("orm") || param.Contains("roughness") || param.Contains("metallic"))
                    {
                        if (param.Contains("orm") && !texFileName.StartsWith("T_Base", StringComparison.OrdinalIgnoreCase))
                            ormTexName = texFileName; // keep UE ORM tex name for mask generation
                        hasTex = true;
                    }
                }
                if (!hasTex) continue;
                // Base + Normal
                if (baseTex != null) mat.SetTexture("_BaseMap", baseTex);
                if (normalTex != null) { mat.SetTexture("_BumpMap", normalTex); mat.EnableKeyword("_NORMALMAP"); mat.SetFloat("_BumpScale", GetScalar(scalars, "NormalIntensity", 1f)); }
                // ORM -> Mask swizzle with scalar ints baked
                if (!string.IsNullOrEmpty(ormTexName))
                {
                    float metInt = GetScalar(scalars, "Metallic_Intensity", GetScalar(scalars, "MetallicIntensity", 0f));
                    float roughInt = GetScalar(scalars, "Roughness_Intensity", GetScalar(scalars, "RoughnessIntensity", 1f));
                    float aoInt = GetScalar(scalars, "AmbientOclussion_Intensity", GetScalar(scalars, "AOIntensity", 1f));
                    // fallback: if generic Mat has MetalicIntensity typo
                    if (matInfo.name.ToLowerInvariant().Contains("glass")) { metInt = 0f; roughInt = 0.1f; }
                    var mask = EnsureMaskTexture(ormTexName, metInt, roughInt, aoInt);
                    if (mask != null)
                    {
                        mat.SetTexture("_MetallicGlossMap", mask);
                        mat.SetTexture("_OcclusionMap", mask);
                        mat.EnableKeyword("_METALLICSPECGLOSSMAP");
                        mat.EnableKeyword("_OCCLUSIONMAP");
                        mat.SetFloat("_Metallic", 1f);
                        mat.SetFloat("_Smoothness", 1f);
                        mat.SetFloat("_OcclusionStrength", 1f);
                        mat.SetFloat("_WorkflowMode", 1f);
                    }
                }
                // Brightness / tint (UE Brightness 1 = white, 0.6 = darker)
                float brightness = GetScalar(scalars, "Brightness", 1f);
                mat.SetColor("_BaseColor", new Color(brightness, brightness, brightness, 1f));
                mat.SetColor("_Color", new Color(brightness, brightness, brightness, 1f));
                mat.SetFloat("_Surface", 0f); mat.SetFloat("_AlphaClip", 0f); mat.SetFloat("_Cull", 2f); mat.SetOverrideTag("RenderType", "Opaque");
                AssetDatabase.CreateAsset(mat, matPath);
                created++;
            }
            return created;
        }

        [Serializable]
        private class MaterialJson
        {
            public string name;
            public string package;
            public List<TextureRef> textures;
        }
        [Serializable]
        private class TextureRef
        {
            public string parameter;
            public string texture;
        }

        private static Dictionary<string, float> ParseScalars(string json)
        {
            var dict = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            try
            {
                // Minimal parse: extract "scalars": [ {"name":"X","value":Y}, ... ]
                int s = json.IndexOf("\"scalars\"", StringComparison.Ordinal);
                if (s < 0) return dict;
                int a = json.IndexOf('[', s);
                int b = json.LastIndexOf(']');
                if (a < 0 || b < 0) return dict;
                string arr = json.Substring(a, b - a + 1);
                var rx = new System.Text.RegularExpressions.Regex("\"name\"\\s*:\\s*\"([^\"]+)\"\\s*,\\s*\"value\"\\s*:\\s*([0-9eE+\\-\\.]+)");
                foreach (System.Text.RegularExpressions.Match m in rx.Matches(arr))
                {
                    if (float.TryParse(m.Groups[2].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v))
                        dict[m.Groups[1].Value] = v;
                }
            }
            catch { }
            return dict;
        }

        private static float GetScalar(Dictionary<string, float> d, string key, float fallback) => d.TryGetValue(key, out var v) ? v : fallback;

        private static Texture2D EnsureMaskTexture(string ormFileName, float metallicInt, float roughnessInt, float aoInt)
        {
            // ormFileName e.g. T_Bookcase01_ORM.png -> T_Bookcase01_Mask.png
            string maskFileName = ormFileName.Replace("_ORM", "_Mask", StringComparison.OrdinalIgnoreCase);
            if (maskFileName.Equals(ormFileName, StringComparison.OrdinalIgnoreCase))
                maskFileName = Path.GetFileNameWithoutExtension(ormFileName) + "_Mask.png";
            if (!maskFileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) maskFileName += ".png";
            string maskAssetPath = $"{ImportedTextures}/{maskFileName}";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(maskAssetPath);
            if (existing != null) return existing;
            // Source ORM png is in Mansion/Textures, not yet guaranteed to be imported? Use source root for pixel read
            string srcPath = Path.Combine(SourceTextures, Path.GetFileName(ormFileName));
            if (!File.Exists(srcPath)) srcPath = Path.Combine(SourceTextures, ormFileName);
            if (!File.Exists(srcPath))
            {
                // fallback: try imported texture path on disk
                string importedFs = Path.Combine(Path.GetFullPath(Path.Combine(Application.dataPath, "..", ImportedTextures)), maskFileName.Replace("_Mask.png", "_ORM.png"));
                if (File.Exists(importedFs)) srcPath = importedFs;
                else return null;
            }
            try
            {
                byte[] bytes = File.ReadAllBytes(srcPath);
                var orm = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
                orm.LoadImage(bytes);
                var mask = new Texture2D(orm.width, orm.height, TextureFormat.RGBA32, false, true);
                var sp = orm.GetPixels32();
                var dp = new Color32[sp.Length];
                for (int i = 0; i < sp.Length; i++)
                {
                    float ao = sp[i].r / 255f * aoInt;
                    float rough = sp[i].g / 255f * roughnessInt;
                    float met = sp[i].b / 255f * metallicInt;
                    byte mr = (byte)Mathf.Clamp((int)(met * 255), 0, 255);
                    byte mg = (byte)Mathf.Clamp((int)(ao * 255), 0, 255);
                    byte ma = (byte)Mathf.Clamp((int)((1f - Mathf.Clamp01(rough)) * 255), 0, 255);
                    dp[i] = new Color32(mr, mg, 0, ma);
                }
                mask.SetPixels32(dp); mask.Apply();
                byte[] outBytes = mask.EncodeToPNG();
                string fullDst = Path.Combine(Path.GetFullPath(Path.Combine(Application.dataPath, "..", ImportedTextures)), maskFileName);
                Directory.CreateDirectory(Path.GetDirectoryName(fullDst));
                File.WriteAllBytes(fullDst, outBytes);
                UnityEngine.Object.DestroyImmediate(orm); UnityEngine.Object.DestroyImmediate(mask);
                AssetDatabase.ImportAsset(maskAssetPath, ImportAssetOptions.ForceSynchronousImport);
                var imp = AssetImporter.GetAtPath(maskAssetPath) as TextureImporter;
                if (imp != null) { bool ch = false; if (imp.sRGBTexture) { imp.sRGBTexture = false; ch = true; } if (imp.textureType != TextureImporterType.Default) { imp.textureType = TextureImporterType.Default; ch = true; } if (ch) imp.SaveAndReimport(); }
                return AssetDatabase.LoadAssetAtPath<Texture2D>(maskAssetPath);
            }
            catch (Exception e) { Debug.LogWarning($"Mansion: mask gen failed for {ormFileName}: {e.Message}"); return null; }
        }

        // ------------------------------------------------------------------
        // 4. Build level prefabs from the manifest
        // ------------------------------------------------------------------

        [MenuItem("Mansion/4. Build Level Prefabs")]
        public static void BuildLevelPrefabs()
        {
            if (!File.Exists(ManifestPath))
            {
                Debug.LogError($"Mansion: manifest not found at {ManifestPath}");
                return;
            }
            if (!AssetDatabase.IsValidFolder(ImportedModels))
            {
                Debug.LogError("Mansion: meshes not imported. Run 'Mansion/1. Import Assets Into Game' first.");
                return;
            }

            var map = JsonUtility.FromJson<ExportMap>(File.ReadAllText(ExportMapPath));
            Directory.CreateDirectory(ImportedLevelPrefabs);
            var pkgToFile = new Dictionary<string, string>();
            foreach (var e in map.entries)
                pkgToFile[e.pkg] = e.file;

            var manifest = JsonUtility.FromJson<MansionManifest>(File.ReadAllText(ManifestPath));
            int totalInstances = 0, missingMeshes = 0;

            foreach (var level in manifest.levels)
            {
                var root = new GameObject(level.name);
                var prefabCache = new Dictionary<string, GameObject>();

                foreach (var inst in level.instances)
                {
                    // manifest stores object path names ("/Game/.../SM_X.SM_X"); export map keys are package paths
                    string pkg = inst.mesh;
                    int dot = pkg.IndexOf('.', StringComparison.Ordinal);
                    if (dot >= 0)
                        pkg = pkg.Substring(0, dot);
                    if (!pkgToFile.TryGetValue(pkg, out var fbxName))
                    {
                        missingMeshes++;
                        continue;
                    }
                    string fbxPath = $"{ImportedModels}/{fbxName}.fbx";
                    if (!prefabCache.TryGetValue(fbxPath, out var prefab))
                    {
                        prefab = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
                        prefabCache[fbxPath] = prefab;
                    }
                    if (prefab == null)
                    {
                        missingMeshes++;
                        continue;
                    }

                    var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
                    go.name = inst.actor;
                    DecomposeBasis(inst.basis, out var rot, out var scl);
                    go.transform.localPosition = new Vector3(inst.position[0], inst.position[1], inst.position[2]);
                    go.transform.localRotation = rot;
                    go.transform.localScale = scl;
                    totalInstances++;
                }

                string prefabPath = $"{ImportedLevelPrefabs}/{level.name}.prefab";
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                UnityEngine.Object.DestroyImmediate(root);
                Debug.Log($"Mansion: level '{level.name}' -> {level.instances.Count} instances saved to {prefabPath}");
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"Mansion: built {manifest.levels.Count} level prefabs, {totalInstances} instances placed, {missingMeshes} missing.");
        }

        // ------------------------------------------------------------------
        // 5. Apply generated materials to the level prefabs
        // ------------------------------------------------------------------

        [MenuItem("Mansion/5. Apply Materials To Level Prefabs")]
        public static void ApplyMaterials()
        {
            var guids = AssetDatabase.FindAssets("t:prefab", new[] { ImportedLevelPrefabs });
            var materials = LoadMaterialLookup();
            int assigned = 0, unmatched = 0, defaulted = 0;

            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (root == null)
                    continue;

                foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    var mats = renderer.sharedMaterials;
                    bool changed = false;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        var current = mats[i];
                        string slot = current != null ? NormalizeSlotName(current.name) : "";
                        if (!string.IsNullOrEmpty(slot) && materials.TryGetValue(slot, out var mat))
                        {
                            if (current != mat)
                            {
                                mats[i] = mat;
                                changed = true;
                                assigned++;
                            }
                        }
                        else if (IsBrokenImportMaterial(current))
                        {
                            // UE exports material instances with black diffuse; replace with neutral grey
                            mats[i] = GetDefaultMaterial();
                            changed = true;
                            defaulted++;
                        }
                        else
                        {
                            unmatched++;
                        }
                    }
                    if (changed)
                        renderer.sharedMaterials = mats;
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"Mansion: applied {assigned} generated materials, {defaulted} broken-black slots defaulted, {unmatched} slots left unmatched.");
        }

        // ------------------------------------------------------------------
        // 6. Remove from game (strip from Assets; source stays in project root)
        // ------------------------------------------------------------------

        [MenuItem("Mansion/6. Remove From Game")]
        public static void RemoveFromGame()
        {
            if (!AssetDatabase.IsValidFolder(ImportedRoot))
            {
                Debug.Log("Mansion: nothing imported - not part of the game.");
                return;
            }
            if (!EditorUtility.DisplayDialog("Remove Mansion From Game",
                    $"Delete {ImportedRoot}?\n\nThe source data stays in the project root '{SourceFolderName}/' folder and can be re-imported at any time via 'Mansion/1. Import Assets Into Game'.",
                    "Remove", "Cancel"))
                return;

            AssetDatabase.DeleteAsset(ImportedRoot);
            AssetDatabase.Refresh();
            Debug.Log("Mansion: removed from the game. Source data preserved in project root 'Mansion/'.");
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        private static void DecomposeBasis(float[] b, out Quaternion rot, out Vector3 scale)
        {
            // basis = 9 floats, row-major rows of the Unity-space 3x3 (rotation * scale)
            var m = Matrix4x4.identity;
            m.m00 = b[0]; m.m01 = b[3]; m.m02 = b[6];
            m.m10 = b[1]; m.m11 = b[4]; m.m12 = b[7];
            m.m20 = b[2]; m.m21 = b[5]; m.m22 = b[8];

            Vector3 c0 = m.GetColumn(0);
            Vector3 c1 = m.GetColumn(1);
            Vector3 c2 = m.GetColumn(2);

            float sx = c0.magnitude;
            float sy = c1.magnitude;
            float sz = c2.magnitude;
            bool mirrored = Matrix4x4.Determinant(m) < 0f;

            rot = Quaternion.LookRotation(c2.normalized, c1.normalized);
            scale = new Vector3(mirrored ? -sx : sx, sy, sz);
        }

        private class TexSet
        {
            public Texture2D Albedo;
            public Texture2D Normal;
            public Texture2D Orm;
        }

        private static Dictionary<string, TexSet> IndexTextures()
        {
            var result = new Dictionary<string, TexSet>();
            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { ImportedTextures });
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string file = Path.GetFileNameWithoutExtension(path);
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (tex == null)
                    continue;

                if (!TrySplitStem(file, out var stem, out var role))
                    continue;

                if (!result.TryGetValue(stem, out var set))
                {
                    set = new TexSet();
                    result[stem] = set;
                }

                string r = role.ToLowerInvariant();
                if (r.EndsWith("basecolor") || r.EndsWith("albedo") || r == "d" || r == "b" || r.EndsWith("diffuse") || r.EndsWith("color"))
                    set.Albedo ??= tex;
                else if (r.EndsWith("normal") || r == "n" || r.EndsWith("normalheight"))
                    set.Normal ??= tex;
                else if (r.EndsWith("orm"))
                    set.Orm ??= tex;
                else if (r.Length == 0)
                    set.Albedo ??= tex; // no recognized role -> assume base texture
            }
            return result;
        }

        private static readonly string[] KnownSuffixes =
        {
            "BaseColor", "Base_Color", "Albedo", "Diffuse", "Color", "NormalHeight",
            "Normal", "Roughness", "Metallic", "Metalness", "Specular", "Displacement",
            "Emissive", "Emission", "Opacity", "OpacityMask", "Height", "Bump",
            "ORM", "RMA", "MRA", "Mask", "MSK", "AO", "D", "N", "B"
        };

        private static bool TrySplitStem(string file, out string stem, out string role)
        {
            stem = role = "";
            if (string.IsNullOrEmpty(file) || file.Length < 4)
                return false;

            // "T_Column02_ORM" -> stem "Column02", role "ORM"; "T_Wood_Dark" -> stem "Wood_Dark", role ""
            string name = file.Substring(2); // strip "T_"
            foreach (var suffix in KnownSuffixes)
            {
                string tail = "_" + suffix;
                if (name.EndsWith(tail, StringComparison.OrdinalIgnoreCase))
                {
                    stem = name.Substring(0, name.Length - tail.Length);
                    role = suffix;
                    return stem.Length > 0;
                }
            }

            stem = name;
            return true;
        }

        private static TexSet ResolveTexSet(string slotStem, Dictionary<string, TexSet> texturesByStem)
        {
            if (texturesByStem.TryGetValue(slotStem, out var exact))
                return exact;

            // Learned pattern SM_Bed: MI_Bed (frame) vs MI_Mattress (fabric) share mesh but
            // textures are T_Bed_mattress_B. Generic rule:
            // 1) exact match wins
            // 2) texture stem == slot OR ends with _slot (e.g. Bed_mattress ends with _Mattress -> slot Mattress matches)
            // 3) Do NOT match prefix Bed -> Bed_mattress (frame would steal mattress texture) - so prefix matches are deprioritized.
            TexSet best = null;
            int bestLen = int.MaxValue;
            string slotLower = slotStem.ToLowerInvariant();
            foreach (var kv in texturesByStem)
            {
                string keyLower = kv.Key.ToLowerInvariant();
                bool endsWithSlot = keyLower == slotLower || keyLower.EndsWith("_" + slotLower);
                if (!endsWithSlot) continue;
                if (kv.Key.Length < bestLen)
                {
                    best = kv.Value;
                    bestLen = kv.Key.Length;
                }
            }
            if (best != null) return best;

            // Fallback: contains (for single-token slots like "Column02" -> "T_Column02_ORM" was exact already)
            // Only use fallback if no endsWith found and slot is not a generic word like "Bed" that would steal specific textures
            foreach (var kv in texturesByStem)
            {
                string keyLower = kv.Key.ToLowerInvariant();
                if (keyLower.Contains(slotLower) && kv.Key.Length < bestLen + 10) // len guard avoids Bed matching Bed_mattress when Mattress exists
                {
                    // Prefer textures where slot is a whole token
                    bool tokenMatch = keyLower.Contains("_" + slotLower) || keyLower.StartsWith(slotLower + "_");
                    if (!tokenMatch) continue;
                    if (kv.Key.Length < bestLen) { best = kv.Value; bestLen = kv.Key.Length; }
                }
            }
            return best;
        }

        private static HashSet<string> CollectSlotStems()
        {
            var stems = new HashSet<string>();
            var guids = AssetDatabase.FindAssets("t:model", new[] { ImportedModels });
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                    continue;
                foreach (var renderer in prefab.GetComponentsInChildren<Renderer>(true))
                {
                    foreach (var mat in renderer.sharedMaterials)
                    {
                        if (mat == null)
                            continue;
                        string stem = NormalizeSlotName(mat.name);
                        if (!string.IsNullOrEmpty(stem))
                            stems.Add(stem);
                    }
                }
            }
            return stems;
        }

        private static string NormalizeSlotName(string slotName)
        {
            // "MI_Wood" -> "Wood"; "M_Wood" -> "Wood"; "SM_Bed_MI_Bed" -> "Bed"
            string name = slotName;
            int marker = name.LastIndexOf("_MI_", StringComparison.OrdinalIgnoreCase);
            if (marker >= 0)
                name = name.Substring(marker + 4);
            marker = name.LastIndexOf("_M_", StringComparison.Ordinal);
            if (marker >= 0)
                name = name.Substring(marker + 3);
            if (name.StartsWith("MI_", StringComparison.OrdinalIgnoreCase) && name.Length > 3)
                name = name.Substring(3);
            else if (name.StartsWith("M_", StringComparison.Ordinal) && name.Length > 2)
                name = name.Substring(2);
            return name.Trim();
        }

        private static bool IsBrokenImportMaterial(Material m)
        {
            // Unity's FBX-imported UE material instances arrive with a black base color and no texture
            if (m == null)
                return true;
            if (m.name == "No Name" || m.name == "Fbx Default Material")
                return true;
            if (m.HasProperty("_BaseColor") && m.GetColor("_BaseColor").maxColorComponent <= 0.01f &&
                (!m.HasProperty("_BaseMap") || m.GetTexture("_BaseMap") == null))
                return true;
            return false;
        }

        private static Material GetDefaultMaterial()
        {
            string path = ImportedMaterials + "/Mansion_Default.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.SetColor("_BaseColor", new Color(0.72f, 0.7f, 0.66f));
                AssetDatabase.CreateAsset(mat, path);
            }
            return mat;
        }

        private static Dictionary<string, Material> LoadMaterialLookup()
        {
            var lookup = new Dictionary<string, Material>();
            var guids = AssetDatabase.FindAssets("t:material", new[] { ImportedMaterials });
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat != null)
                    lookup[NormalizeSlotName(mat.name)] = mat;
            }
            return lookup;
        }

        // ------------------------------------------------------------------
        // 7. Batch: create one prefab per model in ExternalModels (grows utility)
        // ------------------------------------------------------------------

        private const string ExternalPrefabs = "Assets/ExternalModels";
        private static Queue<string> _batchQueue;
        private static int _batchTotal;
        private static bool _batchRunning;
        private static Dictionary<string, TexSet> _texIndex;
        private static Dictionary<string, Material> _matLookup;

        [MenuItem("Mansion/7. Batch All ExternalModels Prefabs (231)")]
        public static void BatchAllExternalPrefabs()
        {
            if (_batchRunning) { Debug.Log("Mansion: batch already running."); return; }
            if (!AssetDatabase.IsValidFolder(ImportedModels)) { Debug.LogError("Mansion: run 1. Import first."); return; }

            // Ensure textures are correctly imported before batch
            FixAllTextureImporters();
            // Ensure materials exist
            GenerateMaterials();
            _texIndex = IndexTextures();
            _matLookup = LoadMaterialLookup();

            var guids = AssetDatabase.FindAssets("t:model", new[] { ImportedModels });
            var allPaths = guids.Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p).ToList();
            // Skip already-done prefabs
            _batchQueue = new Queue<string>(allPaths.Where(p => !File.Exists(GetExternalPrefabPath(p))));
            _batchTotal = allPaths.Count;
            int remaining = _batchQueue.Count;
            if (remaining == 0) { Debug.Log($"Mansion: all {_batchTotal} prefabs already exist in {ExternalPrefabs}."); return; }
            _batchRunning = true;
            Directory.CreateDirectory(ExternalPrefabs);
            EditorApplication.update += StepBatchPrefabs;
            Debug.Log($"Mansion: batch creating {remaining}/{_batchTotal} prefabs one-by-one...");
        }

        private static string GetExternalPrefabPath(string fbxPath) => $"{ExternalPrefabs}/{Path.GetFileNameWithoutExtension(fbxPath)}/{Path.GetFileNameWithoutExtension(fbxPath)}.prefab";

        private static void FixAllTextureImporters()
        {
            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { ImportedTextures });
            int fixedN = 0, fixedORM = 0;
            foreach (var g in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(g);
                string name = Path.GetFileNameWithoutExtension(path);
                if (!TrySplitStem(name, out _, out var role)) continue;
                var imp = AssetImporter.GetAtPath(path) as TextureImporter;
                if (imp == null) continue;
                bool isNormal = role.Equals("N", StringComparison.OrdinalIgnoreCase) || role.Equals("Normal", StringComparison.OrdinalIgnoreCase) || role.Equals("NormalHeight", StringComparison.OrdinalIgnoreCase);
                bool isORM = role.Equals("ORM", StringComparison.OrdinalIgnoreCase);
                bool changed = false;
                if (isNormal)
                {
                    if (imp.textureType != TextureImporterType.NormalMap) { imp.textureType = TextureImporterType.NormalMap; changed = true; }
                    if (imp.sRGBTexture) { imp.sRGBTexture = false; changed = true; }
                }
                else if (isORM)
                {
                    if (imp.sRGBTexture) { imp.sRGBTexture = false; changed = true; }
                }
                if (changed) { imp.SaveAndReimport(); if (isNormal) fixedN++; else fixedORM++; }
            }
            if (fixedN + fixedORM > 0) Debug.Log($"Mansion: fixed texture importers N={fixedN} ORM={fixedORM}");
        }

        private static void StepBatchPrefabs()
        {
            if (_batchQueue.Count == 0)
            {
                EditorApplication.update -= StepBatchPrefabs;
                _batchRunning = false;
                AssetDatabase.SaveAssets();
                Debug.Log($"Mansion: batch complete — {_batchTotal} prefabs in {ExternalPrefabs}.");
                return;
            }
            string fbxPath = _batchQueue.Dequeue();
            try { CreateOneExternalPrefab(fbxPath); }
            catch (System.Exception e) { Debug.LogError($"Mansion: failed {fbxPath}: {e.Message}"); }

            int done = _batchTotal - _batchQueue.Count;
            if (done % 20 == 0 || _batchQueue.Count == 0)
                Debug.Log($"Mansion: batch {done}/{_batchTotal} - {Path.GetFileNameWithoutExtension(fbxPath)}");
        }

        public static void CreateOneExternalPrefabForTest(string fbxPath) => CreateOneExternalPrefab(fbxPath);

        private static void CreateOneExternalPrefab(string fbxPath)
        {
            if (_texIndex == null) _texIndex = IndexTextures();
            if (_matLookup == null) _matLookup = LoadMaterialLookup();
            var fbxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (fbxPrefab == null) return;
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(fbxPrefab);
            instance.name = Path.GetFileNameWithoutExtension(fbxPath);

            // UCX -> MeshCollider
            Transform ucx = null;
            foreach (Transform t in instance.GetComponentsInChildren<Transform>(true))
                if (t.name.StartsWith("UCX_")) { ucx = t; break; }
            Mesh ucxMesh = null;
            if (ucx != null)
            {
                var mf = ucx.GetComponent<MeshFilter>();
                if (mf != null) ucxMesh = mf.sharedMesh;
                UnityEngine.Object.DestroyImmediate(ucx.gameObject);
            }
            if (ucxMesh != null)
            {
                var col = instance.AddComponent<MeshCollider>();
                col.sharedMesh = ucxMesh;
                col.convex = false;
            }
            else
            {
                // Fallback: collider from LOD0
                var lod0 = instance.transform.Find($"{instance.name}_LOD0");
                if (lod0 != null)
                {
                    var mf = lod0.GetComponent<MeshFilter>();
                    if (mf != null && mf.sharedMesh != null)
                    {
                        var col = instance.AddComponent<MeshCollider>();
                        col.sharedMesh = mf.sharedMesh;
                        col.convex = true;
                    }
                }
            }

            // LODGroup
            if (instance.GetComponent<LODGroup>() == null)
            {
                var renderers = instance.GetComponentsInChildren<Renderer>(true).Where(r => !r.name.StartsWith("UCX_")).ToArray();
                var lodGroups = new Dictionary<int, List<Renderer>>();
                foreach (var r in renderers)
                {
                    int lod = -1;
                    var idx = r.name.LastIndexOf("_LOD");
                    if (idx >= 0 && int.TryParse(r.name.Substring(idx + 4), out lod)) { }
                    else lod = 0;
                    if (!lodGroups.ContainsKey(lod)) lodGroups[lod] = new List<Renderer>();
                    lodGroups[lod].Add(r);
                }
                if (lodGroups.Count > 1)
                {
                    var sorted = lodGroups.OrderBy(kv => kv.Key).ToList();
                    var lods = new LOD[sorted.Count];
                    for (int i = 0; i < sorted.Count; i++)
                        lods[i] = new LOD(1f - i * (0.9f / sorted.Count), sorted[i].Value.ToArray());
                    var g = instance.AddComponent<LODGroup>();
                    g.SetLODs(lods);
                    g.RecalculateBounds();
                }
            }

            // Materials per renderer slot
            foreach (var r in instance.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    var cur = mats[i];
                    if (cur == null) continue;
                    string stem = NormalizeSlotName(cur.name);
                    if (string.IsNullOrEmpty(stem)) continue;
                    Material target = null;
                    if (_matLookup.TryGetValue(stem, out target)) { /* exact */ }
                    else target = ResolveTexSet(stem, _texIndex) != null ? EnsureMaterialForStem(stem) : null;
                    if (target != null && cur != target) { mats[i] = target; changed = true; }
                    else if (IsBrokenImportMaterial(cur))
                    {
                        mats[i] = GetDefaultMaterial();
                        changed = true;
                    }
                }
                if (changed) r.sharedMaterials = mats;
            }

            string outPath = GetExternalPrefabPath(fbxPath);
            Directory.CreateDirectory(Path.GetDirectoryName(outPath));
            PrefabUtility.SaveAsPrefabAsset(instance, outPath);
            UnityEngine.Object.DestroyImmediate(instance);
        }

        private static Material EnsureMaterialForStem(string stem)
        {
            string path = $"{ImportedMaterials}/{stem}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            if (!_texIndex.TryGetValue(stem, out var set) && (set = ResolveTexSet(stem, _texIndex)) == null) return null;
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if (set.Albedo != null) mat.SetTexture("_BaseMap", set.Albedo);
            if (set.Normal != null) { mat.SetTexture("_BumpMap", set.Normal); mat.EnableKeyword("_NORMALMAP"); mat.SetFloat("_BumpScale", 1f); }
            if (set.Orm != null)
            {
                // Fidelity: swizzle ORM->Mask with intensity baked (fallback met 0, rough 1, ao 1 when JSON not in this path)
                string ormName = set.Orm.name + ".png"; // asset name -> file
                var mask = EnsureMaskTexture(ormName, 0f, 1f, 1f);
                if (mask != null)
                {
                    mat.SetTexture("_MetallicGlossMap", mask);
                    mat.SetTexture("_OcclusionMap", mask);
                    mat.EnableKeyword("_METALLICSPECGLOSSMAP");
                    mat.EnableKeyword("_OCCLUSIONMAP");
                    mat.SetFloat("_Metallic", 1f);
                    mat.SetFloat("_Smoothness", 1f);
                    mat.SetFloat("_OcclusionStrength", 1f);
                    mat.SetFloat("_WorkflowMode", 1f);
                }
                else { mat.SetFloat("_Metallic", 0f); mat.SetFloat("_Smoothness", 0.65f); }
            }
            AssetDatabase.CreateAsset(mat, path);
            _matLookup[stem] = mat;
            return mat;
        }

        // ------------------------------------------------------------------
        // 8. Populate showcase scene with all ExternalModels prefabs in a grid
        // ------------------------------------------------------------------

        [MenuItem("Mansion/8. Populate Showcase Scene (All Models)")]
        public static void PopulateShowcaseScene()
        {
            var scenePath = "Assets/ExternalModels_Showcase.unity";
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath);
            // Clear existing SM_* instances (keep lights/cameras)
            foreach (var go in GameObject.FindObjectsOfType<GameObject>())
                if (go.name.StartsWith("SM_") && go.transform.parent == null)
                    UnityEngine.Object.DestroyImmediate(go);

            var guids = AssetDatabase.FindAssets("t:prefab", new[] { ExternalPrefabs });
            var paths = guids.Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p).ToList();
            int cols = 14;
            float spacing = 4.5f;
            for (int i = 0; i < paths.Count; i++)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(paths[i]);
                if (prefab == null) continue;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                int x = i % cols, z = i / cols;
                go.transform.position = new Vector3(x * spacing, 0, -z * spacing);
                // Ensure static for batching
                go.isStatic = true;
            }
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            EditorApplication.ExecuteMenuItem("File/Save");
            Debug.Log($"Mansion: showcase populated with {paths.Count} models in {cols}-col grid.");
        }
    }
}
