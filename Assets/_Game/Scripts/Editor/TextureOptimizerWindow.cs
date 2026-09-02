#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    public class TextureOptimizerWindow : EditorWindow
    {
        private Vector2 _scrollPos;
        private int _totalTextures;
        private long _totalDiskBytes;
        private long _totalVramBytes;
        private int _count4K;
        private int _count2K;
        private int _count1K;
        private int _count512OrLess;
        private int _countTga;
        private int _countNoPlatformOverride;
        private bool _hasScanned = false;

        private bool _capBuildingKitTo2K = true;
        private bool _capPropsAndPickupsTo1K = true;
        private bool _capAmmoTo512 = true;
        private bool _capOrmTo1K = true;
        private bool _forceBc7Bc5Compression = true;
        private bool _ensureMipmapsEnabled = true;

        [MenuItem("Tools/Performance/Texture Optimizer")]
        public static void ShowWindow()
        {
            var window = GetWindow<TextureOptimizerWindow>("Texture Optimizer");
            window.minSize = new Vector2(500, 600);
            window.ScanTextures();
        }

        [MenuItem("Tools/Performance/Apply Recommended Texture Budgets")]
        public static void ApplyRecommendedBudgetsMenu()
        {
            if (EditorUtility.DisplayDialog("Apply Texture Budgets", 
                "This will optimize texture import settings across the project (clamp 4K to 2K/1K, apply Standalone BC7/BC5 compression, enable mipmaps, scale ORMs).\n\nProceed?", "Yes", "Cancel"))
            {
                ApplyOptimization(true, true, true, true, true, true);
            }
        }

        private void OnEnable()
        {
            ScanTextures();
        }

        private void OnGUI()
        {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            EditorGUILayout.LabelField("Texture Performance Optimizer", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Audits project textures against commercial PC/Console performance standards (URP / Unity 6) and optimizes MaxSize, Compression (BC7/BC5), and Mipmaps.", MessageType.Info);

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Audit Summary", EditorStyles.boldLabel);

            if (_hasScanned)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField($"Total Texture Assets: {_totalTextures}");
                EditorGUILayout.LabelField($"Total Source Disk Size: {_totalDiskBytes / (1024.0 * 1024.0):F1} MB ({(float)_totalDiskBytes / (1024 * 1024 * 1024):F2} GB)");
                EditorGUILayout.LabelField($"Estimated Active VRAM: {_totalVramBytes / (1024.0 * 1024.0):F1} MB ({(float)_totalVramBytes / (1024 * 1024 * 1024):F2} GB)");
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField($"• 4K Textures (4096): {_count4K}", _count4K > 0 ? EditorStyles.boldLabel : EditorStyles.label);
                EditorGUILayout.LabelField($"• 2K Textures (2048): {_count2K}");
                EditorGUILayout.LabelField($"• 1K Textures (1024): {_count1K}");
                EditorGUILayout.LabelField($"• ≤512 Textures: {_count512OrLess}");
                EditorGUILayout.LabelField($"• Raw .TGA Files: {_countTga}");
                EditorGUILayout.LabelField($"• Missing Standalone Override: {_countNoPlatformOverride}");
                EditorGUILayout.EndVertical();
            }

            if (GUILayout.Button("🔄 Rescan Textures", GUILayout.Height(28)))
            {
                ScanTextures();
            }

            EditorGUILayout.Space(15);
            EditorGUILayout.LabelField("Optimization Settings", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            _capBuildingKitTo2K = EditorGUILayout.ToggleLeft("Clamp Modular Architecture / Kits to 2048 (Down from 4096)", _capBuildingKitTo2K);
            _capPropsAndPickupsTo1K = EditorGUILayout.ToggleLeft("Clamp Props & Shelves to 1024", _capPropsAndPickupsTo1K);
            _capAmmoTo512 = EditorGUILayout.ToggleLeft("Clamp Ammo Pickups & Small Items to 512", _capAmmoTo512);
            _capOrmTo1K = EditorGUILayout.ToggleLeft("Scale ORM / Packed Masks to max 1024 (Half-Res Rule)", _capOrmTo1K);
            _forceBc7Bc5Compression = EditorGUILayout.ToggleLeft("Enforce Standalone BC7 (Albedo/ORM) & BC5 (Normals)", _forceBc7Bc5Compression);
            _ensureMipmapsEnabled = EditorGUILayout.ToggleLeft("Ensure Mipmaps Enabled on 3D Textures", _ensureMipmapsEnabled);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(15);

            GUI.backgroundColor = new Color(0.2f, 0.8f, 0.3f);
            if (GUILayout.Button("⚡ Apply Texture Optimization", GUILayout.Height(36)))
            {
                ApplyOptimization(_capBuildingKitTo2K, _capPropsAndPickupsTo1K, _capAmmoTo512, _capOrmTo1K, _forceBc7Bc5Compression, _ensureMipmapsEnabled);
                ScanTextures();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(20);
            EditorGUILayout.LabelField("Advanced Source File Tools", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Raw TGA to Compressed PNG Conversion", EditorStyles.miniBoldLabel);
            EditorGUILayout.HelpBox("Converts uncompressed 48MB/64MB TGAs to lossless PNGs and updates referencing materials. Saves several gigabytes of repository storage.", MessageType.None);
            if (GUILayout.Button("Convert Uncompressed TGAs to PNGs"))
            {
                ConvertTgasBatch(100);
                ScanTextures();
            }
            if (GUILayout.Button("Downsample Oversized Source PNGs on Disk (4K -> 2K/1K)"))
            {
                DownsampleOversizedPngsBatch(100);
                ScanTextures();
            }
            if (GUILayout.Button("Purge Unreferenced Raw BuildingKit Source Textures"))
            {
                int purged = PurgeUnreferencedBuildingKitRawTextures();
                Debug.Log($"[TextureOptimizer] Purged {purged} unreferenced BuildingKit source textures.");
                ScanTextures();
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndScrollView();
        }

        public void ScanTextures()
        {
            _totalTextures = 0;
            _totalDiskBytes = 0;
            _totalVramBytes = 0;
            _count4K = 0;
            _count2K = 0;
            _count1K = 0;
            _count512OrLess = 0;
            _countTga = 0;
            _countNoPlatformOverride = 0;

            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets" });
            _totalTextures = guids.Length;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path)) continue;

                if (path.EndsWith(".tga", StringComparison.OrdinalIgnoreCase))
                    _countTga++;

                var fi = new FileInfo(path);
                if (fi.Exists) _totalDiskBytes += fi.Length;

                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (tex != null)
                {
                    _totalVramBytes += UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(tex);
                }

                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer != null)
                {
                    if (importer.maxTextureSize >= 4096) _count4K++;
                    else if (importer.maxTextureSize >= 2048) _count2K++;
                    else if (importer.maxTextureSize >= 1024) _count1K++;
                    else _count512OrLess++;

                    var standalone = importer.GetPlatformTextureSettings("Standalone");
                    if (standalone == null || !standalone.overridden)
                        _countNoPlatformOverride++;
                }
            }

            _hasScanned = true;
            Repaint();
        }

        public static int ApplyOptimizationBatch(int startIndex, int batchSize, bool capBuildingKit2K, bool capProps1K, bool capAmmo512, bool capOrm1K, bool forceBc7Bc5, bool ensureMipmaps)
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets" });
            int endIndex = Math.Min(startIndex + batchSize, guids.Length);
            int modifiedCount = 0;

            for (int i = startIndex; i < endIndex; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrEmpty(path)) continue;

                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;

                bool changed = false;
                string lowerPath = path.ToLowerInvariant();
                string fileName = Path.GetFileNameWithoutExtension(lowerPath);

                int targetMaxSize = importer.maxTextureSize;

                // UI / Icons
                if (lowerPath.Contains("/ui/") || lowerPath.Contains("sprite") || importer.textureType == TextureImporterType.Sprite)
                {
                    targetMaxSize = Math.Min(targetMaxSize, 1024);
                }
                // Ammo & Small item pickups
                else if (capAmmo512 && (lowerPath.Contains("ammo") || lowerPath.Contains("cartridge") || lowerPath.Contains("bullet") || fileName.Contains("ammo")))
                {
                    targetMaxSize = Math.Min(targetMaxSize, 512);
                }
                // Small props & furniture
                else if (capProps1K && (lowerPath.Contains("shelf") || lowerPath.Contains("crate") || lowerPath.Contains("cabinet") || lowerPath.Contains("lamp") || lowerPath.Contains("door") || lowerPath.Contains("partition") || lowerPath.Contains("target")))
                {
                    targetMaxSize = Math.Min(targetMaxSize, 1024);
                }
                // ORM / Masks
                else if (capOrm1K && (fileName.EndsWith("_orm") || fileName.EndsWith("_orm_pack") || fileName.EndsWith("_m") || fileName.EndsWith("_mask") || fileName.EndsWith("_roughness") || fileName.EndsWith("_metallic")))
                {
                    targetMaxSize = Math.Min(targetMaxSize, 1024);
                }
                // Modular building kit & architecture
                else if (capBuildingKit2K && (lowerPath.Contains("building_kit") || lowerPath.Contains("buildingkit") || lowerPath.Contains("mansion") || lowerPath.Contains("environment") || lowerPath.Contains("walls") || lowerPath.Contains("floors")))
                {
                    targetMaxSize = Math.Min(targetMaxSize, 2048);
                }
                // General safety cap: no texture needs to exceed 2048 unless explicitly configured hero
                else if (targetMaxSize > 2048)
                {
                    targetMaxSize = 2048;
                }

                if (importer.maxTextureSize != targetMaxSize)
                {
                    importer.maxTextureSize = targetMaxSize;
                    changed = true;
                }

                // Mipmaps
                if (ensureMipmaps && importer.textureType != TextureImporterType.Sprite && !lowerPath.Contains("/ui/"))
                {
                    if (!importer.mipmapEnabled)
                    {
                        importer.mipmapEnabled = true;
                        changed = true;
                    }
                }

                // Ensure sRGB is false for Normal and Mask maps
                if (importer.textureType == TextureImporterType.NormalMap)
                {
                    if (importer.sRGBTexture)
                    {
                        importer.sRGBTexture = false;
                        changed = true;
                    }
                }
                else if (fileName.EndsWith("_orm") || fileName.EndsWith("_orm_pack") || fileName.EndsWith("_mask") || fileName.EndsWith("_metallic") || fileName.EndsWith("_roughness"))
                {
                    if (importer.sRGBTexture)
                    {
                        importer.sRGBTexture = false;
                        changed = true;
                    }
                }

                // Standalone Platform Settings (BC7 / BC5)
                if (forceBc7Bc5)
                {
                    var standalone = importer.GetPlatformTextureSettings("Standalone");
                    if (standalone == null)
                    {
                        standalone = new TextureImporterPlatformSettings
                        {
                            name = "Standalone",
                            overridden = true
                        };
                    }

                    bool platformChanged = false;
                    if (!standalone.overridden)
                    {
                        standalone.overridden = true;
                        platformChanged = true;
                    }

                    if (standalone.maxTextureSize != targetMaxSize)
                    {
                        standalone.maxTextureSize = targetMaxSize;
                        platformChanged = true;
                    }

                    TextureImporterFormat desiredFormat;
                    if (importer.textureType == TextureImporterType.NormalMap)
                    {
                        desiredFormat = TextureImporterFormat.BC5;
                    }
                    else if (fileName.EndsWith("_orm") || fileName.EndsWith("_orm_pack") || fileName.EndsWith("_mask"))
                    {
                        desiredFormat = TextureImporterFormat.BC7;
                    }
                    else
                    {
                        desiredFormat = TextureImporterFormat.BC7;
                    }

                    if (standalone.format != desiredFormat)
                    {
                        standalone.format = desiredFormat;
                        platformChanged = true;
                    }

                    if (standalone.textureCompression != TextureImporterCompression.Compressed)
                    {
                        standalone.textureCompression = TextureImporterCompression.Compressed;
                        platformChanged = true;
                    }

                    if (platformChanged)
                    {
                        importer.SetPlatformTextureSettings(standalone);
                        changed = true;
                    }
                }

                if (changed)
                {
                    importer.SaveAndReimport();
                    modifiedCount++;
                }
            }

            return modifiedCount;
        }

        public static void ApplyOptimization(bool capBuildingKit2K, bool capProps1K, bool capAmmo512, bool capOrm1K, bool forceBc7Bc5, bool ensureMipmaps)
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets" });
            int modifiedCount = 0;
            int batchSize = 25;

            for (int i = 0; i < guids.Length; i += batchSize)
            {
                EditorUtility.DisplayProgressBar("Optimizing Textures", $"Processing {i}/{guids.Length} textures...", (float)i / guids.Length);
                modifiedCount += ApplyOptimizationBatch(i, batchSize, capBuildingKit2K, capProps1K, capAmmo512, capOrm1K, forceBc7Bc5, ensureMipmaps);
            }

            EditorUtility.ClearProgressBar();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[TextureOptimizer] Optimization complete! Updated import settings for {modifiedCount} textures.");
        }

        public static byte[] TextureToPngBytes(Texture2D source, int targetWidth, int targetHeight, bool isLinear)
        {
            var renderTex = RenderTexture.GetTemporary(
                targetWidth, targetHeight, 0,
                RenderTextureFormat.ARGB32,
                isLinear ? RenderTextureReadWrite.Linear : RenderTextureReadWrite.sRGB);

            Graphics.Blit(source, renderTex);
            var prev = RenderTexture.active;
            RenderTexture.active = renderTex;

            var texCopy = new Texture2D(targetWidth, targetHeight, TextureFormat.RGBA32, false, isLinear);
            texCopy.ReadPixels(new Rect(0, 0, targetWidth, targetHeight), 0, 0);
            texCopy.Apply();

            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(renderTex);

            byte[] pngBytes = texCopy.EncodeToPNG();
            DestroyImmediate(texCopy);
            return pngBytes;
        }

        public static int ConvertTgasBatch(int count)
        {
            string[] tgaGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets" });
            var tgaPaths = new List<string>();

            foreach (string guid in tgaGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(".tga", StringComparison.OrdinalIgnoreCase))
                    tgaPaths.Add(path);
            }

            if (tgaPaths.Count == 0) return 0;

            string[] matGuids = AssetDatabase.FindAssets("t:Material", new[] { "Assets" });
            var materials = new List<Material>();
            foreach (var mg in matGuids)
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(mg));
                if (mat != null) materials.Add(mat);
            }

            int processed = 0;
            int limit = Math.Min(count, tgaPaths.Count);

            for (int i = 0; i < limit; i++)
            {
                string tgaPath = tgaPaths[i];
                string pngPath = Path.ChangeExtension(tgaPath, ".png");

                var oldTex = AssetDatabase.LoadAssetAtPath<Texture2D>(tgaPath);
                if (oldTex == null) continue;

                var importer = AssetImporter.GetAtPath(tgaPath) as TextureImporter;
                if (importer == null) continue;

                var origType = importer.textureType;
                bool origSrgb = importer.sRGBTexture;
                int origMaxSize = importer.maxTextureSize;
                int w = Math.Min(oldTex.width, origMaxSize);
                int h = Math.Min(oldTex.height, origMaxSize);
                bool isLinear = !origSrgb || origType == TextureImporterType.NormalMap;

                byte[] pngBytes = TextureToPngBytes(oldTex, w, h, isLinear);
                if (pngBytes != null && pngBytes.Length > 0)
                {
                    File.WriteAllBytes(pngPath, pngBytes);
                    AssetDatabase.ImportAsset(pngPath, ImportAssetOptions.ForceUpdate);

                    var pngImporter = AssetImporter.GetAtPath(pngPath) as TextureImporter;
                    if (pngImporter != null)
                    {
                        pngImporter.textureType = origType;
                        pngImporter.sRGBTexture = origSrgb;
                        pngImporter.maxTextureSize = origMaxSize;
                        pngImporter.mipmapEnabled = true;
                        pngImporter.isReadable = false;

                        var standalone = new TextureImporterPlatformSettings
                        {
                            name = "Standalone",
                            overridden = true,
                            maxTextureSize = origMaxSize,
                            format = origType == TextureImporterType.NormalMap ? TextureImporterFormat.BC5 : TextureImporterFormat.BC7,
                            textureCompression = TextureImporterCompression.Compressed
                        };
                        pngImporter.SetPlatformTextureSettings(standalone);
                        pngImporter.SaveAndReimport();
                    }

                    var newPngTex = AssetDatabase.LoadAssetAtPath<Texture2D>(pngPath);
                    RebindMaterials(oldTex, newPngTex, materials);

                    AssetDatabase.DeleteAsset(tgaPath);
                    processed++;
                }
            }

            AssetDatabase.SaveAssets();
            return processed;
        }

        public static int DownsampleOversizedPngsBatch(int count)
        {
            string[] pngGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets" });
            int processed = 0;

            foreach (string guid in pngGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) continue;

                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;

                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (tex == null) continue;

                int targetMax = importer.maxTextureSize;
                if (tex.width > targetMax || tex.height > targetMax)
                {
                    int newW = Math.Min(tex.width, targetMax);
                    int newH = Math.Min(tex.height, targetMax);
                    bool isLinear = !importer.sRGBTexture || importer.textureType == TextureImporterType.NormalMap;

                    byte[] downsampledBytes = TextureToPngBytes(tex, newW, newH, isLinear);
                    if (downsampledBytes != null && downsampledBytes.Length > 0)
                    {
                        File.WriteAllBytes(path, downsampledBytes);
                        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                        processed++;
                        if (processed >= count) break;
                    }
                }
            }

            AssetDatabase.SaveAssets();
            return processed;
        }

        public static int PurgeUnreferencedBuildingKitRawTextures()
        {
            // Textures in ImportedContent/Building_kit/Textures/ that are NOT referenced by any material
            string[] matGuids = AssetDatabase.FindAssets("t:Material", new[] { "Assets" });
            var referenced = new HashSet<string>();

            foreach (var mg in matGuids)
            {
                string matPath = AssetDatabase.GUIDToAssetPath(mg);
                var deps = AssetDatabase.GetDependencies(matPath, false);
                foreach (var d in deps) referenced.Add(d);
            }

            string[] texGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/ImportedContent/Building_kit/Textures" });
            int deletedCount = 0;

            foreach (var tg in texGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(tg);
                if (!referenced.Contains(path))
                {
                    AssetDatabase.DeleteAsset(path);
                    deletedCount++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return deletedCount;
        }

        private static void RebindMaterials(Texture2D oldTex, Texture2D newTex, List<Material> materials)
        {
            foreach (var mat in materials)
            {
                if (mat == null) continue;
                var shader = mat.shader;
                if (shader == null) continue;

                int count = shader.GetPropertyCount();
                bool matChanged = false;
                for (int i = 0; i < count; i++)
                {
                    if (shader.GetPropertyType(i) == UnityEngine.Rendering.ShaderPropertyType.Texture)
                    {
                        string propName = shader.GetPropertyName(i);
                        if (mat.GetTexture(propName) == oldTex)
                        {
                            mat.SetTexture(propName, newTex);
                            matChanged = true;
                        }
                    }
                }

                if (matChanged)
                {
                    EditorUtility.SetDirty(mat);
                }
            }
        }
    }
}
#endif
