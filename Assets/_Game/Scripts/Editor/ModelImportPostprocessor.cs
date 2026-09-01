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
        bool isBuildingKit = path.Contains("/buildingkit/");
        bool isProp = (path.Contains("/ammo") || path.Contains("/weapons/") || path.Contains("sm_") || path.Contains("gun")) && !isBuildingKit;

        if (isCharacter)
        {
            // Perf: Low for heroes (was Off) - still keeps blendShapes
            importer.importBlendShapes = true;
            importer.meshCompression = ModelImporterMeshCompression.Low;
        }
        else if (isProp)
        {
            // Perf: High for props (was Medium)
            importer.importBlendShapes = false;
            importer.meshCompression = ModelImporterMeshCompression.High;
            importer.addCollider = false;
        }
        else if (isBuildingKit)
        {
            // Master §1: Mesh Compression Off hero fidelity for kit walls etc. Keep Off per Master:34.
            // Only use High for tiny props if build size matters, not for hero kit.
            importer.importBlendShapes = false;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.addCollider = false;
            // Master normals/tangents Import, bakeAxis true, weld/strict handled in manual fix script
            if (importer.importNormals != ModelImporterNormals.Import) importer.importNormals = ModelImporterNormals.Import;
            if (importer.importTangents != ModelImporterTangents.Import) importer.importTangents = ModelImporterTangents.Import;
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
