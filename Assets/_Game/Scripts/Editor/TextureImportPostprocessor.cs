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
            // Hero characters – 2048 with streaming, highest quality.
            importer.maxTextureSize = 2048;
            importer.mipmapEnabled = true;
            importer.streamingMipmaps = true;
            importer.streamingMipmapsPriority = 0;
            importer.isReadable = false;
            // Normal maps keep TextureType.NormalMap – don't override.
        }
        else if (path.Contains("/weapons/") || path.Contains("/prefabs/weapons/"))
        {
            // Props – 2048 default, streaming; large 4096 sources are already capped to 2048.
            // Small ammo shells (1024 source) also capped here – downsizing is free at import.
            importer.maxTextureSize = 2048;
            importer.mipmapEnabled = true;
            importer.streamingMipmaps = true;
            importer.streamingMipmapsPriority = 0;
            importer.isReadable = false;
            // TGA 24-bit sources: wrap repeat for tiling, clamp only for VFX handled above.
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
            // BuildingKit (Unreal Kit) - walls/floors/prop sheets: 2048 for BC, 1024 for N/ORM/EM, streaming
            // Learnings: source TGA 2048-4096, cap to avoid VAT, streaming for arena, clamp for props
            bool isBC = path.Contains("_bc.");
            importer.maxTextureSize = isBC ? 2048 : 1024;
            importer.mipmapEnabled = true;
            importer.streamingMipmaps = true;
            importer.streamingMipmapsPriority = 0;
            importer.isReadable = false;
            importer.textureCompression = TextureImporterCompression.Compressed;
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
