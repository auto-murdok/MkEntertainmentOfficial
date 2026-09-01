#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class TracerSetup
{
    [MenuItem("Tools/Audit/Setup Bullet Tracer")]
    public static void SetupTracer()
    {
        CreateTracerMaterialAndPrefab();
        AssignTracerToWeapons();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[TracerSetup] Bullet tracer setup complete.");
    }

    /// <summary>Headless entry for batchmode.</summary>
    public static void SetupTracerHeadless()
    {
        SetupTracer();
    }

    private static void CreateTracerMaterialAndPrefab()
    {
        const string matPath = "Assets/_Game/Prefabs/Weapons/M_Tracer.mat";
        const string prefabPath = "Assets/_Game/Prefabs/Weapons/Tracer.prefab";

        // 1) Material — URP Unlit, bright yellow, additive-like
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlit == null) unlit = Shader.Find("Unlit/Color");
            mat = new Material(unlit);
            // Fallback if URP not found in batchmode
            if (mat.shader != null)
            {
                mat.SetColor("_BaseColor", new Color(1f, 0.92f, 0.35f, 1f));
                // Enable transparency if shader supports it
                if (mat.HasProperty("_Surface"))
                    mat.SetFloat("_Surface", 1f); // Transparent
            }
            AssetDatabase.CreateAsset(mat, matPath);
            Debug.Log($"[TracerSetup] Created material {matPath} ({mat.shader?.name})");
        }

        // 2) Prefab — GameObject with TracerVisual + TrailRenderer
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (existing != null)
        {
            Debug.Log($"[TracerSetup] Prefab already exists {prefabPath} — reconfiguring");
            // Reconfigure existing to ensure TrailRenderer is correct
            ConfigureTracerPrefab(existing, mat);
            EditorUtility.SetDirty(existing);
            return;
        }

        GameObject go = new GameObject("Tracer");
        var tracer = go.AddComponent<TracerVisual>();
        var trail = go.AddComponent<TrailRenderer>();

        // TrailRenderer gold standard for short-lived tracer (0.08s flight)
        trail.time = 0.12f;
        trail.minVertexDistance = 0.02f;
        trail.widthMultiplier = 0.025f;
        trail.numCornerVertices = 2;
        trail.numCapVertices = 2;
        trail.alignment = LineAlignment.View;
        trail.textureMode = LineTextureMode.Stretch;
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trail.receiveShadows = false;
        trail.material = mat;
        trail.emitting = true;
        trail.enabled = true;
        // Gradient: bright head fading to transparent tail
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(new Color(1f, 0.92f, 0.35f), 0f), new GradientColorKey(new Color(1f, 0.85f, 0.2f), 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        trail.colorGradient = g;
        trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0.1f);
        trail.autodestruct = false;

        // Wire TrailRenderer to TracerVisual via SerializedObject
        var so = new SerializedObject(tracer);
        so.FindProperty("_trail").objectReferenceValue = trail;
        so.FindProperty("_lifetime").floatValue = 0.12f;
        so.ApplyModifiedPropertiesWithoutUndo();

        // Save as prefab
        PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
        Object.DestroyImmediate(go);
        Debug.Log($"[TracerSetup] Created prefab {prefabPath}");
    }

    private static void ConfigureTracerPrefab(GameObject prefab, Material mat)
    {
        var tracer = prefab.GetComponent<TracerVisual>();
        var trail = prefab.GetComponent<TrailRenderer>();
        if (tracer == null) tracer = prefab.AddComponent<TracerVisual>();
        if (trail == null) trail = prefab.AddComponent<TrailRenderer>();
        trail.material = mat;
        trail.time = 0.12f;
        trail.widthMultiplier = 0.025f;
        var so = new SerializedObject(tracer);
        so.FindProperty("_trail").objectReferenceValue = trail;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(prefab);
    }

    private static void AssignTracerToWeapons()
    {
        const string prefabPath = "Assets/_Game/Prefabs/Weapons/Tracer.prefab";
        GameObject tracerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (tracerPrefab == null)
        {
            Debug.LogError($"[TracerSetup] Tracer prefab not found at {prefabPath}");
            return;
        }

        // Assign to WeaponDefinition assets
        string[] defGuids = AssetDatabase.FindAssets("t:WeaponDefinition", new[] { "Assets/_Game/Data/Weapons" });
        foreach (string guid in defGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var def = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(path);
            if (def == null) continue;
            var so = new SerializedObject(def);
            var prop = so.FindProperty("_tracerPrefab");
            if (prop != null && prop.objectReferenceValue != tracerPrefab)
            {
                prop.objectReferenceValue = tracerPrefab;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(def);
                Debug.Log($"[TracerSetup] Assigned tracer to {path}");
            }
        }

        // Assign to SM_Gun_Pistol prefab's HitscanWeapon
        const string pistolPath = "Assets/_Game/Prefabs/Weapons/SM_Gun_Pistol/SM_Gun_Pistol.prefab";
        GameObject pistol = AssetDatabase.LoadAssetAtPath<GameObject>(pistolPath);
        if (pistol != null)
        {
            var hitscan = pistol.GetComponentInChildren<HitscanWeapon>(true);
            if (hitscan != null)
            {
                var so = new SerializedObject(hitscan);
                var prop = so.FindProperty("_tracerPrefab");
                if (prop != null)
                {
                    prop.objectReferenceValue = tracerPrefab;
                    so.ApplyModifiedProperties();
                    Debug.Log($"[TracerSetup] Assigned tracer to HitscanWeapon on {pistolPath}");
                }
            }
            // Also ensure Weapon's definition points to tracer (already done via defs, but ensure pistol's Weapon._definition is set)
            var weapon = pistol.GetComponent<Weapon>();
            if (weapon != null)
            {
                // Weapon._definition already set; no direct tracer ref needed there
            }
            PrefabUtility.SavePrefabAsset(pistol);
        }

        // Also patch Projectile.prefab if someone still uses legacy path — not needed for hitscan but keep consistent
    }
}
#endif
