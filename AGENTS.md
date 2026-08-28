# AGENTS.md — Unity Project Guidelines & Live Editor Integration

This guide provides instructions and standards for AI agents working in this repository.

---

## 🚀 Core Directive: Prefer Live Unity Editor via `unity-cli`

> **CRITICAL:** Whenever the Unity Editor is running, **always use `unity-cli` to inspect, create, modify, and test scene objects, prefabs, components, and assets**.
> **NEVER hand-edit or regex-patch Unity YAML files** (`.unity`, `.prefab`, `.asset`, `.mat`, `.controller`) directly unless explicitly instructed or when no editor is available. Direct YAML edits frequently corrupt GUIDs, fileIDs, and internal serialization references.

---

## 🛠️ Unity CLI Quick Reference

The `unity-cli` interacts directly with the live Editor instance via the `com.unity.pipeline` package (running locally over IPC/WebSocket).

### 1. Launching & Verification
When opening or starting the Unity Editor for automated CLI workflows:
```bash
# Open Editor with automated flag
unity open --args "-automated"
unity open . --args "-automated"

# Check whether Editor is connected and ready
unity status --format json
```
- Look for state `"ready"`.
- If multiple projects/editors are open, specify `--project-path .` (or absolute path).

### 2. Tool & Command Discovery
List what the connected Editor exposes:
```bash
# List all available Editor commands
unity command

# Search for specific commands (e.g. screenshot, play, gameobject)
unity command --query screenshot
unity command --query gameobject

# List with JSON formatting
unity command --format json
```

### 3. Executing Editor Commands
```bash
# Toggle / Enter Play mode
unity command editor_play

# Capture Game/Scene view screenshot
unity command screenshot --output ./screenshot.png --width 1920 --height 1080

# Log messages to the Unity Console
unity command log_editor "Message from agent"
```

### 4. Live C# Evaluation (`eval`)
Use `eval` to inspect hierarchy, instantiate prefabs, inspect components, or manipulate the scene in real time:
```bash
# Inspect Unity Version / Scene
unity command eval "return UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;"

# Find a GameObject or component property
unity command eval "return UnityEngine.GameObject.Find('Player')?.transform.position.ToString();"

# Create or configure objects live in Editor
unity command eval "var go = new UnityEngine.GameObject('TestTarget'); go.transform.position = new UnityEngine.Vector3(0, 1, 0);"

# Instantiate a Prefab from Assets
unity command eval "var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>('Assets/Prefabs/Characters/Zombie/ZombieModel.prefab'); UnityEditor.PrefabUtility.InstantiatePrefab(prefab);"

# Mark active scene dirty & save
unity command eval "UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();"
```

---

## 🔄 Safe Mode & Compilation Error Recovery

When the C# codebase has compilation errors:
1. The Unity Editor enters **Safe Mode**.
2. In Safe Mode, custom packages including `com.unity.pipeline` are disabled, causing `unity command` to fail or time out.

### Recovery Workflow:
1. **Check compilation errors:**
   ```bash
   unity logs --tail 50
   ```
2. **Fix C# syntax/type errors** in `Assets/Scripts/`.
3. **Verify the Editor leaves Safe Mode** and pipeline reconnects:
   ```bash
   unity pipeline list
   unity status
   ```

---

## 📦 Pipeline Package Management

If the pipeline package is missing or needs updating:
```bash
# Check status of pipeline package across editors
unity pipeline list

# Install / update com.unity.pipeline
unity pipeline install

# Upgrade to latest registry version
unity pipeline upgrade
```

---

## 🏗️ Project Architecture & Coding Standards

- **Scripts Directory:** `Assets/Scripts/`
  - `Core/AI/`: AI locomotion, context, state machine, and reactive triggers.
  - `Core/CharacterStateMachine/`: Base state machine, state definitions, and blackboard architecture.
  - `Player/`: Player controllers, contexts, UI elements, and player locomotion states.
  - `Items/Weapons/`: Weapon interfaces (`IFirearm`), weapon states, firearm events, and gun contexts.
- **Conventions:**
  - Follow standard C# naming conventions (PascalCase for public methods/properties, camelCase / `_camelCase` for private fields).
  - Always maintain corresponding `.meta` files when creating, moving, or deleting C# scripts and assets.
  - Test state machine changes incrementally.
