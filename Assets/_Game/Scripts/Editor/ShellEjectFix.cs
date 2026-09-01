#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class ShellEjectFix
{
    [MenuItem("Tools/Audit/Fix Shell Eject")]
    public static void FixEject()
    {
        const string pistolPath = "Assets/_Game/Prefabs/Weapons/SM_Gun_Pistol/SM_Gun_Pistol.prefab";
        GameObject pistol = AssetDatabase.LoadAssetAtPath<GameObject>(pistolPath);
        if (pistol == null) { Debug.LogError("[ShellEjectFix] pistol not found"); return; }

        var we = pistol.GetComponent<WeaponEffects>();
        if (we == null) { Debug.LogError("[ShellEjectFix] WeaponEffects not found"); return; }

        var so = new SerializedObject(we);
        // Ejection velocity: local (-1,2,0.5) gives world up ~+1.5 when gun is at rest (tested via live eval)
        // Previous (2,1,-0.5) mapped to world (0.4,-2,1) downward -> shell hit ground instantly
        so.FindProperty("_ejectionVelocity").vector3Value = new Vector3(-1f, 2f, 0.5f);
        so.FindProperty("_ejectionTorque").vector3Value = new Vector3(4f, 7f, 3f) * 2f; // more spin at 10x
        so.FindProperty("_shellLife").floatValue = 4f; // longer visible
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(pistol);
        PrefabUtility.SavePrefabAsset(pistol);
        Debug.Log("[ShellEjectFix] ejectionVelocity -> (-1,2,0.5) torque*2 life 4s on " + pistolPath);

        // Also ensure shell prefab has continuous collision and slightly higher mass for 10x scale
        const string shellPath = "Assets/_Game/Prefabs/Weapons/SM_Gun_Pistol/GunFX_Pistol/Meshes/SM_GunShells_HandGun.prefab";
        GameObject shell = AssetDatabase.LoadAssetAtPath<GameObject>(shellPath);
        if (shell != null)
        {
            var rb = shell.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.mass = 0.05f; // was 0.01, 10x needs more inertia
                rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                EditorUtility.SetDirty(shell);
            }
            var col = shell.GetComponent<Collider>();
            if (col != null)
            {
                // BoxCollider already scaled via transform 10x, keep
            }
            PrefabUtility.SavePrefabAsset(shell);
            Debug.Log("[ShellEjectFix] shell Rigidbody mass 0.05 continuous interpolate");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }
    public static void FixEjectHeadless() => FixEject();
}
#endif
