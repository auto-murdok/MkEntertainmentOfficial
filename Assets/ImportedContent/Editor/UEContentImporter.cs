// =============================================================================
// UEI - Unity-side batch importer for the UE -> Unity content pipeline.
// -----------------------------------------------------------------------------
// Consumes the output folder shape produced by Tools/UEImport (leg #1
// Run-Export.ps1 or leg #2 Convert-Cooked.ps1):
//
//     <sourceFolder>\<Mesh>.fbx
//     <sourceFolder>\<Texture>.png
//     <sourceFolder>\import_manifest.csv   (mesh,slot,material,param,texture,texture_path)
//
// What it does:
//   - copies + imports every FBX and PNG into Assets/ImportedContent/<folder>/
//   - applies texture import rules by suffix (_N -> normal map, _ORM/_EM/_M ->
//     linear) and builds ORM channel packs for URP
//   - creates URP/Lit materials per unique manifest material, wiring
//     BaseColor / Normal / ORM params (first manifest occurrence wins)
//   - URP-version aware: uses _MaskMap when the shader exposes it, otherwise
//     _MetallicGlossMap (R=metallic, A=smoothness) + _OcclusionMap (G=AO)
//   - saves one prefab per mesh with materials assigned by slot name
//   - handles UCX_ collision meshes (renderer off + convex MeshCollider) via
//     an AssetPostprocessor scoped to Assets/ImportedContent/
//
// Usage: Tools > UE Import > Import FBX Folder...
// (programmatic use: UEContentImporter.Run(sourceFolder))
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class UEContentImporter
{
    const string DestRoot = "Assets/ImportedContent";
    const string PrefsLastFolder = "UEImport.LastFolder";

    static readonly string[] BaseColorParams = { "00_BaseColor", "BaseColor", "Base_Color", "Color", "Albedo", "Diffuse" };
    static readonly string[] NormalParams = { "00_Normal", "Normal", "NormalMap", "Normal_Map" };
    static readonly string[] OrmParams = { "ORM", "00_ORM", "MaskMap", "OcclusionRoughnessMetallic" };

    struct Row
    {
        public string Mesh, Slot, Material, Param, Texture;
    }

    [MenuItem("Tools/UE Import/Import FBX Folder...")]
    public static void ImportFolder()
    {
        var start = EditorPrefs.GetString(PrefsLastFolder, "");
        var folder = EditorUtility.OpenFolderPanel("Select exported folder (contains *.fbx + import_manifest.csv)", start, "");
        if (string.IsNullOrEmpty(folder)) return;
        EditorPrefs.SetString(PrefsLastFolder, folder);
        Run(folder);
    }

    public static void Run(string sourceFolder)
    {
        sourceFolder = sourceFolder.TrimEnd('/', '\\');
        var kitName = Path.GetFileName(sourceFolder);
        var dest = DestRoot + "/" + kitName;
        var errors = new List<string>();

        // ------------------------------------------------------- manifest
        var manifestPath = Path.Combine(sourceFolder, "import_manifest.csv");
        var rows = new List<Row>();
        if (File.Exists(manifestPath))
        {
            foreach (var line in File.ReadLines(manifestPath).Skip(1))
            {
                var p = line.Split(',');
                if (p.Length < 5) continue;
                rows.Add(new Row
                {
                    Mesh = p[0].Trim(),
                    Slot = p[1].Trim(),
                    Material = p[2].Trim(),
                    Param = p[3].Trim(),
                    Texture = p[4].Trim(),
                });
            }
        }
        else
        {
            Debug.LogWarning("[UEImport] no import_manifest.csv in " + sourceFolder + " - meshes will import without material wiring");
        }

        var meshes = rows.Select(r => r.Mesh).Where(m => !string.IsNullOrEmpty(m)).Distinct().ToList();
        var fbxFiles = Directory.GetFiles(sourceFolder, "*.fbx").Select(Path.GetFileNameWithoutExtension).ToList();
        foreach (var f in fbxFiles.Where(f => !meshes.Contains(f)))
            meshes.Add(f); // FBX without manifest rows still gets imported

        try
        {
            Directory.CreateDirectory(dest);
            var texCache = new Dictionary<string, Texture2D>();
            var packCache = new Dictionary<string, Texture2D>();
            var matCache = new Dictionary<string, Material>();

            // shader capability probe (Unity 6.3 URP Lit has no _MaskMap)
            var urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null) throw new Exception("Universal Render Pipeline/Lit shader not found");
            var useMaskMap = new Material(urpLit).HasProperty("_MaskMap");

            int total = meshes.Count;
            for (var mi = 0; mi < meshes.Count; mi++)
            {
                var meshName = meshes[mi];
                EditorUtility.DisplayProgressBar("UE Import", meshName, mi / (float)total);
                try
                {
                    var fbxSrc = Path.Combine(sourceFolder, meshName + ".fbx");
                    if (!File.Exists(fbxSrc))
                    {
                        errors.Add("FBX missing: " + meshName);
                        continue;
                    }

                    // ---------------- slot -> material mapping for this mesh
                    var slotOrder = new List<string>();
                    var slotMaterials = new Dictionary<string, string>(); // slot -> material name
                    var usedMaterials = new List<string>();
                    foreach (var r in rows.Where(r => r.Mesh == meshName))
                    {
                        if (r.Slot.Length > 0 && !slotMaterials.ContainsKey(r.Slot))
                        {
                            slotMaterials[r.Slot] = r.Material;
                            slotOrder.Add(r.Slot);
                        }
                        if (r.Material.Length > 0 && !usedMaterials.Contains(r.Material))
                            usedMaterials.Add(r.Material);
                    }
                    if (usedMaterials.Count == 0) usedMaterials.Add(meshName);

                    // ---------------- materials
                    var resolved = new Dictionary<string, Material>();
                    foreach (var matName in usedMaterials)
                        resolved[matName] = GetOrCreateMaterial(matName, rows, sourceFolder, dest,
                            urpLit, useMaskMap, texCache, packCache, errors);

                    // ---------------- FBX + prefab
                    var fbxPath = ImportFbx(fbxSrc, dest);
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
                    if (prefab == null)
                    {
                        errors.Add("FBX import failed: " + meshName);
                        continue;
                    }

                    var goName = meshName;
                    var existing = AssetDatabase.LoadAssetAtPath<GameObject>(dest + "/" + goName + ".prefab");
                    var go = existing != null
                        ? (GameObject)PrefabUtility.LoadPrefabContents(dest + "/" + goName + ".prefab")
                        : (GameObject)PrefabUtility.InstantiatePrefab(prefab);

                    foreach (var rend in go.GetComponentsInChildren<MeshRenderer>(true))
                    {
                        var mats = rend.sharedMaterials;
                        for (var i = 0; i < mats.Length; i++)
                        {
                            Material target = null;
                            var importedName = mats[i] != null ? mats[i].name : null;
                            if (importedName != null && resolved.TryGetValue(importedName, out target))
                            {
                                // matched by imported material name
                            }
                            else if (i < slotOrder.Count && resolved.TryGetValue(slotMaterials[slotOrder[i]], out target))
                            {
                                // matched by manifest slot order
                            }
                            else if (slotOrder.Count > 0 && resolved.TryGetValue(slotMaterials[slotOrder[0]], out target))
                            {
                                // fallback: first slot's material
                            }
                            if (target != null) mats[i] = target;
                        }
                        rend.sharedMaterials = mats;
                    }

                    var prefabPath = dest + "/" + goName + ".prefab";
                    PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
                    if (existing != null) PrefabUtility.UnloadPrefabContents(go);
                    else UnityEngine.Object.DestroyImmediate(go);
                }
                catch (Exception ex)
                {
                    errors.Add(meshName + ": " + ex.Message);
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[UEImport] DONE kit='{kitName}' meshes={meshes.Count} materials={matCache.Count} textures={texCache.Count} packs={packCache.Count} errors={errors.Count}" +
                      (errors.Count > 0 ? "\n" + string.Join("\n", errors) : ""));
            if (errors.Count > 0)
                EditorUtility.DisplayDialog("UE Import", $"Finished with {errors.Count} error(s). See console for details.", "OK");
        }
        catch (Exception ex)
        {
            Debug.LogError("[UEImport] FAILED: " + ex);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    // ------------------------------------------------------------------ FBX
    static string ImportFbx(string src, string dest)
    {
        var dst = dest + "/" + Path.GetFileName(src);
        File.Copy(src, dst, true);
        AssetDatabase.ImportAsset(dst, ImportAssetOptions.ForceUpdate);
        return dst;
    }

    // -------------------------------------------------------------- textures
    static Texture2D ImportTexture(string sourceFolder, string dest, string texName,
        Dictionary<string, Texture2D> cache)
    {
        if (cache.TryGetValue(texName, out var cached)) return cached;

        var src = Path.Combine(sourceFolder, texName + ".png");
        if (!File.Exists(src))
        {
            Debug.LogWarning("[UEImport] texture PNG missing: " + texName);
            cache[texName] = null;
            return null;
        }

        var dir = dest + "/Textures";
        Directory.CreateDirectory(dir);
        var dst = $"{dir}/{texName}.png";
        File.Copy(src, dst, true);
        AssetDatabase.ImportAsset(dst, ImportAssetOptions.ForceUpdate);

        var ti = (TextureImporter)AssetImporter.GetAtPath(dst);
        if (ti != null)
        {
            var n = texName.ToLowerInvariant();
            if (n.EndsWith("_n"))
            {
                ti.textureType = TextureImporterType.NormalMap;
                ti.sRGBTexture = false;
            }
            else if (n.EndsWith("_orm") || n.EndsWith("_em") || n.EndsWith("_m") || n.EndsWith("_mask"))
            {
                ti.textureType = TextureImporterType.Default;
                ti.sRGBTexture = false;
            }
            else
            {
                ti.textureType = TextureImporterType.Default;
                ti.sRGBTexture = true;
            }
            ti.mipmapEnabled = true;
            ti.SaveAndReimport();
        }

        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(dst);
        cache[texName] = tex;
        return tex;
    }

    // ------------------------------------------------- ORM channel pack (URP)
    // R = metallic (ORM.B), G = AO (ORM.R), A = smoothness (1 - ORM.G)
    static Texture2D GetOrCreatePack(string sourceFolder, string dest, string ormTexName,
        Dictionary<string, Texture2D> cache)
    {
        if (cache.TryGetValue(ormTexName, out var cached)) return cached;

        var dir = dest + "/Generated";
        Directory.CreateDirectory(dir);
        var dst = $"{dir}/{ormTexName}_Pack.png";

        if (!File.Exists(dst))
        {
            var src = Path.Combine(sourceFolder, ormTexName + ".png");
            if (!File.Exists(src))
            {
                Debug.LogWarning("[UEImport] ORM PNG missing for pack: " + ormTexName);
                cache[ormTexName] = null;
                return null;
            }

            var orm = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            orm.LoadImage(File.ReadAllBytes(src));
            var px = orm.GetPixels32();
            var pack = new Color32[px.Length];
            for (var i = 0; i < px.Length; i++)
                pack[i] = new Color32(px[i].b, px[i].r, 0, (byte)(255 - px[i].g));
            var outTex = new Texture2D(orm.width, orm.height, TextureFormat.RGBA32, false, true);
            outTex.SetPixels32(pack);
            outTex.Apply();
            File.WriteAllBytes(dst, outTex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(orm);
            UnityEngine.Object.DestroyImmediate(outTex);
        }

        AssetDatabase.ImportAsset(dst, ImportAssetOptions.ForceUpdate);
        var ti = (TextureImporter)AssetImporter.GetAtPath(dst);
        if (ti != null)
        {
            ti.sRGBTexture = false;
            ti.mipmapEnabled = true;
            ti.SaveAndReimport();
        }

        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(dst);
        cache[ormTexName] = tex;
        return tex;
    }

    // ------------------------------------------------------------- materials
    static Material GetOrCreateMaterial(string matName, List<Row> rows, string sourceFolder,
        string dest, Shader urpLit, bool useMaskMap,
        Dictionary<string, Texture2D> texCache, Dictionary<string, Texture2D> packCache,
        List<string> errors)
    {
        var dir = dest + "/Materials";
        Directory.CreateDirectory(dir);
        var path = $"{dir}/{matName}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(urpLit) { name = matName };
            AssetDatabase.CreateAsset(mat, path);
        }

        // first manifest occurrence per parameter wins (instance overrides
        // come before parent-chain defaults)
        string FindTex(string[] candidates)
        {
            foreach (var c in candidates)
            {
                var row = rows.FirstOrDefault(r => r.Material == matName && r.Param == c && r.Texture.Length > 0);
                if (row.Texture != null) return row.Texture;
            }
            return null;
        }

        var baseTexName = FindTex(BaseColorParams);
        var normalTexName = FindTex(NormalParams);
        var ormTexName = FindTex(OrmParams);

        if (baseTexName != null)
        {
            var t = ImportTexture(sourceFolder, dest, baseTexName, texCache);
            if (t != null) mat.SetTexture("_BaseMap", t);
            else errors.Add($"{matName}: base color PNG missing ({baseTexName})");
        }
        if (normalTexName != null)
        {
            var t = ImportTexture(sourceFolder, dest, normalTexName, texCache);
            if (t != null) { mat.SetTexture("_BumpMap", t); mat.SetFloat("_BumpScale", 1f); }
            else errors.Add($"{matName}: normal PNG missing ({normalTexName})");
        }
        if (ormTexName != null)
        {
            var pack = GetOrCreatePack(sourceFolder, dest, ormTexName, packCache);
            if (pack != null)
            {
                if (useMaskMap)
                {
                    mat.SetTexture("_MaskMap", pack);
                }
                else
                {
                    mat.SetTexture("_MetallicGlossMap", pack);
                    mat.SetTexture("_OcclusionMap", pack);
                    mat.SetFloat("_GlossMapScale", 1f);
                    mat.DisableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
                }
                mat.SetFloat("_Metallic", 1f);
                mat.SetFloat("_OcclusionStrength", 1f);
            }
            else errors.Add($"{matName}: ORM PNG missing ({ormTexName})");
        }

        EditorUtility.SetDirty(mat);
        return mat;
    }
}

// =============================================================================
// Scoped postprocessor: UCX collision handling for anything imported under
// Assets/ImportedContent/ (replaces the old global ModelImportPostprocessor).
// UE convention: collision proxies are named UCX_<mesh> in the FBX.
// =============================================================================
class UEImportPostprocessor : AssetPostprocessor
{
    void OnPostprocessModel(GameObject root)
    {
        if (!assetPath.StartsWith("Assets/ImportedContent/", StringComparison.OrdinalIgnoreCase))
            return;

        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            if (!t.name.StartsWith("UCX", StringComparison.OrdinalIgnoreCase)) continue;
            var mf = t.GetComponent<MeshFilter>();
            var mr = t.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.enabled = false;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }
            if (mf != null && mf.sharedMesh != null && t.GetComponent<MeshCollider>() == null)
            {
                var col = t.gameObject.AddComponent<MeshCollider>();
                col.sharedMesh = mf.sharedMesh;
                col.convex = true;
            }
        }
    }
}
