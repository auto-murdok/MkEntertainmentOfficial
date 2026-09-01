#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class BuildingKitMaterialFix
{
    [MenuItem("Tools/Audit/Fix BuildingKit Materials")]
    public static void FixMaterials()
    {
        var texGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/_Game/Art/BuildingKit/Textures" });
        var texByKey = new Dictionary<string, Dictionary<string, string>>();
        foreach (var g in texGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(g);
            string file = System.IO.Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
            if (!file.StartsWith("t_")) continue;
            string core = file.Substring(2);
            string suffix = null; string baseKey = null;
            if (core.EndsWith("_bc")) { suffix = "bc"; baseKey = core.Substring(0, core.Length - 3); }
            else if (core.EndsWith("_n")) { suffix = "n"; baseKey = core.Substring(0, core.Length - 2); }
            else if (core.EndsWith("_orm")) { suffix = "orm"; baseKey = core.Substring(0, core.Length - 4); }
            else if (core.EndsWith("_em")) { suffix = "em"; baseKey = core.Substring(0, core.Length - 3); }
            else continue;
            if (!texByKey.TryGetValue(baseKey, out var dict)) { dict = new Dictionary<string, string>(); texByKey[baseKey] = dict; }
            dict[suffix] = path;
        }

        var matGuids = AssetDatabase.FindAssets("t:Material", new[] { "Assets/_Game/Art/BuildingKit/Materials" });
        int fixedMats = 0;
        foreach (var g in matGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(g);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            string raw = mat.name.ToLowerInvariant();
            if (raw.StartsWith("mi_")) raw = raw.Substring(3);
            string baseKey = null;
            if (texByKey.ContainsKey(raw)) baseKey = raw;
            else
            {
                foreach (var k in texByKey.Keys)
                {
                    if (raw.Contains(k) || k.Contains(raw)) { baseKey = k; break; }
                }
            }
            if (baseKey == null) continue;
            var dict = texByKey[baseKey];
            bool dirty = false;
            if (dict.TryGetValue("bc", out var bcPath))
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture>(bcPath);
                if (tex != null && mat.GetTexture("_BaseMap") != tex) { mat.SetTexture("_BaseMap", tex); dirty = true; }
            }
            if (dict.TryGetValue("n", out var nPath))
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture>(nPath);
                if (tex != null && mat.GetTexture("_BumpMap") != tex) { mat.SetTexture("_BumpMap", tex); mat.EnableKeyword("_NORMALMAP"); dirty = true; }
            }
            if (dict.TryGetValue("orm", out var ormPath))
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture>(ormPath);
                if (tex != null)
                {
                    if (mat.HasProperty("_MetallicGlossMap") && mat.GetTexture("_MetallicGlossMap") != tex) { mat.SetTexture("_MetallicGlossMap", tex); mat.EnableKeyword("_METALLICSPECGLOSSMAP"); dirty = true; }
                    if (mat.HasProperty("_OcclusionMap") && mat.GetTexture("_OcclusionMap") != tex) { mat.SetTexture("_OcclusionMap", tex); mat.EnableKeyword("_OCCLUSIONMAP"); dirty = true; }
                }
            }
            if (dict.TryGetValue("em", out var emPath))
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture>(emPath);
                if (tex != null && mat.GetTexture("_EmissionMap") != tex) { mat.SetTexture("_EmissionMap", tex); mat.EnableKeyword("_EMISSION"); mat.SetColor("_EmissionColor", Color.white); dirty = true; }
            }
            if (dirty) { EditorUtility.SetDirty(mat); fixedMats++; }
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[BuildingKitMaterialFix] fixed {fixedMats}/{matGuids.Length} materials");
    }

    public static void FixHeadless() => FixMaterials();
}
#endif
