#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Gold-standard mesh import guardrail (Unity 6 Manual: configure-mesh-compression, types-of-mesh-data-compression).
/// Hard-surface props get blendShapes off + Medium compression; skinned heroes keep blendShapes.
/// Safe under parallel import – only touches importer fields, no AssetDatabase writes.
/// </summary>
public class ModelImportPostprocessor : AssetPostprocessor
{
    private void OnPreprocessModel()
    {
        var importer = assetImporter as ModelImporter;
        if (importer == null)
        {
            return;
        }

        string path = assetPath.ToLowerInvariant();
        if (path.StartsWith("packages/"))
        {
            return;
        }

        // Common sane defaults for all meshes.
        importer.isReadable = false;
        importer.optimizeMeshPolygons = true;
        importer.optimizeMeshVertices = true;
        // Keep 16-bit index where possible (indexFormat: Auto = 0/1 decision at build).
        // importer.indexFormat is already Auto by default.

        bool isCharacter = path.Contains("/characters/") || path.Contains("femalemodelyellow") || path.Contains("zombiemodel");
        bool isProp = path.Contains("/ammo") || path.Contains("/weapons/") || path.Contains("sm_") || path.Contains("gun");

        if (isCharacter)
        {
            // Hero skinned meshes – blendShapes required for face/morph; no compression to preserve skinning quality.
            // Keep importBlendShapes = true, meshCompression = Off.
            importer.importBlendShapes = true;
            importer.meshCompression = ModelImporterMeshCompression.Off;
        }
        else if (isProp)
        {
            // Hard-surface – no blendShapes, medium compression saves 30-40% mesh data.
            importer.importBlendShapes = false;
            importer.meshCompression = ModelImporterMeshCompression.Medium;
            // Props should not add colliders by default.
            importer.addCollider = false;
        }
        else
        {
            // Environment/unknown – low compression, no blendShapes.
            importer.importBlendShapes = false;
            if (importer.meshCompression == ModelImporterMeshCompression.Off)
            {
                importer.meshCompression = ModelImporterMeshCompression.Low;
            }
        }
    }
}
#endif
