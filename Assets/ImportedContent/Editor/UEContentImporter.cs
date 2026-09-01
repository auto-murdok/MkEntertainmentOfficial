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
//     (FBX under Meshes/, generated prefabs under Prefabs/)
//   - applies texture import rules by suffix (_N -> normal map, _ORM/_EM/_M ->
//     linear) and caps import size to the source resolution (max 4096)
//   - builds ORM channel packs for URP and normal-map green flips
//     (UE DirectX-style Y- -> Unity OpenGL-style Y+)
//   - creates URP/Lit materials per unique manifest material using
//     LAYER-AWARE texture selection: UE master materials here are layered
//     (params grouped by prefix like 00_, 04_Grunge_, 08_VCOL_, 12_AO_); the
//     picker ignores placeholder defaults (T_Base_*, T_Default_*, /Engine/)
//     and takes BaseColor/Normal/ORM from the layer whose BaseColor texture
//     is the most detailed (PNG byte size proxy - approximates UE's blended
//     result; flat tint layers compress tiny)
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
    const int MaxTextureSizeCap = 4096;

    // Render both faces on imported kit materials. Matches UE for genuinely
    // TwoSided masters AND masks winding flips that FBX export bakes into
    // mirrored/negatively-scaled modular pieces (Unity culls those from the
    // side that should be visible -> "transparent from one side"). Trade-off:
    // extra overdraw on large kits; genuinely one-sided UE materials (manifest
    // two_sided=0) would also render their backfaces.
    const bool ForceTwoSided = true;

    struct Row
    {
        public string Mesh, Slot, Material, Param, Texture, TexturePath, TwoSided;
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
                    TexturePath = p.Length > 5 ? p[5].Trim() : "",
                    TwoSided = p.Length > 6 ? p[6].Trim() : "",
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

            // ---------------- import EVERY exported texture (not just the
            // ones wired into materials) so the kit's texture set is complete
            var allPngs = Directory.GetFiles(sourceFolder, "*.png");
            for (var i = 0; i < allPngs.Length; i++)
            {
                var name = Path.GetFileNameWithoutExtension(allPngs[i]);
                EditorUtility.DisplayProgressBar("UE Import", "texture " + name, i / (float)Math.Max(1, allPngs.Length));
                ImportTexture(sourceFolder, dest, name, texCache);
            }

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
                    var prefabPath = dest + "/Prefabs/" + goName + ".prefab";
                    Directory.CreateDirectory(dest + "/Prefabs");
                    var existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                    var go = existing != null
                        ? (GameObject)PrefabUtility.LoadPrefabContents(prefabPath)
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
        var meshDir = dest + "/Meshes";
        Directory.CreateDirectory(meshDir);
        var dst = meshDir + "/" + Path.GetFileName(src);
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

        var maxSize = PickMaxSize(src);
        var isNormal = texName.EndsWith("_N", StringComparison.OrdinalIgnoreCase);

        // import the original into Textures/ first (complete texture set)
        var dir = dest + "/Textures";
        Directory.CreateDirectory(dir);
        var dst = $"{dir}/{texName}.png";
        File.Copy(src, dst, true);
        ApplyTextureImport(dst, isNormal, isNormal, maxSize);

        // normals: wire a green-flipped copy (UE DirectX Y- -> Unity GL Y+);
        // the original stays in Textures/ for reference
        if (isNormal)
        {
            var flipped = GetOrCreateNormalFlip(sourceFolder, dest, texName, maxSize, cache);
            if (flipped != null) return flipped;
        }

        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(dst);
        cache[texName] = tex;
        return tex;
    }

    static void ApplyTextureImport(string assetPath, bool normal, bool linear, int maxSize)
    {
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        var ti = (TextureImporter)AssetImporter.GetAtPath(assetPath);
        if (ti == null) return;
        ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        ti.sRGBTexture = !linear;
        ti.mipmapEnabled = true;
        if (maxSize > 0) ti.maxTextureSize = maxSize;
        ti.SaveAndReimport();
    }

    // PNG IHDR: width/height are big-endian at bytes 16..23
    static int PickMaxSize(string pngPath)
    {
        try
        {
            using var fs = File.OpenRead(pngPath);
            Span<byte> buf = stackalloc byte[24];
            if (fs.Read(buf) < 24) return 0;
            int w = (buf[16] << 24) | (buf[17] << 16) | (buf[18] << 8) | buf[19];
            int h = (buf[20] << 24) | (buf[21] << 16) | (buf[22] << 8) | buf[23];
            int need = Math.Max(w, h);
            int[] sizes = { 256, 512, 1024, 2048, 4096, 8192 };
            foreach (var s in sizes)
                if (s >= need)
                    return Math.Min(s, MaxTextureSizeCap);
            return MaxTextureSizeCap;
        }
        catch
        {
            return 0; // leave importer default
        }
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
            ti.maxTextureSize = PickMaxSize(dst);
            ti.SaveAndReimport();
        }

        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(dst);
        cache[ormTexName] = tex;
        return tex;
    }

    // --------------------------------------------------- normal green flip
    // UE saves DirectX-style normal maps (green = -Y); Unity expects OpenGL
    // (green = +Y). Invert the G channel once into Generated/.
    static Texture2D GetOrCreateNormalFlip(string sourceFolder, string dest, string texName,
        int maxSize, Dictionary<string, Texture2D> cache)
    {
        if (cache.TryGetValue(texName, out var cached)) return cached;

        var dir = dest + "/Generated";
        Directory.CreateDirectory(dir);
        var dst = $"{dir}/{texName}_N_Unity.png";

        if (!File.Exists(dst))
        {
            var src = Path.Combine(sourceFolder, texName + ".png");
            var nrm = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            nrm.LoadImage(File.ReadAllBytes(src));
            var px = nrm.GetPixels32();
            var flipped = new Color32[px.Length];
            for (var i = 0; i < px.Length; i++)
                flipped[i] = new Color32(px[i].r, (byte)(255 - px[i].g), px[i].b, px[i].a);
            var outTex = new Texture2D(nrm.width, nrm.height, TextureFormat.RGBA32, false, true);
            outTex.SetPixels32(flipped);
            outTex.Apply();
            File.WriteAllBytes(dst, outTex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(nrm);
            UnityEngine.Object.DestroyImmediate(outTex);
        }

        ApplyTextureImport(dst, true, true, maxSize);
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(dst);
        cache[texName] = tex;
        return tex;
    }

    // ------------------------------------------------------- layer selection
    // UE master materials here are layered: parameters are grouped by a
    // numeric prefix (00_BaseColor, 04_Grunge_BaseColor, 08_VCOL_BaseColor_A,
    // 12_AO_BaseColor, ...). Unprefixed params (ORM, BaseColor_Texture, ...)
    // belong to the master's root group and usually hold flat placeholder
    // defaults (T_Base_*, T_Default_*, /Engine/...). For each material we pick
    // the layer that has a REAL (non-placeholder) BaseColor and take that
    // layer's Normal/ORM with it, falling back across layers per role.
    static bool IsPlaceholder(Row r)
    {
        if (r.Texture.Length == 0) return true;
        if (r.TexturePath.StartsWith("/Engine/", StringComparison.OrdinalIgnoreCase)) return true;
        var n = r.Texture.ToLowerInvariant();
        return n.StartsWith("t_base_") || n == "t_default_normal" || n.StartsWith("t_normal");
    }

    static string LayerOf(string param)
    {
        // layer prefix = leading digits + underscore (e.g. "00_", "04_", "12_")
        for (var i = 0; i < param.Length; i++)
        {
            if (char.IsDigit(param[i])) continue;
            return (i >= 2 && param[i] == '_') ? param.Substring(0, i + 1) : "";
        }
        return "";
    }

    static bool RoleMatch(string param, string role)
    {
        var p = param.ToLowerInvariant();
        switch (role)
        {
            case "base": return p.Contains("basecolor");
            case "normal": return p.Contains("normal");
            // "normal" contains "orm" - guard so normal params don't match ORM
            case "orm": return p.Contains("orm") && !p.Contains("normal");
            default: return false;
        }
    }

    static string FindInLayer(List<Row> layerRows, string role)
    {
        foreach (var r in layerRows)
            if (RoleMatch(r.Param, role))
                return r.Texture; // first occurrence wins (instance overrides first)
        return null;
    }

    static (string baseTex, string normalTex, string ormTex) SelectMaterialTextures(List<Row> matRows, string sourceFolder)
    {
        var layers = new Dictionary<string, List<Row>>();
        foreach (var r in matRows)
        {
            if (r.Texture.Length == 0 || IsPlaceholder(r)) continue;
            var layer = LayerOf(r.Param);
            if (!layers.TryGetValue(layer, out var list))
                layers[layer] = list = new List<Row>();
            list.Add(r);
        }

        // choose the layer with a real BaseColor; prefer the most DETAILED one -
        // PNG byte size is the detail proxy (flat tint layers compress tiny,
        // e.g. a 2048^2 tint = ~78 KB vs a detailed concrete = ~7.8 MB), which
        // approximates UE's blended result far better than picking by row count
        string chosen = null;
        long bestSize = -1;
        foreach (var kv in layers)
        {
            var baseRow = kv.Value.FirstOrDefault(r => RoleMatch(r.Param, "base"));
            if (baseRow.Texture == null) continue;
            long size = 0;
            try
            {
                var png = Path.Combine(sourceFolder, baseRow.Texture + ".png");
                if (File.Exists(png)) size = new FileInfo(png).Length;
            }
            catch { }
            if (size > bestSize)
            {
                bestSize = size;
                chosen = kv.Key;
            }
        }

        if (chosen == null)
            return (null, null, null); // material is flat placeholders in UE too

        var chosenRows = layers[chosen];
        var baseTex = FindInLayer(chosenRows, "base");
        if (baseTex == null)
            baseTex = layers.Values.SelectMany(v => v).FirstOrDefault(r => RoleMatch(r.Param, "base")).Texture;
        var normalTex = FindInLayer(chosenRows, "normal");
        if (normalTex == null)
            normalTex = layers.Values.SelectMany(v => v).FirstOrDefault(r => RoleMatch(r.Param, "normal")).Texture;
        var ormTex = FindInLayer(chosenRows, "orm");
        if (ormTex == null)
            ormTex = layers.Values.SelectMany(v => v).FirstOrDefault(r => RoleMatch(r.Param, "orm")).Texture;
        return (baseTex, normalTex, ormTex);
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

        var matRows = rows.Where(r => r.Material == matName).ToList();
        var (baseTexName, normalTexName, ormTexName) = SelectMaterialTextures(matRows, sourceFolder);

        if (baseTexName != null)
        {
            var t = ImportTexture(sourceFolder, dest, baseTexName, texCache);
            if (t != null) mat.SetTexture("_BaseMap", t);
            else errors.Add($"{matName}: base color PNG missing ({baseTexName})");
        }
        else
        {
            errors.Add($"{matName}: no non-placeholder base color in manifest (flat material in UE)");
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

        // two-sided handling: ForceTwoSided renders both faces unconditionally;
        // otherwise the manifest two_sided column decides, defaulting to
        // two-sided when absent (matches UE interior kits)
        var twoSidedStr = matRows.Select(r => r.TwoSided).FirstOrDefault(v => v.Length > 0);
        var twoSided = ForceTwoSided
                        || twoSidedStr.Length == 0
                        || twoSidedStr == "1"
                        || twoSidedStr.Equals("true", StringComparison.OrdinalIgnoreCase);
        mat.SetFloat("_Cull", twoSided ? 0f : 2f); // 0 = Off (both faces), 2 = Back

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
