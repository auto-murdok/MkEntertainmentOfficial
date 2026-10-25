using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Generates the Mansion chunk scenes from the placement manifest
// (the "edit-time scene generation" of the stackable-prefabs plan —
// real saved scenes, no runtime spawner). Regenerate after a factory
// run or a manifest change; never hand-edit the generated scenes,
// hand-authored content belongs in other scenes.
//
// Chunk assignment is ONE function (ChunkFor) so re-sectoring later
// is a local change: Kit -> Shell, foliage/exterior props + ground
// planes -> Grounds, everything else -> Interior, plus a Core scene
// (sun + MansionSceneLoader) that owns lighting and stays active.
// Load group == lightmap bake group == occlusion bake group by
// construction: chunks are always loaded together for this level.
//
// Transform conversion (validated 2026-10-25 against the Blender
// reconstruction's world matrices for 2,461 non-mirrored placements,
// worst deviation on a single hand-tweaked prop):
//   position  Unity = (UE.x, UE.z, UE.y) / 100
//   rotation  R_u = A * FRotationMatrix(pyr) * A^T, A = (x,z,y) swap
//   scale     Unity = (|sx|, |sz|, |sy|); negative determinant selects
//             the factory's baked _M mirrored prefab instead of a
//             negative scale.
public static class MansionSceneBuilder
{
    private const string MansionRoot = "Assets/_Game/Environments/Mansion";
    private const string SourceDir = MansionRoot + "/Source";
    private const string PrefabsDir = MansionRoot + "/Prefabs";
    private const string ScenesDir = MansionRoot + "/Scenes";
    private const string AuditDir = MansionRoot + "/Audit";

    private static readonly string[] ChunkNames =
        { "Mansion_Shell", "Mansion_Interior", "Mansion_Grounds" };

    [Serializable]
    private class ManifestJson
    {
        public ManifestActor[] actors;
    }

    [Serializable]
    private class ManifestActor
    {
        public string label;
        public string kind;
        public string mesh;
        public float[] loc_cm;
        public float[] rot_pyr;
        public float[] scale;
    }

    [MenuItem("Tools/Mansion/Build Chunk Scenes")]
    public static void BuildAll()
    {
        EnsureFolder(ScenesDir);
        var catalog = AssetDatabase.LoadAssetAtPath<MansionPartCatalog>(
            MansionRoot + "/MansionPartCatalog.asset");
        if (catalog == null)
        {
            throw new InvalidOperationException("Part catalog missing — run the prefab factory first.");
        }
        var classByPart = catalog.entries.ToDictionary(e => e.partName, e => e.partClass);

        string json = File.ReadAllText(Path.GetFullPath(SourceDir + "/level_actors.json"));
        ManifestActor[] actors = JsonUtility.FromJson<ManifestJson>(json).actors
            .Where(a => a.kind == "staticmesh").ToArray();

        var counts = new Dictionary<string, int>();
        foreach (string chunk in ChunkNames)
        {
            counts[chunk] = BuildChunkScene(chunk, actors, classByPart);
        }
        BuildCoreScene();
        RegisterBuildScenes();

        string report = "{\n" + string.Join(",\n",
            counts.Select(kv => $"  \"{kv.Key}\": {kv.Value}")) + "\n}\n";
        File.WriteAllText(Path.GetFullPath(AuditDir + "/scene-build-report.json"), report);
        AssetDatabase.Refresh();
        Debug.Log("[MansionBuilder] Chunk scenes built: " +
                  string.Join(", ", counts.Select(kv => $"{kv.Key}={kv.Value}")));
    }

    private static string ChunkFor(ManifestActor actor, IReadOnlyDictionary<string, string> classByPart)
    {
        if (string.IsNullOrEmpty(actor.mesh) || actor.mesh == "Plane")
        {
            return "Mansion_Grounds";
        }
        string partClass = classByPart.TryGetValue(actor.mesh, out string c) ? c : "Fixture";
        if (partClass == "KitStructural")
        {
            return "Mansion_Shell";
        }
        if (partClass == "Foliage"
            || actor.mesh.Contains("Fence")
            || actor.mesh.Contains("Gate")
            || actor.mesh.Contains("Lamp"))
        {
            return "Mansion_Grounds";
        }
        return "Mansion_Interior";
    }

    private static int BuildChunkScene(
        string chunkName, ManifestActor[] actors, IReadOnlyDictionary<string, string> classByPart)
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = chunkName;
        var root = new GameObject("Generated");
        int placed = 0;

        foreach (ManifestActor actor in actors)
        {
            if (ChunkFor(actor, classByPart) != chunkName)
            {
                continue;
            }
            GameObject instance = InstantiateActor(actor);
            if (instance == null)
            {
                continue;
            }
            instance.transform.SetParent(root.transform, false);
            placed++;
        }

        string path = $"{ScenesDir}/{chunkName}.unity";
        EditorSceneManager.SaveScene(scene, path);
        return placed;
    }

    private static GameObject InstantiateActor(ManifestActor actor)
    {
        ConvertTransform(actor, out Vector3 position, out Quaternion rotation, out Vector3 scale, out bool mirrored);

        GameObject instance;
        if (actor.mesh == "Plane")
        {
            instance = GameObject.CreatePrimitive(PrimitiveType.Plane);
        }
        else
        {
            string prefabName = mirrored ? actor.mesh + "_M" : actor.mesh;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabsDir}/{prefabName}.prefab");
            if (prefab == null)
            {
                Debug.LogError($"[MansionBuilder] Wrapper prefab missing for actor {actor.label} ({prefabName})");
                return null;
            }
            instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        }
        instance.name = actor.label;
        instance.transform.localPosition = position;
        instance.transform.localRotation = rotation;
        instance.transform.localScale = scale;
        return instance;
    }

    private static void BuildCoreScene()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = "Mansion_Core";

        var sun = new GameObject("Sun");
        var light = sun.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.0f;
        light.shadows = LightShadows.Soft;
        sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        var loaderObject = new GameObject("MansionLoader");
        var loader = loaderObject.AddComponent<Game.Core.Mansion.MansionSceneLoader>();
        var serialized = new SerializedObject(loader);
        SerializedProperty chunks = serialized.FindProperty("_chunkScenes");
        chunks.arraySize = ChunkNames.Length;
        for (int i = 0; i < ChunkNames.Length; i++)
        {
            chunks.GetArrayElementAtIndex(i).stringValue = ChunkNames[i];
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, $"{ScenesDir}/Mansion_Core.unity");
    }

    private static void RegisterBuildScenes()
    {
        var scenes = EditorBuildSettings.scenes.ToList();
        foreach (string name in new[] { "Mansion_Core" }.Concat(ChunkNames))
        {
            string path = $"{ScenesDir}/{name}.unity";
            if (scenes.All(s => s.path != path))
            {
                scenes.Add(new EditorBuildSettingsScene(path, true));
            }
        }
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    // UE (left-handed, Z-up, cm) -> Unity (right-handed, Y-up, m).
    private static void ConvertTransform(
        ManifestActor actor,
        out Vector3 position, out Quaternion rotation, out Vector3 scale, out bool mirrored)
    {
        float[] loc = actor.loc_cm;
        position = new Vector3(loc[0], loc[2], loc[1]) * 0.01f;

        float[] s = actor.scale;
        mirrored = s[0] * s[1] * s[2] < 0f;
        scale = new Vector3(Mathf.Abs(s[0]), Mathf.Abs(s[2]), Mathf.Abs(s[1]));

        // UE FRotationMatrix for (pitch, yaw, roll), then basis-change
        // conjugation A * M * A^T with A swapping Y/Z.
        double pitch = actor.rot_pyr[0] * Math.PI / 180.0;
        double yaw = actor.rot_pyr[1] * Math.PI / 180.0;
        double roll = actor.rot_pyr[2] * Math.PI / 180.0;
        double cp = Math.Cos(pitch), sp = Math.Sin(pitch);
        double cy = Math.Cos(yaw), sy = Math.Sin(yaw);
        double cr = Math.Cos(roll), sr = Math.Sin(roll);

        var ue = new Matrix4x4();
        ue.SetRow(0, new Vector4((float)(cy * cp), (float)(cy * sp * sr - sy * cr), (float)(cy * sp * cr + sy * sr), 0f));
        ue.SetRow(1, new Vector4((float)(sy * cp), (float)(sy * sp * sr + cy * cr), (float)(sy * sp * cr - cy * sr), 0f));
        ue.SetRow(2, new Vector4((float)-sp, (float)(cp * sr), (float)(cp * cr), 0f));
        ue.SetRow(3, new Vector4(0f, 0f, 0f, 1f));

        // A (UE basis -> Unity basis): rows map (x, y, z)_ue -> (x, z, y)_u.
        var a = new Matrix4x4();
        a.SetRow(0, new Vector4(1f, 0f, 0f, 0f));
        a.SetRow(1, new Vector4(0f, 0f, 1f, 0f));
        a.SetRow(2, new Vector4(0f, 1f, 0f, 0f));
        a.SetRow(3, new Vector4(0f, 0f, 0f, 1f));

        Matrix4x4 m = a * ue * a.transpose;
        Vector3 forward = new Vector3(m.m02, m.m12, m.m22).normalized;
        Vector3 up = new Vector3(m.m01, m.m11, m.m21).normalized;
        rotation = Quaternion.LookRotation(forward, up);
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder))
        {
            return;
        }
        string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
    }
}
