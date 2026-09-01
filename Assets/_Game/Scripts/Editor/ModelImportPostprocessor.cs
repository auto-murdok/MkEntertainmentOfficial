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
        bool isBuildingKit = path.Contains("/buildingkit/");

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
        else if (isBuildingKit)
        {
            // BuildingKit modular pieces - hard-surface, no skinning, keep Low compression for walls/floors (silhouette matters)
            importer.importBlendShapes = false;
            importer.meshCompression = ModelImporterMeshCompression.Low;
            importer.addCollider = false; // UCX_* handled via custom collider setup, not auto
            // Learnings: globalScale 0.01 (1cm->1m) is correct for Unreal kit, don't override to 1; useFileScale true
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

    private void OnPostprocessModel(GameObject g)
    {
        // Learnings: UCX_ meshes are collision-only (Unreal) - disable MeshRenderer, ensure MeshCollider
        // Applies to BuildingKit and shell - keep SM_ visible, UCX_ hidden
        string path = assetPath.ToLowerInvariant();
        if (!path.Contains("/buildingkit/") && !path.Contains("gunshells")) return;

        foreach (Transform t in g.GetComponentsInChildren<Transform>(true))
        {
            if (!t.name.StartsWith("UCX_")) continue;
            var mr = t.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.enabled = false;
                // Also ensure no shadow casting
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            var mf = t.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                var mc = t.GetComponent<MeshCollider>();
                if (mc == null)
                {
                    mc = t.gameObject.AddComponent<MeshCollider>();
                    mc.sharedMesh = mf.sharedMesh;
                    mc.convex = true;
                    mc.cookingOptions = MeshColliderCookingOptions.None;
                }
            }
        }
    }
}
#endif
