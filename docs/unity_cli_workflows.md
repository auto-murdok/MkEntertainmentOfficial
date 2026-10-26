# Unity CLI Workflows & Best Practices Guide

This guide documents the gold-standard workflow for interacting with the Unity Editor, running automated checks, and leveraging AI coding agent skills using **`unity-cli@1.0.0-beta.14`** and **`com.unity.pipeline@0.9.1-exp.1`**.

---

## 🚀 Core Principles

1. **Always drive the live Editor via `unity-cli`**:  
   Never hand-edit Unity YAML files (`.unity`, `.prefab`, `.asset`) directly. Hand-editing risks corrupting internal fileIDs and GUID references.
2. **Evaluate C# via files, not inline quotes**:  
   PowerShell and bash mangle inline double/single quotes and flags. Use `./Tools/eval-cs.ps1` or `unity command eval_file`.
3. **Verify Play Mode before trusting captures**:  
   Entering Play mode and snapping a screenshot is setup, not verification. Unfocused editors throttle execution to frame 1. Always verify frame advancement (`Time.frameCount`).

---

## 🤖 AI Agent & Antigravity Skills

Unity provides official first-party agent skills bundled via the `unity-agent-plugin`.

### Installation in Antigravity
```powershell
# Automated setup
unity setup antigravity

# Or directly via the Antigravity CLI
agy plugin install https://github.com/Unity-Technologies/unity-agent-plugin
agy plugin list  # Verifies: - unity (enabled)
```

The plugin provides structured skills for:
- `unity-cli`: Editor control, scene inspection, compilation, and batch tools.
- `ui-toolkit`: UXML/USS flexbox layouts, runtime data binding, and `UIDocument`.
- `render-graph`: Unity 6 Render Graph execution passes and URP features.
- `multiplayer`: Netcode for GameObjects (NGO) replication and authority.
- `optimize-audio` & `localization`: Engine subsystem best practices.

---

## 🎮 Play-Mode Verification Loop

### The Automated Script
Run the automated verification loop directly:
```powershell
./Tools/playmode-loop.ps1
```

### Manual Command Breakdown
```powershell
# 1. Confirm Editor status
unity status --format json

# 2. Prevent unfocused frame freeze
unity command editor set_auto_tick
unity command editor.playmode set_run_in_background

# 3. Enter Play mode
unity command editor.playmode start

# 4. CRITICAL: Prove frame progression
unity command job wait -- --condition '{"member":"UnityEngine.Time.frameCount","op":"changed"}' --timeout_s 10

# 5. Capture Game view screenshot
unity command capture game_view -- --source screen --save_path Screenshots/playtest.png

# 6. Read console exceptions
unity command console get -- --tail 20 --level error

# 7. Stop Play mode
unity command editor.playmode stop
unity command editor.playmode set_run_in_background -- --enable false
```

> [!NOTE]
> **Desktop Lock Warning**: If the machine desktop is locked, `--source screen` will produce a solid blank frame. Use `--source camera` when testing unattended on locked servers.

---

## ⚡ Safe C# Evaluation in Live Editor

Roslyn evaluation scripts execute inside the running Editor process:
- **No `using` directives**: All types must be fully qualified (`UnityEngine.GameObject`, `System.Text.StringBuilder`).
- **Use helper script**:
  ```powershell
  # Inline string execution without escaping bugs
  ./Tools/eval-cs.ps1 -Code 'return UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;'

  # Or pipe code
  "return UnityEngine.Time.frameCount;" | ./Tools/eval-cs.ps1

  # Or execute a .cs file
  ./Tools/eval-cs.ps1 -ScriptFile "path/to/task.cs"
  ```

---

## ⚙️ Disambiguating Multiple Editors

When multiple Editors or Multiplayer Play Mode (MPPM) virtual players are running:
```powershell
# By project path
unity command editor status --project-path "C:/murdokdev/MKEntertainmentOfficial"

# By port
unity command editor status --port 38412

# By process ID
unity command editor status --pid 14220

# By MPPM instance name
unity command editor status --instance "Player 2"
```

---

## 🛡️ Safe Mode Recovery

When compilation errors block the Editor:
1. `unity status` reports `STATUS_BLOCKED_BY_DIALOG_SAFE_MODE` along with the specific compile errors.
2. Launching with `unity open . --until-ready` reports `OPEN_BLOCKED_BY_DIALOG_SAFE_MODE`.
3. Fix the syntax/type error in `Assets/_Game/Scripts/`.
4. Re-check readiness:
   ```powershell
   unity pipeline list
   unity status
   ```
