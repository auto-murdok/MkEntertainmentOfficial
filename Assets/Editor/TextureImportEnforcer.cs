// TextureImportEnforcer.cs  (POC 4 — Editor-only, NOT compile-tested)
// ---------------------------------------------------------------------------
// Enforces the texture rules proven in the POC 4 audit (2026-10-02):
//
//  * T_AmmoCase_01_* (MTF_Environment): 4096x4096 sources for a hand-sized
//    prop. Standalone was capped 2048 with format Automatic (~22 MB VRAM
//    per set) and DefaultTexturePlatform was UNCAPPED 4096 (~89 MB).
//    Rule: max 1024 on every platform, explicit BC formats.
//    A/B render proof (4096 vs 1024 textures, prop filling the frame):
//    visually identical, RMSE 0.56% — see _blender/polish/poc4 tex proof.
//
//  * The 4096x4096 building-kit sources are already capped correctly on
//    Standalone (normals/albedo 2048 BC5/BC7, ORM 1024 BC7) — do NOT touch
//    their sizes. Only gap: mip streaming is OFF on every audited texture.
//    Rule: enable streaming mipmaps for textures under ImportedContent.
//
// Rules apply inside OnPreprocessTexture, only when a value differs, so
// imports stay stable (no reimport churn). Delete or relax a rule here if
// the art direction changes; this file is the single place the policy lives.
//
// Install: anywhere under an "Editor" folder in Assets. Existing textures
// pick the rules up on their next reimport (right-click -> Reimport).
// ---------------------------------------------------------------------------
using UnityEditor;
using UnityEngine;

public class TextureImportEnforcer : AssetPostprocessor
{
    private void OnPreprocessTexture()
    {
        var importer = (TextureImporter)assetImporter;
        string path = assetPath;

        bool isAmmoCase = path.Contains("MTF_Environment") &&
                          System.IO.Path.GetFileName(path).StartsWith("T_AmmoCase_01_");
        bool isImportedContent = path.StartsWith("Assets/ImportedContent/");

        // --- Rule 1: AmmoCase prop set -> 1024 max, explicit BC formats ---
        if (isAmmoCase)
        {
            bool isNormal = importer.textureType == TextureImporterType.NormalMap;

            var def = importer.GetDefaultPlatformTextureSettings();
            if (def.maxTextureSize > 1024)
            {
                def.maxTextureSize = 1024;
                importer.SetDefaultPlatformTextureSettings(def);
            }

            var standalone = importer.GetPlatformTextureSettings("Standalone");
            var wanted = isNormal ? TextureImporterFormat.BC5 : TextureImporterFormat.BC7;
            if (!standalone.overridden || standalone.maxTextureSize > 1024 ||
                standalone.format != wanted)
            {
                standalone.overridden = true;
                standalone.maxTextureSize = 1024;
                standalone.format = wanted;
                importer.SetPlatformTextureSettings(standalone);
            }
        }

        // --- Rule 2: mip streaming ON for imported content ---
        if (isImportedContent && !importer.streamingMipmaps)
        {
            importer.streamingMipmaps = true;
            importer.streamingMipmapsPriority = 0;
        }
    }
}
