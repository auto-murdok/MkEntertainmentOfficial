#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Gold-standard guardrail for texture imports (Unity 6 Manual: ScriptedImporters – OnPreprocessTexture).
/// Path-based caps + streaming + per-platform overrides. Deterministic, no AssetDatabase writes here
/// so it is safe under parallel import (ParallelImport.html).
/// One-time repairs for already-imported assets: Tools → Audit → Fix Import Settings.
/// </summary>
public class TextureImportPostprocessor : AssetPostprocessor
{
    private void OnPreprocessTexture()
    {
        var importer = assetImporter as TextureImporter;
        if (importer == null)
        {
            return;
        }

        string path = assetPath.ToLowerInvariant();

        // Leave generated/built-in textures alone.
        if (path.Contains("/editor/") || path.StartsWith("packages/"))
        {
            return;
        }

        // Preserve explicit sprite type – don't force Default on UI sprites.
        bool isSprite = importer.textureType == TextureImporterType.Sprite;

        // ---- Path-based rules (mirrors AssetAuditFix so reimport converges) ----
        if (path.Contains("/art/ui/") || path.Contains("_game/art/ui/"))
        {
            // Crosshair.png etc – tiny UI, no mips.
            importer.mipmapEnabled = false;
            importer.streamingMipmaps = false;
            importer.maxTextureSize = 256;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.compressionQuality = 50;
            importer.isReadable = false;
            // Keep Sprite type; don't force Default.
            if (!isSprite)
            {
                importer.textureType = TextureImporterType.Sprite;
            }
        }
        else if (path.Contains("smoke") || path.Contains("muzzle") || path.Contains("gunfx"))
        {
            // Transient VFX – 1024 cap, clamp, streaming.
            importer.maxTextureSize = 1024;
            importer.mipmapEnabled = true;
            importer.streamingMipmaps = true;
            importer.streamingMipmapsPriority = 0;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.isReadable = false;
        }
        else if (path.Contains("/characters/"))
        {
            // Perf: 1024 for heroes (was 2048 sharp) - user requested performant
            importer.maxTextureSize = 1024;
            importer.mipmapEnabled = true;
            importer.streamingMipmaps = true;
            importer.streamingMipmapsPriority = 0;
            importer.isReadable = false;
        }
        else if (path.Contains("/weapons/") || path.Contains("/prefabs/weapons/"))
        {
            // Perf: 1024 BC / 512 N/ORM (was 2048)
            bool isBCw = path.Contains("_bc.");
            importer.maxTextureSize = isBCw ? 1024 : 512;
            importer.mipmapEnabled = true;
            importer.streamingMipmaps = true;
            importer.streamingMipmapsPriority = 0;
            importer.isReadable = false;
        }
        else if (path.Contains("/environment/"))
        {
            importer.maxTextureSize = 1024;
            importer.mipmapEnabled = true;
            importer.streamingMipmaps = true;
            importer.isReadable = false;
        }
        else if (path.Contains("/buildingkit/"))
        {
            // BuildingKit perf: 1024 BC / 512 N/ORM/EM streaming (was 2048/1024 sharp) - user requested performant over sharp
            bool isBC = path.Contains("_bc.");
            bool isN = path.Contains("_n.");
            bool isORM = path.Contains("_orm.");
            bool isEM = path.Contains("_em.");
            importer.maxTextureSize = isBC ? 1024 : 512;
            
            importer.mipmapEnabled = true;
            importer.streamingMipmaps = true;
            importer.streamingMipmapsPriority = 0;
            importer.isReadable = false;
            importer.textureCompression = TextureImporterCompression.Compressed;
            if (isN)
            {
                importer.textureType = TextureImporterType.NormalMap;
                importer.sRGBTexture = false;
            }
            else if (isORM || isEM)
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = false;
            }
            else if (isBC)
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true;
            }
        }
        else if (path.Contains("/mansion_buildingkit/") || path.Contains("/mansion/"))
        {
            // Mansion_BuildingKit per Master §2: *_B.png = BC sRGB on 4096, *_N sRGB off, *_ORM/_RMA/_ORD sRGB off linear.
            // Master §2 hero fidelity 4096 (was perf 512/1024). Detect Mansion naming: _B. = BC, _N. = Normal, _ORM/_RMA/_ORD = packed.
            bool isMask = path.Contains("mask");
            bool isBC = path.Contains("basecolor") || path.Contains("base_color") || path.Contains("_bc.") || path.Contains("_b.") || path.EndsWith("_b.png") || path.Contains("_b_");
            bool isN = path.Contains("normal") || path.Contains("_n.") || path.Contains("_n_");
            bool isORM = isMask || path.Contains("orm") || path.Contains("_orm.") || path.Contains("_rma") || path.Contains("_ord") || path.Contains("_r.") || path.Contains("_m.") || path.Contains("_mm.");
            bool isAO = path.Contains("_ao") || (path.Contains("ao") && !path.Contains("basecolor"));
            bool isGray = path.Contains("grayscale");
            // Hero overrides for bathtub etc: keep 4096 per Master §2, not perf 512.
            bool isHeroMansion = path.Contains("bathtub") || path.Contains("bathtube") || path.Contains("bath");
            if (isN)
            {
                importer.textureType = TextureImporterType.NormalMap;
                importer.sRGBTexture = false;
                importer.maxTextureSize = 4096;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.filterMode = FilterMode.Bilinear;
            }
            else if (isORM || isAO || isGray)
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = false;
                importer.maxTextureSize = isHeroMansion ? 4096 : 2048;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.filterMode = FilterMode.Bilinear;
            }
            else // BaseColor / default
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true;
                importer.maxTextureSize = isHeroMansion ? 4096 : 2048;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.filterMode = FilterMode.Bilinear;
                // skip 0-byte placeholders (T_Base_*.png 0 bytes) - keep small
                if (path.Contains("t_base_")) importer.maxTextureSize = 256;
            }
            importer.mipmapEnabled = true;
            importer.streamingMipmaps = false; // Master fidelity off for hero; enable if memory-bound
            importer.streamingMipmapsPriority = 0;
            importer.isReadable = false;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.compressionQuality = 50;
            importer.anisoLevel = 4;
        }
        else
        {
            // Fallback – ensure sane defaults without stomping explicit artist settings.
            if (importer.maxTextureSize > 2048)
            {
                importer.maxTextureSize = 2048;
            }
            importer.isReadable = false;
        }

        // Texture shape / sRGB handled by preset – don't force here except aniso.
        // Aniso via importer.anisoLevel (not serialized directly here – set via textureSettings API on reimport).
        // Platform overrides are handled in OnPostprocess via TextureImporterPlatformSettings API
        // but that requires AssetDatabase write; keep guardrail minimal and let AssetAuditFix
        // enforce platform overrides headlessly for existing assets.
    }
}
#endif
