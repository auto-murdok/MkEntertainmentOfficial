#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class ShellScaleFix
{
    [MenuItem("Tools/Audit/Scale Shell 10x")]
    public static void ScaleShell()
    {
        const string prefabPath = "Assets/_Game/Prefabs/Weapons/SM_Gun_Pistol/GunFX_Pistol/Meshes/SM_GunShells_HandGun.prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            Debug.LogError($"[ShellScaleFix] Prefab not found {prefabPath}");
            return;
        }
        // Prefab root transform
        var root = prefab.transform;
        Vector3 oldScale = root.localScale;
        Vector3 newScale = new Vector3(10f, 10f, 10f);
        if (oldScale != newScale)
        {
            // Use SerializedObject to ensure prefab override is saved
            var so = new SerializedObject(prefab.transform);
            // Direct assignment on loaded prefab instance then save
            root.localScale = newScale;
            EditorUtility.SetDirty(prefab);
            PrefabUtility.SavePrefabAsset(prefab);
            Debug.Log($"[ShellScaleFix] Scaled {prefabPath} {oldScale} -> {newScale}");
        }
        else
        {
            Debug.Log($"[ShellScaleFix] Already 10x {prefabPath}");
        }

        // Also scale collider to match if needed — BoxCollider size stays logical, but visual is transform scale
        // No need to adjust Rigidbody mass.

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    public static void ScaleShellHeadless() => ScaleShell();
}
#endif
