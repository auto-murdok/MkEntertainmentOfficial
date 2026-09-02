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
        ConfigureGraphicsSettings();
        ConfigureRendererData();
        ConfigureUrpAssets();
        ConfigureGlobalSettings();
        RemoveCompatibilityModeDefine();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[RenderingScalabilitySetup] Unity 6 Rendering Scalability features (BRG Keep All, Forward+, GRD, GPU Occlusion, STP, Render Graph) applied successfully.");
    }

    private static void ConfigureGraphicsSettings()
    {
        string graphicsSettingsPath = "ProjectSettings/GraphicsSettings.asset";
        var graphicsSettings = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(graphicsSettingsPath);
        if (graphicsSettings == null)
        {
            Debug.LogWarning($"[RenderingScalabilitySetup] Could not load GraphicsSettings at: {graphicsSettingsPath}");
            return;
        }

        SerializedObject so = new SerializedObject(graphicsSettings);
        SerializedProperty brgProp = so.FindProperty("m_BrgStripping");
        if (brgProp != null)
        {
            brgProp.intValue = 1; // 1 = Strip Unused (fast builds, only variants used in scenes are compiled)
        }

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(graphicsSettings);
        Debug.Log("[RenderingScalabilitySetup] Configured BatchRendererGroup Variants to 'Strip Unused' (m_BrgStripping = 1).");
    }

    private static void ConfigureRendererData()
    {
        string[] rendererPaths = new[]
        {
            "Assets/_Game/Settings/URP-HighFidelity-Renderer.asset",
            "Assets/_Game/Settings/URP-Balanced-Renderer.asset",
            "Assets/_Game/Settings/URP-Performant-Renderer.asset"
        };

        foreach (string path in rendererPaths)
        {
            var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
            if (rendererData == null)
            {
                Debug.LogWarning($"[RenderingScalabilitySetup] Could not load renderer data at: {path}");
                continue;
            }

            SerializedObject so = new SerializedObject(rendererData);
            SerializedProperty renderingModeProp = so.FindProperty("m_RenderingMode");
            if (renderingModeProp != null)
            {
                renderingModeProp.intValue = 2; // 2 = ForwardPlus
            }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(rendererData);
            Debug.Log($"[RenderingScalabilitySetup] Configured {path} to RenderingMode.ForwardPlus.");
        }
    }

    private static void RemoveCompatibilityModeDefine()
    {
        UnityEditor.Build.NamedBuildTarget[] targets = new[]
        {
            UnityEditor.Build.NamedBuildTarget.Standalone,
            UnityEditor.Build.NamedBuildTarget.Server
        };

        foreach (var target in targets)
        {
            string defines = PlayerSettings.GetScriptingDefineSymbols(target);
            if (defines.Contains("URP_COMPATIBILITY_MODE"))
            {
                var list = new System.Collections.Generic.List<string>(defines.Split(';'));
                list.RemoveAll(d => d == "URP_COMPATIBILITY_MODE");
                string newDefines = string.Join(";", list);
                PlayerSettings.SetScriptingDefineSymbols(target, newDefines);
                Debug.Log($"[RenderingScalabilitySetup] Removed URP_COMPATIBILITY_MODE define from {target.TargetName}.");
            }
        }
    }

    private static void ConfigureUrpAssets()
    {
        string[] assetPaths = new[]
        {
            "Assets/_Game/Settings/URP-HighFidelity.asset",
            "Assets/_Game/Settings/URP-Balanced.asset",
            "Assets/_Game/Settings/URP-Performant.asset"
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
                grdProp.intValue = 1; // 1 = InstancedDrawing
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
                upscalingProp.intValue = 4; // 4 = STP
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
