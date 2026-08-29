# AGENTS.md — Unity Project Guidelines & Live Editor Integration

This guide provides instructions and standards for AI agents working in this repository.

---

## 🚀 Core Directive: Prefer Live Unity Editor via `unity-cli`

> **CRITICAL:** Whenever the Unity Editor is running, **always use `unity-cli` to inspect, create, modify, and test scene objects, prefabs, components, and assets**.
> **NEVER hand-edit or regex-patch Unity YAML files** (`.unity`, `.prefab`, `.asset`, `.mat`, `.controller`) directly unless explicitly instructed or when no editor is available. Direct YAML edits frequently corrupt GUIDs, fileIDs, and internal serialization references.

---

## 🔬 Research Standard (MANDATORY for audits, refactors & new systems)

Before auditing, simplifying, or refactoring gameplay / character / rendering code, you MUST research
the current best practices and the "gold standard" for the specific pattern using BOTH tools:

- **context7** — resolve the relevant library (e.g. `/websites/unity3d_manual`) and query the
  specific pattern (state machines, damage/health, input, animation, URP, etc.).
- **firecrawl** — web-search developer sources (Unity discussions, gamedev.tv, official docs,
  Unity best-practice guides) for the same pattern to validate and cross-check the guidance.

Synthesize the findings into your plan and explicitly state the gold-standard pattern you are
targeting. Do not refactor from intuition alone — anchor every change to researched best practice.

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
# Enter / exit Play mode (NOT 'editor_play_exit' — it does not exist)
unity command editor_play
unity command editor_stop

# Read the Unity Console (supports --level error|warning and --tail N)
unity command console --level error --tail 20
unity command clear_console

# Force script recompilation and poll until done
unity command recompile
unity command recompile_status

# Capture Game/Scene view screenshot
unity command screenshot --output ./screenshot.png --width 1920 --height 1080

# Log messages to the Unity Console
unity command log_editor "Message from agent"
```
Note: `unity logs` reads the **Unity Hub CLI** log, NOT the Editor console. For
Editor log output use `unity command console` (or tail
`%LOCALAPPDATA%\Unity\Editor\Editor.log` with `FileShare.ReadWrite` — the file
is locked while the Editor runs).

### 4. Live C# Evaluation — ALWAYS use `eval_file`, not inline `eval`

> **CRITICAL:** Passing C# inline through PowerShell breaks on (a) embedded
> double quotes, (b) `-` at token starts (parsed as CLI flags), (c) char
> literals like `' '` (PowerShell merges/mangles them), and (d) single quotes.
> WSL does not fix this either (`wsl.exe` joins args without re-quoting).

**The reliable pattern:** write the C# to a temp `.cs` file, then run it.

```powershell
# 1) Write the script (Write tool) to e.g. C:\Users\<user>\AppData\Local\Temp\opencode\task.cs
# 2) Execute:
unity command eval_file --file "C:\Users\<user>\AppData\Local\Temp\opencode\task.cs"
```

Rules for eval scripts:
- **No `using` directives** — the Roslyn context rejects them. Fully qualify
  everything (`UnityEngine.GameObject`, `System.Text.StringBuilder`, …).
- Top-level statements + local functions are OK; `return value;` returns output.
- Multi-line, real string literals, bullets, quotes — all fine once in a file.
- Long output (>50 KB) is truncated but saved to a file; parse that file (it is
  a single JSON line — extract the `"result":"..."` field and split on `\r\n`).

Minimal example `task.cs`:
```csharp
var sb = new System.Text.StringBuilder();
sb.AppendLine("Scene: " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
foreach (var r in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
    sb.AppendLine("- " + r.name);
return sb.ToString();
```

### 5. Comparing objects / prefabs safely
Prefer serialized-property diffs over guessing. See
`docs/spawnable_player_rigging_fixes.md` for a reusable diff approach:
iterate `SerializedObject.GetIterator()`, compare by `propertyPath`, and
**ignore identity noise** (`m_FileID`, `m_CorrespondingSourceObject`,
`m_PrefabInstance`, `m_PrefabAsset`, `m_GameObject`, `m_Father`, `m_Children`,
`m_Bones`, `m_RootBone`). When diffing two hierarchies, normalize paths so
different root names still match (compare relative paths under the root).

---

## 🔄 Safe Mode & Compilation Error Recovery

When the C# codebase has compilation errors:
1. The Unity Editor enters **Safe Mode**.
2. In Safe Mode, custom packages including `com.unity.pipeline` are disabled, causing `unity command` to fail or time out.

### Recovery Workflow:
1. **Check compilation errors** (tail the Editor log — `unity logs` reads the Hub log, not this):
   ```powershell
   Get-Content "$env:LOCALAPPDATA\Unity\Editor\Editor.log" -Tail 50
   ```
2. **Fix C# syntax/type errors** in `Assets/_Game/Scripts/`.
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

- **Scripts Directory:** `Assets/_Game/Scripts/`
  - `Characters/Player/`: Player brain, input observers, and player locomotion states (`StateMachine/`).
  - `Core/AI/`: AI locomotion, context, state machine, and reactive triggers.
  - `Core/InputHandler/`: Input actions enum and input subject.
  - `Core/Observer/`: Generic `Subject<TAction, TValue>` / `IObserver` pattern. `Subject.AddObserver` does **not** dedupe — guard subscriptions with a flag when both `OnEnable` and `Start` can subscribe.
  - Singleton duplicate guards must `Destroy(this)` (the component), **never** `Destroy(gameObject)` — killing the GameObject takes siblings with it and `OnDestroy` then nulls the singleton (see `docs/zombie_bite_interaction_fixes.md`).
  - Unity magic methods (`Start`, `Awake`, …) are **not virtual**: a private `Start` in a derived class hides the base one, silently skipping base setup (e.g. `ActorBrainBase.Start` registration). Make the base `protected virtual` and call `base.Start()` from overrides.
  - `Core/UI/`: Character UI controller/elements.
  - `Items/`: PrefabManager, weapons, firearm events and gun contexts.
- **Player spawning architecture (composition root):**
  - Scenes contain **only** map + MainCamera (CinemachineBrain + `MousePosition` child) + baked NavMesh Surface + `PlayerSpawner`.
  - `PlayerSpawner.Awake` instantiates `SpawnableFemaleCharacter`, `InputHandler`, and `PlayerCoreComponents` prefabs and wires ALL cross-references on the **instances** (never on prefab assets — mutating a prefab asset at runtime corrupts it for every future spawn).
  - Unity **strips prefab → scene references** on save. Any scene object a prefab needs must be re-injected at spawn time. See `docs/spawnable_player_requirements.md`.
  - **Animation Rigging:** `RigBuilder` builds its graph during `Instantiate`. After changing constraint data (e.g. `MultiAimConstraint.sourceObjects`) at runtime, call `rigBuilder.Clear(); rigBuilder.Build();` or the constraints silently ignore the new data. Details: `docs/spawnable_player_rigging_fixes.md`.
  - `PlayerCoreUI._aimTarget` is the **Crossair UI toggle**, not the world aim point; the world aim point is the `AimTarget` child of `PlayerCoreComponents`.
- **Shooting engine:**
  - **AIM FIRST:** the weapon's rest pose points **down**. Shooting without aiming fires along the muzzle's rest forward — bullets go into the ground. This is intended; the aim direction (`HandgunContext.aimDirection` toward `CharacterLocomotion._aimTarget`) is only meaningful while the crosshair is active. Details + debug workflow: `docs/shooting_engine_notes.md`.
  - Bullets are pooled (`ObjectPool<BulletProjectile>`), teleported via `Rigidbody.position` (never transform-only on re-activated bodies), use `ContinuousSpeculative` CCD, and score exactly one damage event per flight (`_hasHit` / `_isReleased` guards).
  - `DebugHud` (F3 toggle, attached by `PlayerSpawner`) shows player HP, FSM states, clip/reserve, live bullets and the `CombatLog` ring buffer — the fastest way to diagnose combat issues.
- **Conventions:**
  - Follow standard C# naming conventions (PascalCase for public methods/properties, camelCase / `_camelCase` for private fields).
  - Always maintain corresponding `.meta` files when creating, moving, or deleting C# scripts and assets.
  - Test state machine changes incrementally.

---

## ⚡ Performance Best Practices & Anti-pattern Guidelines

1. **Pre-hash Animator Parameters**:
   - Never call `animator.SetFloat("Name", ...)` or `animator.SetTrigger("Name")` in hot loops or update ticks.
   - Use `Animator.StringToHash("Name")` cached in static constants (e.g. `AnimatorUtils.HorizontalHash`).
2. **Avoid Heap Allocation in Scene / Physics Queries**:
   - Avoid `Physics.OverlapSphere` in per-tick AI vision or combat checks. Use `Physics.OverlapSphereNonAlloc` with pre-allocated buffer arrays.
3. **Cache Camera.main and Components**:
   - Never use `Camera.main` or `GetComponent<T>()` inside `Update()` or `CheckTransitions()`. Cache references in `Awake()`, `Start()`, or context blackboards.
4. **Observer Notification & List Iterations**:
   - Avoid `List<T>.ForEach(action => ...)` or LINQ allocations in frequent callbacks. Use indexed reverse loops (`for (int i = list.Count - 1; i >= 0; i--)`) to eliminate closures and handle mutations safely.

---

## 🎨 Unity 6 Rendering Scalability & URP Standards

When configuring Universal Render Pipeline in Unity 6:

1. **GPU-Resident Drawer (GRD) & Forward+ Rendering Path**:
   - GRD requires **Forward+** (`RenderingMode.ForwardPlus` = 2) or **Deferred+** (`RenderingMode.DeferredPlus` = 3) on all Universal Renderer Data assets (`URP-*-Renderer.asset`).
   - In `UniversalRenderPipelineAsset`, set `gpuResidentDrawerMode = GPUResidentDrawerMode.InstancedDrawing` and `gpuResidentDrawerEnableOcclusionCullingInCameras = true`.
2. **BatchRendererGroup (BRG) Variants**:
   - When GRD is active, set `m_BrgStripping: 2` (**Keep All**) in `ProjectSettings/GraphicsSettings.asset` to prevent stripping DOTS instancing shaders during player builds.
3. **Native Render Graph Framework**:
   - Unity 6 deprecates Compatibility Mode. Enable Render Graph natively (`m_EnableRenderGraph: 1` in `UniversalRenderPipelineGlobalSettings.asset`) and remove `URP_COMPATIBILITY_MODE` define symbols from `PlayerSettings.SetScriptingDefineSymbols`.
4. **Spatial-Temporal Post-Processing (STP)**:
   - Configure `upscalingFilter = UpscalingFilterSelection.STP` on quality tier assets for temporal spatial upscaling.
5. **Unity CLI `eval` Quoting in PowerShell**:
   - Do not pass C# inline. Use `unity command eval_file --file <task.cs>` — see §4 "Live C# Evaluation".

