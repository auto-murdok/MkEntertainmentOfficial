#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class RenderingScalabilitySetup
{
    [MenuItem("Tools/Rendering/Apply Scalability Settings")]
    public static void ApplyScalabilitySettings()
    {
        ConfigureUrpAssets();
        ConfigureGlobalSettings();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[RenderingScalabilitySetup] Unity 6 Rendering Scalability features applied successfully.");
    }

    private static void ConfigureUrpAssets()
    {
        string[] assetPaths = new[]
        {
            "Assets/Settings/URP-HighFidelity.asset",
            "Assets/Settings/URP-Balanced.asset",
            "Assets/Settings/URP-Performant.asset"
        };

        foreach (string path in assetPaths)
        {
            var urpAsset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
            if (urpAsset == null)
            {
                Debug.LogWarning($"[RenderingScalabilitySetup] Could not load URP asset at: {path}");
                continue;
            }

            SerializedObject so = new SerializedObject(urpAsset);

            // 1. GPU-Resident Drawer (InstancedDrawing = 1)
            SerializedProperty grdProp = so.FindProperty("m_GPUResidentDrawerMode");
            if (grdProp != null)
            {
                grdProp.intValue = (int)GPUResidentDrawerMode.InstancedDrawing;
            }

            // 2. GPU Occlusion Culling
            SerializedProperty occProp = so.FindProperty("m_GPUResidentDrawerEnableOcclusionCullingInCameras");
            if (occProp != null)
            {
                occProp.boolValue = true;
            }

            // 3. Spatial-Temporal Post-Processing (STP = 4)
            SerializedProperty upscalingProp = so.FindProperty("m_UpscalingFilter");
            if (upscalingProp != null)
            {
                upscalingProp.intValue = (int)UpscalingFilterSelection.STP;
            }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(urpAsset);
            Debug.Log($"[RenderingScalabilitySetup] Configured: {path} (GRD: InstancedDrawing, GPU Occlusion: True, Upscaling: STP)");
        }
    }

    private static void ConfigureGlobalSettings()
    {
        string globalSettingsPath = "Assets/UniversalRenderPipelineGlobalSettings.asset";
        var globalSettings = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(globalSettingsPath);
        if (globalSettings == null)
        {
            Debug.LogWarning($"[RenderingScalabilitySetup] Could not load global settings at: {globalSettingsPath}");
            return;
        }

        SerializedObject so = new SerializedObject(globalSettings);
        SerializedProperty renderGraphProp = so.FindProperty("m_EnableRenderGraph");
        if (renderGraphProp != null)
        {
            renderGraphProp.intValue = 1;
        }

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(globalSettings);
        Debug.Log("[RenderingScalabilitySetup] Enabled Render Graph in UniversalRenderPipelineGlobalSettings.");
    }
}
#endif
