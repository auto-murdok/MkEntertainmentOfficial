#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Unity.AI.Navigation;

public static class ExpandedArenaGenerator
{
    private const string SourceScenePath = "Assets/_Game/Scenes/Arenas/ZombieCombatArena.unity";
    private const string TargetScenePath = "Assets/_Game/Scenes/Arenas/ExpandedCombatArena.unity";
    private const string BlueMatPath = "Assets/_Game/Art/Environment/Materials/GridBlue_01_Mat.mat";
    private const string OrangeMatPath = "Assets/_Game/Art/Environment/Materials/GridOrange_01_Mat.mat";

    [MenuItem("Tools/Arena/Generate Expanded Combat Arena")]
    public static void GenerateScene()
    {
        Debug.Log("[ExpandedArenaGenerator] Starting generation...");

        // 1. Open the original scene
        var sourceScene = EditorSceneManager.OpenScene(SourceScenePath, OpenSceneMode.Single);
        
        // 2. Save as new scene
        EditorSceneManager.SaveScene(sourceScene, TargetScenePath);
        var activeScene = EditorSceneManager.OpenScene(TargetScenePath, OpenSceneMode.Single);

        Material blueMat = AssetDatabase.LoadAssetAtPath<Material>(BlueMatPath);
        Material orangeMat = AssetDatabase.LoadAssetAtPath<Material>(OrangeMatPath);

        // 3. Remove old single Cube and ExampleObject if present
        var oldCube = GameObject.Find("Cube");
        if (oldCube != null) Object.DestroyImmediate(oldCube);
        var oldExample = GameObject.Find("ExampleObject");
        if (oldExample != null) Object.DestroyImmediate(oldExample);

        // 4. Configure Floor (Plane)
        var floor = GameObject.Find("Plane");
        if (floor == null)
        {
            floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Plane";
        }
        // Plane is 10x10 at scale 1. Scale 4 -> 40m x 40m (16x area of original 10x10)
        floor.transform.position = Vector3.zero;
        floor.transform.rotation = Quaternion.identity;
        floor.transform.localScale = new Vector3(4f, 1f, 4f);
        
        if (blueMat != null)
        {
            var renderer = floor.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.sharedMaterial = blueMat;
        }
        GameObjectUtility.SetStaticEditorFlags(floor, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.OccluderStatic);

        // 5. Parent container for environment structures
        var envRoot = GameObject.Find("Environment_Structures");
        if (envRoot != null) Object.DestroyImmediate(envRoot);
        envRoot = new GameObject("Environment_Structures");
        envRoot.transform.position = Vector3.zero;

        // Container for Perimeter Walls
        var wallsRoot = new GameObject("Perimeter_Walls");
        wallsRoot.transform.SetParent(envRoot.transform);

        float arenaHalfSize = 20f; // 40m total width / depth
        float wallHeight = 4f;
        float wallThickness = 1f;

        // North Wall
        CreateWall(wallsRoot.transform, "Wall_North", new Vector3(0, wallHeight * 0.5f, arenaHalfSize), new Vector3(arenaHalfSize * 2f + wallThickness, wallHeight, wallThickness), orangeMat);
        // South Wall
        CreateWall(wallsRoot.transform, "Wall_South", new Vector3(0, wallHeight * 0.5f, -arenaHalfSize), new Vector3(arenaHalfSize * 2f + wallThickness, wallHeight, wallThickness), orangeMat);
        // East Wall
        CreateWall(wallsRoot.transform, "Wall_East", new Vector3(arenaHalfSize, wallHeight * 0.5f, 0), new Vector3(wallThickness, wallHeight, arenaHalfSize * 2f + wallThickness), orangeMat);
        // West Wall
        CreateWall(wallsRoot.transform, "Wall_West", new Vector3(-arenaHalfSize, wallHeight * 0.5f, 0), new Vector3(wallThickness, wallHeight, arenaHalfSize * 2f + wallThickness), orangeMat);

        // Container for Obstacles
        var obstaclesRoot = new GameObject("Obstacles_And_Cover");
        obstaclesRoot.transform.SetParent(envRoot.transform);

        // --- Center Courtyard ---
        // 4 Angled low cover barriers around center
        CreateCoverBox(obstaclesRoot.transform, "Center_Cover_NW", new Vector3(-4f, 0.6f, 4f), new Vector3(3f, 1.2f, 0.6f), Quaternion.Euler(0, 45, 0), orangeMat);
        CreateCoverBox(obstaclesRoot.transform, "Center_Cover_NE", new Vector3(4f, 0.6f, 4f), new Vector3(3f, 1.2f, 0.6f), Quaternion.Euler(0, -45, 0), orangeMat);
        CreateCoverBox(obstaclesRoot.transform, "Center_Cover_SW", new Vector3(-4f, 0.6f, -4f), new Vector3(3f, 1.2f, 0.6f), Quaternion.Euler(0, -45, 0), orangeMat);
        CreateCoverBox(obstaclesRoot.transform, "Center_Cover_SE", new Vector3(4f, 0.6f, -4f), new Vector3(3f, 1.2f, 0.6f), Quaternion.Euler(0, 45, 0), orangeMat);

        // --- North-East Quadrant: Warehouse / Pillars & Stacked Crates ---
        CreatePillar(obstaclesRoot.transform, "Pillar_NE_1", new Vector3(8f, 2f, 8f), new Vector3(1.4f, 2f, 1.4f), orangeMat);
        CreatePillar(obstaclesRoot.transform, "Pillar_NE_2", new Vector3(14f, 2f, 8f), new Vector3(1.4f, 2f, 1.4f), orangeMat);
        CreatePillar(obstaclesRoot.transform, "Pillar_NE_3", new Vector3(8f, 2f, 14f), new Vector3(1.4f, 2f, 1.4f), orangeMat);
        CreatePillar(obstaclesRoot.transform, "Pillar_NE_4", new Vector3(14f, 2f, 14f), new Vector3(1.4f, 2f, 1.4f), orangeMat);
        
        // Shipping container / heavy crate
        CreateCoverBox(obstaclesRoot.transform, "Crate_Large_NE", new Vector3(11f, 1.25f, 11f), new Vector3(3.5f, 2.5f, 2f), Quaternion.Euler(0, 20, 0), orangeMat);
        CreateCoverBox(obstaclesRoot.transform, "Crate_Low_NE", new Vector3(11f, 0.6f, 7.5f), new Vector3(2f, 1.2f, 1.2f), Quaternion.identity, orangeMat);

        // --- North-West Quadrant: Alleyways & Partition Walls ---
        CreateCoverBox(obstaclesRoot.transform, "Partition_NW_1", new Vector3(-10f, 1.5f, 12f), new Vector3(0.6f, 3f, 8f), Quaternion.identity, orangeMat);
        CreateCoverBox(obstaclesRoot.transform, "Partition_NW_2", new Vector3(-15f, 1.5f, 8f), new Vector3(6f, 3f, 0.6f), Quaternion.identity, orangeMat);
        CreateCoverBox(obstaclesRoot.transform, "Cover_NW_Alley", new Vector3(-12.5f, 0.6f, 12f), new Vector3(2.5f, 1.2f, 0.6f), Quaternion.identity, orangeMat);

        // --- South-West Quadrant: Raised Platform & Tactical Ramp ---
        // Platform
        var platform = CreateCoverBox(obstaclesRoot.transform, "Platform_SW", new Vector3(-11f, 0.75f, -11f), new Vector3(7f, 1.5f, 7f), Quaternion.identity, orangeMat);
        // Half-walls on platform
        CreateCoverBox(platform.transform, "Parapet_W", new Vector3(-3.2f, 1.25f, 0), new Vector3(0.4f, 1f, 6.5f), Quaternion.identity, orangeMat);
        CreateCoverBox(platform.transform, "Parapet_S", new Vector3(0, 1.25f, -3.2f), new Vector3(6.5f, 1f, 0.4f), Quaternion.identity, orangeMat);
        // Ramp (tilted cube)
        var ramp = CreateCoverBox(obstaclesRoot.transform, "Ramp_SW", new Vector3(-11f, 0.75f, -6f), new Vector3(4f, 0.4f, 4.5f), Quaternion.Euler(20f, 0, 0), orangeMat);

        // --- South-East Quadrant: Bunker & Sandbag Staggers ---
        CreateCoverBox(obstaclesRoot.transform, "Bunker_Wall_SE_1", new Vector3(10f, 1f, -10f), new Vector3(5f, 2f, 0.8f), Quaternion.identity, orangeMat);
        CreateCoverBox(obstaclesRoot.transform, "Bunker_Wall_SE_2", new Vector3(12.5f, 1f, -13f), new Vector3(0.8f, 2f, 5f), Quaternion.identity, orangeMat);
        CreateCoverBox(obstaclesRoot.transform, "Cover_SE_Low1", new Vector3(7f, 0.6f, -14f), new Vector3(3f, 1.2f, 0.6f), Quaternion.Euler(0, 30, 0), orangeMat);
        CreateCoverBox(obstaclesRoot.transform, "Cover_SE_Low2", new Vector3(14f, 0.6f, -6f), new Vector3(3f, 1.2f, 0.6f), Quaternion.Euler(0, -30, 0), orangeMat);

        // 6. Adjust Zombie Spawner Points to cover the larger arena
        var spawnerObj = GameObject.Find("[Zombie Spawner]");
        if (spawnerObj != null)
        {
            SetOrCreateSpawnPoint(spawnerObj.transform, "Spawn_North", new Vector3(0, 0, 17f));
            SetOrCreateSpawnPoint(spawnerObj.transform, "Spawn_South", new Vector3(0, 0, -17f));
            SetOrCreateSpawnPoint(spawnerObj.transform, "Spawn_East", new Vector3(17f, 0, 0));
            SetOrCreateSpawnPoint(spawnerObj.transform, "Spawn_West", new Vector3(-17f, 0, 0));
            
            SetOrCreateSpawnPoint(spawnerObj.transform, "Spawn_NE", new Vector3(16f, 0, 16f));
            SetOrCreateSpawnPoint(spawnerObj.transform, "Spawn_NW", new Vector3(-16f, 0, 16f));
            SetOrCreateSpawnPoint(spawnerObj.transform, "Spawn_SE", new Vector3(16f, 0, -16f));
            SetOrCreateSpawnPoint(spawnerObj.transform, "Spawn_SW", new Vector3(-16f, 0, -16f));
        }

        // 7. Reset Player Position
        var player = GameObject.Find("FemaleCharacter");
        if (player != null)
        {
            player.transform.position = Vector3.zero;
            player.transform.rotation = Quaternion.identity;
        }

        // 8. Configure NavMeshSurface & Bake
        var navSurface = floor.GetComponent<NavMeshSurface>();
        if (navSurface == null)
        {
            navSurface = floor.AddComponent<NavMeshSurface>();
        }
        navSurface.collectObjects = CollectObjects.All;
        navSurface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
        
        Debug.Log("[ExpandedArenaGenerator] Baking NavMeshSurface...");
        navSurface.BuildNavMesh();

        // 9. Save scene
        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);
        AssetDatabase.SaveAssets();

        Debug.Log("[ExpandedArenaGenerator] Successfully created and baked " + TargetScenePath);
    }

    private static GameObject CreateWall(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
    {
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = name;
        wall.transform.SetParent(parent);
        wall.transform.position = pos;
        wall.transform.localScale = scale;
        if (mat != null) wall.GetComponent<MeshRenderer>().sharedMaterial = mat;
        GameObjectUtility.SetStaticEditorFlags(wall, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.OccluderStatic);
        return wall;
    }

    private static GameObject CreateCoverBox(Transform parent, string name, Vector3 pos, Vector3 scale, Quaternion rot, Material mat)
    {
        var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name;
        box.transform.SetParent(parent);
        box.transform.position = pos;
        box.transform.rotation = rot;
        box.transform.localScale = scale;
        if (mat != null) box.GetComponent<MeshRenderer>().sharedMaterial = mat;
        GameObjectUtility.SetStaticEditorFlags(box, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.OccluderStatic);
        return box;
    }

    private static GameObject CreatePillar(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
    {
        var cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cylinder.name = name;
        cylinder.transform.SetParent(parent);
        cylinder.transform.position = pos;
        cylinder.transform.localScale = scale;
        if (mat != null) cylinder.GetComponent<MeshRenderer>().sharedMaterial = mat;
        GameObjectUtility.SetStaticEditorFlags(cylinder, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.OccluderStatic);
        return cylinder;
    }

    private static void SetOrCreateSpawnPoint(Transform parent, string name, Vector3 localPos)
    {
        var child = parent.Find(name);
        if (child == null)
        {
            var newChild = new GameObject(name);
            newChild.transform.SetParent(parent);
            child = newChild.transform;
        }
        child.localPosition = localPos;
    }
}
#endif
