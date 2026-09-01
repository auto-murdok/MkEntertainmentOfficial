#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Gold-standard batch fix for the asset audit (Textures + Models + FBM + Quality).
/// Usable three ways:
///   1. Editor menu: Tools → Audit → Fix Import Settings / Audit Only / Delete FBM Duplicates
///   2. Headless: Unity.exe -batchmode -projectPath . -executeMethod AssetAuditFix.ApplyAll -quit -logFile Logs/asset-fix.log
///   3. unity-cli: unity run . --command "Tools/Audit/Fix Import Settings"
///
/// All writes go through TextureImporter/ModelImporter APIs (never YAML regex) so GUIDs stay intact.
/// See RenderingScalabilitySetup.cs for the SerializedObject pattern for Quality/Graphics.
/// </summary>
public static class AssetAuditFix
{
    private const string LogPrefix = "[AssetAuditFix]";

    // ------------------------------------------------------------
    // Public entry points
    // ------------------------------------------------------------

    [MenuItem("Tools/Audit/Apply All Fixes (Textures + Models + FBM + Quality)")]
    public static void ApplyAllFromMenu()
    {
        int t = FixTextures();
        int m = FixModels();
        int f = DeleteFbmDuplicates();
        int q = FixQualityStreaming();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"{LogPrefix} ApplyAll done — textures:{t} models:{m} fbm:{f} quality:{q}. See logs above.");
    }

    [MenuItem("Tools/Audit/Audit Only (Log Issues, No Writes)")]
    public static void AuditOnly() => AuditOnlyInternal();

    [MenuItem("Tools/Audit/Delete FBM Duplicates")]
    public static void DeleteFbmFromMenu()
    {
        int n = DeleteFbmDuplicates();
        AssetDatabase.Refresh();
        Debug.Log($"{LogPrefix} FBM cleanup: deleted {n} orphaned files.");
    }

    /// <summary>Headless entry – called via -executeMethod.</summary>
    public static void ApplyAll()
    {
        Debug.Log($"{LogPrefix} ApplyAll (headless) starting — projectPath={Directory.GetCurrentDirectory()}");
        int t = FixTextures();
        int m = FixModels();
        int f = DeleteFbmDuplicates();
        int q = FixQualityStreaming();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"{LogPrefix} ApplyAll finished — textures:{t} models:{m} fbm:{f} quality:{q}");
        // Asset postprocessors will reimport on next domain reload; force synchronous reimport for CI.
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
    }

    // ------------------------------------------------------------
    // Textures
    // ------------------------------------------------------------

    public static int FixTextures()
    {
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/_Game" });
        int fixedCount = 0;
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            // Skip .fbm embedded textures – they are duplicates handled by DeleteFbmDuplicates.
            if (path.ToLowerInvariant().Contains(".fbm/"))
            {
                continue;
            }

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                continue;
            }

            bool changed = ApplyTextureRules(importer, path);
            // Apply platform overrides (needs TextureImporterPlatformSettings API).
            bool platformChanged = ApplyTexturePlatformOverrides(importer, path);
            changed |= platformChanged;

            if (changed)
            {
                EditorUtility.SetDirty(importer);
                importer.SaveAndReimport();
                fixedCount++;
            }
        }

        Debug.Log($"{LogPrefix} FixTextures — scanned:{guids.Length} fixed:{fixedCount}");
        return fixedCount;
    }

    private static bool ApplyTextureRules(TextureImporter importer, string assetPath)
    {
        string lower = assetPath.ToLowerInvariant();
        bool dirty = false;

        // Track desired values then compare.
        int desiredMax = importer.maxTextureSize;
        bool desiredMip = importer.mipmapEnabled;
        bool desiredStreaming = importer.streamingMipmaps;
        TextureWrapMode desiredWrap = importer.wrapMode;
        TextureImporterCompression desiredCompression = importer.textureCompression;
        bool desiredReadable = false; // always false for runtime textures.

        if (lower.Contains("/art/ui/"))
        {
            desiredMax = 256;
            desiredMip = false;
            desiredStreaming = false;
            desiredWrap = TextureWrapMode.Clamp;
            desiredCompression = TextureImporterCompression.Compressed;
        }
        else if (lower.Contains("smoke") || lower.Contains("muzzle") || lower.Contains("gunfx"))
        {
            desiredMax = 1024;
            desiredMip = true;
            desiredStreaming = true;
            desiredWrap = TextureWrapMode.Clamp;
            desiredCompression = TextureImporterCompression.Compressed;
        }
        else if (lower.Contains("/characters/"))
        {
            desiredMax = 1024; // perf was 2048
            desiredMip = true;
            desiredStreaming = true;
            desiredWrap = TextureWrapMode.Repeat;
            desiredCompression = TextureImporterCompression.Compressed;
        }
        else if (lower.Contains("/weapons/") || lower.Contains("prefabs/weapons"))
        {
            bool isBCw = lower.Contains("_bc.");
            desiredMax = isBCw ? 1024 : 512; // perf was 2048
            desiredMip = true;
            desiredStreaming = true;
            desiredWrap = TextureWrapMode.Repeat;
            desiredCompression = TextureImporterCompression.Compressed;
        }
        else if (lower.Contains("/environment/"))
        {
            desiredMax = 1024;
            desiredMip = true;
            desiredStreaming = true;
            desiredCompression = TextureImporterCompression.Compressed;
        }
        else if (lower.Contains("/buildingkit/"))
        {
            bool isBC = lower.Contains("_bc.");
            desiredMax = isBC ? 1024 : 512; // perf: was 2048/1024
            desiredMip = true;
            desiredStreaming = true;
            desiredWrap = TextureWrapMode.Repeat;
            desiredCompression = TextureImporterCompression.Compressed;
        }

        if (importer.maxTextureSize != desiredMax)
        {
            importer.maxTextureSize = desiredMax;
            dirty = true;
            Debug.Log($"{LogPrefix} {assetPath} maxTextureSize {importer.maxTextureSize}->{desiredMax}");
        }
        if (importer.mipmapEnabled != desiredMip)
        {
            importer.mipmapEnabled = desiredMip;
            dirty = true;
            Debug.Log($"{LogPrefix} {assetPath} mipmapEnabled {importer.mipmapEnabled}->{desiredMip}");
        }
        if (importer.streamingMipmaps != desiredStreaming)
        {
            importer.streamingMipmaps = desiredStreaming;
            dirty = true;
            Debug.Log($"{LogPrefix} {assetPath} streamingMipmaps {importer.streamingMipmaps}->{desiredStreaming}");
        }
        if (desiredStreaming && importer.streamingMipmapsPriority != 0)
        {
            importer.streamingMipmapsPriority = 0;
            dirty = true;
        }
        if (importer.wrapMode != desiredWrap && desiredWrap != importer.wrapMode)
        {
            // Only force clamp for VFX/UI where repeat causes edge bleeding.
            if (lower.Contains("smoke") || lower.Contains("muzzle") || lower.Contains("/art/ui/"))
            {
                importer.wrapMode = desiredWrap;
                dirty = true;
            }
        }
        if (importer.textureCompression != desiredCompression)
        {
            importer.textureCompression = desiredCompression;
            dirty = true;
        }
        if (importer.isReadable != desiredReadable)
        {
            importer.isReadable = desiredReadable;
            dirty = true;
        }

        // Preserve sRGB / normal map flags – don't touch importer.sRGBTexture / normalMap here.

        return dirty;
    }

    private static bool ApplyTexturePlatformOverrides(TextureImporter importer, string assetPath)
    {
        bool any = false;
        string lower = assetPath.ToLowerInvariant();

        // Standalone: BC7 for quality (if importer's textureType supports it).
        var standalone = importer.GetPlatformTextureSettings("Standalone");
        if (!standalone.overridden)
        {
            // Only override for characters/props where quality matters; leave UI sprites at default.
            bool shouldOverride = lower.Contains("/characters/") || lower.Contains("/weapons/") || lower.Contains("/environment/") || lower.Contains("/buildingkit/");
            if (shouldOverride)
            {
                var s = new TextureImporterPlatformSettings
                {
                    name = "Standalone",
                    overridden = true,
                    maxTextureSize = importer.maxTextureSize,
                    format = TextureImporterFormat.BC7,
                    textureCompression = TextureImporterCompression.Compressed,
                    compressionQuality = 50,
                    crunchedCompression = false
                };
                importer.SetPlatformTextureSettings(s);
                any = true;
                Debug.Log($"{LogPrefix} {assetPath} Standalone override -> BC7 {s.maxTextureSize}");
            }
        }

        // Android: ASTC 6x6 for balance (Unity Manual: ASTC recommended, ETC2 fallback auto).
        var android = importer.GetPlatformTextureSettings("Android");
        if (!android.overridden)
        {
            bool shouldOverride = lower.Contains("/characters/") || lower.Contains("/weapons/") || lower.Contains("/environment/") || lower.Contains("/buildingkit/") || lower.Contains("smoke");
            if (shouldOverride)
            {
                var a = new TextureImporterPlatformSettings
                {
                    name = "Android",
                    overridden = true,
                    maxTextureSize = importer.maxTextureSize <= 1024 ? importer.maxTextureSize : 1024,
                    format = TextureImporterFormat.ASTC_6x6,
                    textureCompression = TextureImporterCompression.Compressed,
                    compressionQuality = 50,
                    crunchedCompression = false,
                    androidETC2FallbackOverride = (int)AndroidETC2FallbackOverride.UseBuildSettings
                };
                importer.SetPlatformTextureSettings(a);
                any = true;
                Debug.Log($"{LogPrefix} {assetPath} Android override -> ASTC_6x6 {a.maxTextureSize}");
            }
        }

        // UI: cap all platforms at 256 (base already 256, but ensure overrides).
        if (lower.Contains("/art/ui/"))
        {
            foreach (string plat in new[] { "Standalone", "Android", "DefaultTexturePlatform" })
            {
                var p = importer.GetPlatformTextureSettings(plat);
                if (!p.overridden || p.maxTextureSize != 256)
                {
                    var au = new TextureImporterPlatformSettings
                    {
                        name = plat,
                        overridden = true,
                        maxTextureSize = 256,
                        format = plat == "Android" ? TextureImporterFormat.ASTC_6x6 : TextureImporterFormat.Automatic,
                        textureCompression = TextureImporterCompression.Compressed,
                        compressionQuality = 50
                    };
                    importer.SetPlatformTextureSettings(au);
                    any = true;
                }
            }
        }

        return any;
    }

    // ------------------------------------------------------------
    // Models
    // ------------------------------------------------------------

    public static int FixModels()
    {
        string[] guids = AssetDatabase.FindAssets("t:Model", new[] { "Assets/_Game" });
        int fixedCount = 0;
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
            {
                continue;
            }

            string lower = path.ToLowerInvariant();
            bool isCharacter = lower.Contains("/characters/") || lower.Contains("femalemodelyellow") || lower.Contains("zombiemodel");
            bool isProp = lower.Contains("/ammo") || lower.Contains("/weapons/") || lower.Contains("sm_") || lower.Contains("gun");

            bool dirty = false;
            if (isCharacter)
            {
                if (!importer.importBlendShapes)
                {
                    importer.importBlendShapes = true;
                    dirty = true;
                }
                if (importer.meshCompression != ModelImporterMeshCompression.Off)
                {
                    importer.meshCompression = ModelImporterMeshCompression.Off;
                    dirty = true;
                }
            }
            else if (isProp)
            {
                if (importer.importBlendShapes)
                {
                    importer.importBlendShapes = false;
                    dirty = true;
                    Debug.Log($"{LogPrefix} {path} importBlendShapes true->false");
                }
                if (importer.meshCompression != ModelImporterMeshCompression.Medium)
                {
                    importer.meshCompression = ModelImporterMeshCompression.Medium;
                    dirty = true;
                    Debug.Log($"{LogPrefix} {path} meshCompression {importer.meshCompression}->Medium");
                }
                if (importer.addCollider)
                {
                    importer.addCollider = false;
                    dirty = true;
                }
            }
            else
            {
                if (importer.importBlendShapes)
                {
                    importer.importBlendShapes = false;
                    dirty = true;
                }
                if (importer.meshCompression == ModelImporterMeshCompression.Off)
                {
                    importer.meshCompression = ModelImporterMeshCompression.Low;
                    dirty = true;
                }
            }

            if (importer.isReadable)
            {
                importer.isReadable = false;
                dirty = true;
            }

            if (dirty)
            {
                EditorUtility.SetDirty(importer);
                importer.SaveAndReimport();
                fixedCount++;
            }
        }

        Debug.Log($"{LogPrefix} FixModels — scanned:{guids.Length} fixed:{fixedCount}");
        return fixedCount;
    }

    // ------------------------------------------------------------
    // FBM duplicates
    // ------------------------------------------------------------

    public static int DeleteFbmDuplicates()
    {
        // FemaleModelYellow.fbm contains 4 exact copies of textures already at parent.
        // Delete the folder contents; FBX will still resolve via external PNGs.
        string[] fbmDirs = Directory.GetDirectories("Assets/_Game/Art/Characters/Survivor", "*.fbm", SearchOption.AllDirectories);
        int deleted = 0;
        foreach (string dir in fbmDirs)
        {
            string[] files = Directory.GetFiles(dir, "*", SearchOption.AllDirectories);
            foreach (string file in files)
            {
                if (file.EndsWith(".meta", System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                // AssetDatabase.DeleteAsset handles .meta cleanup.
                string assetPath = file.Replace("\\", "/");
                if (AssetDatabase.DeleteAsset(assetPath))
                {
                    deleted++;
                    Debug.Log($"{LogPrefix} deleted duplicate {assetPath}");
                }
                else if (File.Exists(file))
                {
                    File.Delete(file);
                    string meta = file + ".meta";
                    if (File.Exists(meta))
                    {
                        File.Delete(meta);
                    }
                    deleted++;
                    Debug.Log($"{LogPrefix} file-deleted {assetPath}");
                }
            }

            // Remove empty .fbm directory itself.
            try
            {
                if (Directory.Exists(dir) && Directory.GetFiles(dir, "*", SearchOption.AllDirectories).Length == 0)
                {
                    Directory.Delete(dir, true);
                    string metaDir = dir + ".meta";
                    if (File.Exists(metaDir))
                    {
                        File.Delete(metaDir);
                    }
                    Debug.Log($"{LogPrefix} removed empty dir {dir}");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"{LogPrefix} could not remove dir {dir}: {ex.Message}");
            }
        }

        if (deleted == 0)
        {
            Debug.Log($"{LogPrefix} no FBM duplicates found (already clean).");
        }
        return deleted;
    }

    // ------------------------------------------------------------
    // QualitySettings streaming
    // ------------------------------------------------------------

    private static int FixQualityStreaming()
    {
        string qualityPath = "ProjectSettings/QualitySettings.asset";
        var qualityAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(qualityPath);
        if (qualityAsset == null)
        {
            Debug.LogWarning($"{LogPrefix} could not load {qualityPath} – skipping quality streaming fix.");
            return 0;
        }

        SerializedObject so = new SerializedObject(qualityAsset);
        SerializedProperty levels = so.FindProperty("m_QualitySettings");
        if (levels == null || !levels.isArray)
        {
            Debug.LogWarning($"{LogPrefix} QualitySettings array not found.");
            return 0;
        }

        int changedLevels = 0;
        for (int i = 0; i < levels.arraySize; i++)
        {
            SerializedProperty lvl = levels.GetArrayElementAtIndex(i);
            SerializedProperty nameProp = lvl.FindPropertyRelative("name");
            string name = nameProp != null ? nameProp.stringValue : "";

            // Enable streaming on Balanced and High Fidelity; leave Performant off for low-end.
            bool shouldStream = name == "Balanced" || name == "High Fidelity";
            SerializedProperty streamProp = lvl.FindPropertyRelative("streamingMipmapsActive");
            if (streamProp != null && streamProp.boolValue != shouldStream)
            {
                streamProp.boolValue = shouldStream;
                changedLevels++;
                Debug.Log($"{LogPrefix} Quality '{name}' streamingMipmapsActive { !shouldStream }->{shouldStream}");
            }
        }

        if (changedLevels > 0)
        {
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(qualityAsset);
            Debug.Log($"{LogPrefix} QualitySettings streaming enabled on {changedLevels} tiers.");
        }
        else
        {
            Debug.Log($"{LogPrefix} QualitySettings streaming already correct.");
        }

        return changedLevels;
    }

    // ------------------------------------------------------------
    // Audit-only (read-only, no writes)
    // ------------------------------------------------------------

    private static void AuditOnlyInternal()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"{LogPrefix} Audit Only — reading importer settings (no writes).");

        // Textures
        string[] texGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/_Game" });
        int mipOff = 0, noStream = 0, oversizeUi = 0, noPlatformOverride = 0, fbmCount = 0;
        foreach (string guid in texGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.ToLowerInvariant().Contains(".fbm/"))
            {
                fbmCount++;
                continue;
            }
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null) continue;
            if (!imp.mipmapEnabled && !path.ToLowerInvariant().Contains("/ui/")) mipOff++;
            if (imp.mipmapEnabled && !imp.streamingMipmaps && imp.maxTextureSize >= 1024) noStream++;
            if (path.ToLowerInvariant().Contains("/art/ui/") && imp.maxTextureSize > 256) oversizeUi++;
            if (!imp.GetPlatformTextureSettings("Standalone").overridden && !path.ToLowerInvariant().Contains("/ui/")) noPlatformOverride++;
        }
        sb.AppendLine($"Textures: scanned {texGuids.Length} | mipOff(non-UI):{mipOff} | streamingOff(>=1024):{noStream} | UI oversize:{oversizeUi} | noStandaloneOverride:{noPlatformOverride} | fbm:{fbmCount}");

        // Models
        string[] modelGuids = AssetDatabase.FindAssets("t:Model", new[] { "Assets/_Game" });
        int blendOnProp = 0, noCompression = 0;
        foreach (string g in modelGuids)
        {
            string p = AssetDatabase.GUIDToAssetPath(g);
            var mi = AssetImporter.GetAtPath(p) as ModelImporter;
            if (mi == null) continue;
            string l = p.ToLowerInvariant();
            bool isProp = l.Contains("/ammo") || l.Contains("/weapons/") || l.Contains("sm_");
            if (isProp && mi.importBlendShapes) blendOnProp++;
            if (isProp && mi.meshCompression == ModelImporterMeshCompression.Off) noCompression++;
        }
        sb.AppendLine($"Models: scanned {modelGuids.Length} | prop blendShapes on:{blendOnProp} | prop noCompression:{noCompression}");

        // Quality
        var qa = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>("ProjectSettings/QualitySettings.asset");
        if (qa != null)
        {
            var so = new SerializedObject(qa);
            var arr = so.FindProperty("m_QualitySettings");
            for (int i = 0; i < arr.arraySize; i++)
            {
                var lvl = arr.GetArrayElementAtIndex(i);
                string n = lvl.FindPropertyRelative("name").stringValue;
                bool s = lvl.FindPropertyRelative("streamingMipmapsActive").boolValue;
                sb.AppendLine($"Quality '{n}' streaming:{s}");
            }
        }

        Debug.Log(sb.ToString());
    }
}
#endif
