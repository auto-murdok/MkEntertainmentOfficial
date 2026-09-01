// UEI helper - dump the material bindings of every prefab in an imported kit
// via unity-cli, to verify the import actually wired textures.
//
// Usage:
//   1. Edit KIT_NAME below (folder name under Assets/ImportedContent/).
//   2. unity command eval_file --file Tools\UEImport\unity\verify_import.cs
//
// Output per renderer slot:
//   <renderer>/<material>: base=<tex> normal=<tex> metal=<tex> occ=<tex>
// Any NULL means that texture did not get wired.

const string KIT_NAME = "Building_kit";

var sb = new System.Text.StringBuilder();
var kitFolder = "Assets/ImportedContent/" + KIT_NAME;
var prefabGuids = UnityEditor.AssetDatabase.FindAssets("t:Prefab", new[] { kitFolder });
if (prefabGuids.Length == 0) return "FAIL: no prefabs under " + kitFolder;
foreach (var guid in prefabGuids)
{
    var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
    var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(path);
    if (prefab == null) continue;
    sb.AppendLine("== " + path);
    foreach (var r in prefab.GetComponentsInChildren<UnityEngine.MeshRenderer>())
        foreach (var m in r.sharedMaterials)
        {
            if (m == null) { sb.AppendLine("  " + r.name + ": NULL MATERIAL"); continue; }
            var b = m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : null;
            var n = m.HasProperty("_BumpMap") ? m.GetTexture("_BumpMap") : null;
            var mg = m.HasProperty("_MetallicGlossMap") ? m.GetTexture("_MetallicGlossMap") : null;
            var oc = m.HasProperty("_OcclusionMap") ? m.GetTexture("_OcclusionMap") : null;
            var mask = m.HasProperty("_MaskMap") ? m.GetTexture("_MaskMap") : null;
            sb.AppendLine("  " + r.name + "/" + m.name
                + ": base=" + (b == null ? "NULL" : b.name)
                + " normal=" + (n == null ? "NULL" : n.name)
                + " metal=" + (mg == null ? "NULL" : mg.name)
                + " occ=" + (oc == null ? "NULL" : oc.name)
                + (mask != null ? " mask=" + mask.name : ""));
        }
}
return sb.ToString();
