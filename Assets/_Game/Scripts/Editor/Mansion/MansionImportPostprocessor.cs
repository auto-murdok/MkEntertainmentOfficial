using System.IO;
using UnityEditor;
using UnityEngine;

// Import discipline for the Mansion environment source assets
// (Assets/_Game/Environments/Mansion/Source/...). The gold-standard rule
// enforced here: import settings are decided ONCE, centrally, per folder
// class — never per-file by hand — so the prefab factory and scene builder
// can rely on uniform source data.
//
// Folder classes:
// - Source/Kit, Source/Props: FBX meshes. Kit FBX are cm-authored, prop
//   FBX are m-authored; BOTH carry FBX unit metadata and Unity's file-scale
//   conversion normalises them to metres — do NOT stack a manual
//   globalScale on top (that was the 100x-giants bug in the Blender
//   rebuild). Settings: no animation/cameras/lights (environment
//   geometry), Read/Write OFF (meshes are consumed through wrapper
//   prefabs with proxy colliders; the prefab factory owns the exception
//   list for any mesh that genuinely needs a MeshCollider — on Unity
//   6.6+, missing Read/Write on a MeshCollider mesh fails the build, so
//   exceptions must be explicit), medium mesh compression.
// - Source/KitTextures, Source/PropTextures: PNG textures in the UE
//   naming convention — *_B / *_BaseColor = base colour (sRGB),
//   *_N / *_Normal = normal map, *_ORM / T_AO* / *_Grayscale / *Grunge* =
//   linear data. Mipmaps + Mipmap Streaming on, max size 2048 (sources
//   stay full-res in the repo; the cap governs build size and can be
//   re-tuned without touching sources). Kit textures tile (Repeat);
//   prop textures are object-space (Clamp).
public class MansionImportPostprocessor : AssetPostprocessor
{
    private const string MansionSourceRoot = "Assets/_Game/Environments/Mansion/Source/";
    private const int MaxTextureSize = 2048;

    private void OnPreprocessModel()
    {
        if (!assetPath.StartsWith(MansionSourceRoot))
        {
            return;
        }

        ModelImporter importer = (ModelImporter)assetImporter;
        importer.importAnimation = false;
        importer.importCameras = false;
        importer.importLights = false;
        importer.isReadable = false;
        importer.meshCompression = ModelImporterMeshCompression.Medium;
    }

    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(MansionSourceRoot))
        {
            return;
        }

        TextureImporter importer = (TextureImporter)assetImporter;
        string fileName = Path.GetFileNameWithoutExtension(assetPath);
        bool isKitTexture = assetPath.Contains("/KitTextures/");

        importer.mipmapEnabled = true;
        importer.streamingMipmaps = true;
        importer.maxTextureSize = MaxTextureSize;
        importer.wrapMode = isKitTexture ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;

        if (fileName.EndsWith("_N") || fileName.Contains("_Normal"))
        {
            importer.textureType = TextureImporterType.NormalMap;
        }
        else if (fileName.EndsWith("_ORM")
                 || fileName.StartsWith("T_AO")
                 || fileName.Contains("_Grayscale")
                 || fileName.Contains("Grunge"))
        {
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = false;
        }
        else
        {
            // *_B, *_BaseColor and anything unclassified is treated as
            // colour. The material consolidation pass assigns maps by
            // name; a wrong linear flag on a colour map is the more
            // damaging error, so colour is the safe default.
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
        }
    }
}
