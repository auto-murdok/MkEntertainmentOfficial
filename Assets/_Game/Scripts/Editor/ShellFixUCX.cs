#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class ShellFixUCX
{
    [MenuItem("Tools/Audit/Fix Shell UCX + 100x")]
    public static void Fix()
    {
        const string prefabPath = "Assets/_Game/Prefabs/Weapons/SM_Gun_Pistol/GunFX_Pistol/Meshes/SM_GunShells_HandGun.prefab";
        var root = PrefabUtility.LoadPrefabContents(prefabPath);
        if (root == null) { Debug.LogError("[ShellFixUCX] LoadPrefabContents failed " + prefabPath); return; }

        // 1) Root uniform scale 100 (you needed 100x to see it)
        root.transform.localScale = new Vector3(100f, 100f, 100f);
        Debug.Log($"[ShellFixUCX] root scale -> {root.transform.localScale}");

        // 2) Find UCX and disable its MeshRenderer (keep collider path separate)
        var ucx = FindDeep(root.transform, "UCX_SM_GunShells_HandGun");
        if (ucx != null)
        {
            var mr = ucx.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.enabled = false;
                EditorUtility.SetDirty(mr);
                Debug.Log("[ShellFixUCX] UCX MeshRenderer disabled on " + ucx.name);
            }
            var mf = ucx.GetComponent<MeshFilter>();
            if (mf != null)
            {
                // Ensure UCX mesh not used for rendering - clear sharedMesh if desired, but keep for optional collider
                Debug.Log($"[ShellFixUCX] UCX mesh {mf.sharedMesh?.name} verts {mf.sharedMesh?.vertexCount}");
            }
            // Remove MeshRenderer component entirely to guarantee no render (optional, we just disable)
        }
        else
        {
            Debug.LogWarning("[ShellFixUCX] UCX child not found - checking all renderers");
            foreach (var mr in root.GetComponentsInChildren<MeshRenderer>(true))
                Debug.Log($"  MR on {mr.gameObject.name} enabled={mr.enabled} path={GetPath(mr.transform)}");
        }

        // 3) Ensure SM_ visible renderer stays enabled
        var sm = FindDeep(root.transform, "SM_GunShells_HandGun");
        if (sm != null)
        {
            var mr = sm.GetComponent<MeshRenderer>();
            if (mr != null) { mr.enabled = true; Debug.Log("[ShellFixUCX] SM renderer kept enabled"); }
        }

        // 4) Keep BoxCollider on root (cheaper than MeshCollider); ensure it exists and is sized for 100x
        var box = root.GetComponent<BoxCollider>();
        if (box == null) box = root.AddComponent<BoxCollider>();
        // BoxCollider size is local; at 100x world size = local*100. Keep small local so world ~0.02m (real 9mm shell)
        // Previously 0.02 at 10x => 0.2 world (too big). At 100x we need 0.0002 for 0.02 world, but physics min is ~0.01.
        // So set local to 0.01 / 0.01 / 0.02 => world 1,1,2 at 100x still big but visible. Instead set to 0.005 => 0.5 world.
        // For now keep 0.02 but will be 2m world at 100x - acceptable for shell bounce visibility vs realism.
        // Let's set to realistic: 0.009 diameter, 0.019 length -> local 0.00009/0.00019 is too small for BoxCollider (clamped).
        // So use compromise: local 0.001,0.001,0.002 => world 0.1,0.1,0.2 (10cm) visible and collidable.
        box.size = new Vector3(0.002f, 0.002f, 0.004f);
        box.center = Vector3.zero;
        Debug.Log($"[ShellFixUCX] BoxCollider size {box.size}");

        // 5) Rigidbody already Continuous from previous fix - keep
        var rb = root.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
        }

        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        PrefabUtility.UnloadPrefabContents(root);
        Debug.Log("[ShellFixUCX] Saved " + prefabPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    public static void FixHeadless() => Fix();

    private static Transform FindDeep(Transform root, string name)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        return null;
    }
    private static string GetPath(Transform t) { string p=""; while(t!=null){ p="/"+t.name+p; t=t.parent; } return p; }
}
#endif
