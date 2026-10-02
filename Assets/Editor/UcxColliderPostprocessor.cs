// UcxColliderPostprocessor.cs  (POC 3 draft — Editor-only, NOT compile-tested)
// ---------------------------------------------------------------------------
// Converts Unreal-style UCX_* collision meshes in imported FBX models into
// Unity MeshColliders and removes the UCX render nodes.
//
// Why: Unity has no collision naming convention. Imported raw, UCX hulls
// become extra renderable meshes (wasted draw calls + memory) and provide
// NO collision at all.
//
// Behavior (OnPostprocessModel):
//   For every child named "UCX_<TargetName>":
//     1. Find the sibling GameObject named "<TargetName>".
//     2. Add a MeshCollider using the UCX mesh. Convex when the hull is
//        <= 255 triangles (Unity's convex cap; every faced UCX hull in
//        this project measures 12-224 tris), otherwise non-convex with a
//        warning (non-convex = static/kinematic only).
//     3. Destroy the UCX node (its MeshFilter/MeshRenderer go with it).
//   Faceless UCX point-clouds (0 triangles — 18 exist in this project,
//   mostly weapons) are destroyed without a collider and logged: they
//   carry no hull data at all.
//
// Install: drop this file anywhere under an "Editor" folder in Assets,
// then reimport the affected FBX files (right-click -> Reimport).
// Test case: SM_ExternalWall_Baseflor_wall01.fbx should import with the
// UCX child gone and a convex MeshCollider (24 tris) on the wall object.
// ---------------------------------------------------------------------------
using UnityEditor;
using UnityEngine;

public class UcxColliderPostprocessor : AssetPostprocessor
{
    private const int ConvexTriangleCap = 255;

    private void OnPostprocessModel(GameObject root)
    {
        // Collect first; the hierarchy is mutated during conversion.
        var ucxNodes = new System.Collections.Generic.List<Transform>();
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name.StartsWith("UCX_"))
                ucxNodes.Add(t);
        }

        foreach (var ucx in ucxNodes)
        {
            string targetName = ucx.name.Substring("UCX_".Length);
            var filter = ucx.GetComponent<MeshFilter>();
            Mesh hull = filter != null ? filter.sharedMesh : null;
            int tris = hull != null ? (int)(hull.GetIndexCount(0) / 3) : 0;
            // Multi-submesh hulls: sum all submesh index counts.
            if (hull != null && hull.subMeshCount > 1)
            {
                tris = 0;
                for (int i = 0; i < hull.subMeshCount; i++)
                    tris += (int)(hull.GetIndexCount(i) / 3);
            }

            Transform target = FindSibling(root.transform, targetName);

            if (hull == null || tris == 0)
            {
                Debug.Log($"[UCX] {assetPath}: '{ucx.name}' has no faces " +
                          "(point cloud) — removed, no collider created.");
            }
            else if (target == null)
            {
                Debug.LogWarning($"[UCX] {assetPath}: no target object named " +
                                 $"'{targetName}' for '{ucx.name}' — removed without collider.");
            }
            else
            {
                var collider = target.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = hull;
                collider.convex = tris <= ConvexTriangleCap;
                if (!collider.convex)
                {
                    Debug.LogWarning($"[UCX] {assetPath}: hull for '{targetName}' has " +
                                       $"{tris} tris (> {ConvexTriangleCap}) — imported " +
                                       "non-convex; valid for static geometry only.");
                }
                else
                {
                    Debug.Log($"[UCX] {assetPath}: '{targetName}' got convex " +
                              $"MeshCollider ({tris} tris) from '{ucx.name}'.");
                }
            }

            Object.DestroyImmediate(ucx.gameObject);
        }
    }

    private static Transform FindSibling(Transform root, string name)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == name)
                return t;
        }
        return null;
    }
}
