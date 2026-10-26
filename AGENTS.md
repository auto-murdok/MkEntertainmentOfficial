# AGENTS.md — Unity Project Guidelines & Live Editor Integration

This guide provides instructions and standards for AI agents working in this repository.

---

## 🛡️ Contribution Workflow — Hard Rules (owner-directed, 2026-10-25)

These rules are the pre-flight gate and operating contract for **any** contribution to this repo — human or AI agent. Rules 1–3 are checked before any work starts; if a gate fails, stop and report it to the repo owner. Do not work around a failed gate in another folder, another editor, or another clone.

1. **Main folder on the gaming PC, verified via SSH.** The canonical source of truth is GitHub; the ONE working checkout is `C:\murdokdev\MKEntertainmentOfficial` on the gaming PC, with branches switched in place. Before anything, verify over SSH that `git rev-parse --show-toplevel` in that folder returns exactly that path, and report branch, HEAD, and `git status`. Pre-existing changes in the checkout are the owner's — never clean them up silently. No per-task worktrees or extra clones; any other clone is a git-ops mirror only and must never hold unique unpushed state.
2. **No other Unity processes.** Every session, before any Unity work: list the running `Unity.exe` processes on the gaming PC, close ALL stale editors (any editor not actively needed for the task at hand), then open only the ONE editor the work needs, for the main folder. If a remaining Unity process is not a stale editor (e.g. the session-0 self-hosted CI runner batch process), do not kill it blind and do not silently proceed — stop and report it.
3. **Unity CLI, the proper way.** Drive the live Editor only over SSH straight to the gaming PC, using the PC's `unity.exe` CLI targeted at the main folder's Editor. Verify the chain every time: `unity status` → `ready`, `unity pipeline list` reachable, plus one real command. Follow the detail sections below: live-Editor work instead of hand-editing Unity YAML, C# via `eval_file` (never inline `eval`), no single wait longer than ~60 s, and restart a wedged Editor instead of debugging the hang.
4. **Gold-standard research before any implementation.** Before implementing, auditing, or refactoring, research the current gold standard for the pattern (see *Research Standard* below: context7 + web sources, under 1 month old preferred, max 3 months, undated sources labeled) and state the pattern being targeted. The ONLY exception is a direct owner order for a straightforward action — then execute exactly that action, with no scope expansion.
5. **Tests: 10-minute hard cap.** Run the test suite after every behaviour change (see *Testing* below) and keep it green: the suite is the specification of record — a failure counts against the change, and tests are never edited to bless new code. If a test run exceeds **10 minutes total**, stop waiting/polling/retrying, notify the repo owner immediately with the status so far, and hand it over for manual testing. Do not grind past 10 minutes without the owner's word.
6. **GitHub via the `gh` CLI, always.** All GitHub reads and writes for this repo — PR creation, merges, branch cleanup, CI/artifact reads — go through the `gh` CLI, never the GitHub MCP connector, so the owner is not prompted to approve GitHub actions. Changes land only through PRs (never a direct push to `main`), and PRs are merged via rebase (linear history) only on the owner's word.

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

The `unity-cli` (`unity`) interacts directly with the live Editor instance via the `com.unity.pipeline` package (running locally over IPC/WebSocket). Detailed workflows and cheat sheets are also in `docs/unity_cli_workflows.md`.

### 1. Launching & Verification
When opening or starting the Unity Editor for automated CLI workflows:
```bash
# Accept software terms headlessly (for containers / unattended CI)
unity terms accept 6000.0.47f1 --yes

# Open Editor with automated flag and block until ready (detects modal blockers)
unity open . --args "-automated" --until-ready --timeout 180

# Check whether Editor is connected and ready
unity status --format json
```
- Look for state `"ready"`.
- If multiple projects/editors are open, specify `--project-path .` (or absolute path).
- To pin one Editor among multiple instances or MPPM clones, pass `--port <number>`, `--pid <number>`, or `--instance <name>` (e.g. `--instance "Player 2"`).

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
# Enter / exit Play mode (modern command groups and legacy aliases both work)
unity command editor.playmode start     # or: unity command editor_play
unity command editor.playmode stop      # or: unity command editor_stop

# Read the Unity Console (supports --level error|warning and --tail N)
unity command console get --level error --tail 20   # or: unity command console --level error --tail 20
unity command clear_console

# Force script recompilation and poll until done
unity command recompile
unity command recompile_status

# Capture Game/Scene view screenshot
unity command capture game_view --source screen --save_path ./screenshot.png
unity command screenshot --output ./screenshot.png --width 1920 --height 1080

# Log messages to the Unity Console (command name is 'log', not 'log_editor')
unity command log "Message from agent"
```
Note: `unity logs` reads the **Unity Hub CLI** log, NOT the Editor console. For
Editor log output use `unity command console` (or tail
`%LOCALAPPDATA%\Unity\Editor\Editor.log` with `FileShare.ReadWrite` — the file
is locked while the Editor runs).

> **The `--` Flag Fence**: When passing arguments that share names with CLI options (such as `timeout`), put pipeline arguments after a `--` fence so the CLI does not swallow them:  
> `unity command script eval -- --code 'return UnityEngine.Time.frameCount;' --timeout 15000`

### 4. Play-Mode Verification Loop (MANDATORY)

> **CRITICAL**: Entering Play mode and taking a screenshot is **setup, not proof**. Unity throttles `EditorApplication.update` when the Editor window is unfocused, meaning an unfocused Editor can freeze on frame 1 while `status` still reports `playing`, and a screenshot will capture the frozen frame.

**Always run the verified Play-Mode loop:**
1. Keep the Editor ticking while unfocused: `unity command editor set_auto_tick`
2. Allow background execution: `unity command editor.playmode set_run_in_background`
3. Enter Play mode: `unity command editor.playmode start`
4. **Prove frame progression**: `unity command job wait -- --condition '{"member":"UnityEngine.Time.frameCount","op":"changed"}' --timeout_s 10`
5. Capture game view: `unity command capture game_view --source screen --save_path Screenshots/verify.png` (use `--source camera` if the desktop is locked)
6. Check console errors: `unity command console get --tail 20 --level error`
7. Safely exit Play mode: `unity command editor.playmode stop`

> **Automated Script**: Run `./Tools/playmode-loop.ps1` to execute this entire loop in one command.

### 5. Live C# Evaluation — ALWAYS use `eval_file` or `./Tools/eval-cs.ps1`

> **CRITICAL:** Passing C# inline through PowerShell breaks on (a) embedded
> double quotes, (b) `-` at token starts (parsed as CLI flags), (c) char
> literals like `' '` (PowerShell merges/mangles them), and (d) single quotes.
> WSL does not fix this either (`wsl.exe` joins args without re-quoting).

**The reliable pattern:** use `./Tools/eval-cs.ps1` or write the C# to a temp `.cs` file, then run it.

```powershell
# Option A: One-line safe execution using repo helper (auto-escapes via temp file):
./Tools/eval-cs.ps1 -Code 'return UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;'
"return UnityEngine.Time.frameCount;" | ./Tools/eval-cs.ps1

# Option B: Manual temp file via eval_file:
unity command eval_file --file "C:\Users\<user>\AppData\Local\Temp\task.cs"
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

### 6. Comparing objects / prefabs safely
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
3. In `1.0.0-beta.14`, `unity status` explicitly detects and reports:
   - `STATUS_BLOCKED_BY_DIALOG_SAFE_MODE` (quotes compile errors directly).
   - `STATUS_BLOCKED_BY_DIALOG_GRAPHICS_API` (Auto Graphics API prompt).
   - `STATUS_BLOCKED_BY_DIALOG_SOFTWARE_TERMS` (License Software Terms).
   `unity open . --until-ready` reports the corresponding `OPEN_BLOCKED_BY_DIALOG_*` code.

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

### 🕐 Responsiveness rules (user-mandated)
- **Never wait more than ~60 seconds on any single command or poll.** Long
  operations (builds, test runs, bakes) are async via unity-cli: poll
  `*_status` in short increments (≤60 s per wait) instead of one long sleep.
- **If the Editor wedges** (main-thread unity-cli commands time out while the
  process sits idle at 0% CPU — typically a modal dialog or stuck gate),
  **restart the Editor immediately** instead of debugging the hang:
  `Stop-Process -Id <pid> -Force` (all assets must be saved first), then
  `unity open . --args "-automated"` and wait for `unity status` → `ready`.

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
  - `Core/UI/`: Character UI controller/elements, `PlayerHud` (visible gameplay HUD: HP, ammo, combat ticker), `DebugHud` (hidden F3 diagnostics overlay), `MainMenuController` (menu canvas + start/host/join/quit flow), `PauseMenuController` (Esc/Start overlay in arenas: resume / quit-to-menu with session shutdown).
  - `Core/GameState/`: `GameStateManager` — game flow (Playing → GameOver): consumes `PlayerDiedChannel`, shows the game-over screen, disables spawning via `SpawningEnabledChannel`, reloads the main menu.
  - `Items/`: ItemCatalog (SO asset), weapons, firearm events and gun contexts, `AmmoPickup` (zombie ammo drops / reserve refill).
- **ScriptableObject architecture (modularity):** entities never reach out to scene objects or static singletons.
  - `InteractableRegistry` (SO, RuntimeSet pattern): every actor prefab references the shared asset (`Assets/_Game/Data/Registries/InteractableRegistry.asset`) and self-registers/unregisters; bite interactions go through `registry.Interact(...)`.
  - `ItemCatalog` (SO): `CharacterLocomotion` references `Assets/_Game/Data/Items/ItemCatalog_Default.asset` directly — no PrefabManager singleton.
  - **Event channels (SO):** `VoidEventChannel` / `BoolEventChannel` (`Core/Events/`) decouple producers from consumers — player death (`PlayerDiedChannel.asset`) and the spawning toggle (`SpawningEnabledChannel.asset`). The composition root (`PlayerSpawner`) injects channel refs; consumers subscribe via their channel property (setter subscribes immediately + unsubscribes the old one; `OnDisable` cleans up).
  - `GameStateManager` is a plain component created and wired by the composition root (no static `Instance`).
  - **Game flow:** scenes are `MainMenu` and the arena. `GameStateManager` consumes `PlayerDiedChannel` → GameOver (game-over UI, cursor release, time freeze), raises `SpawningEnabledChannel` so `ZombieSpawner` stops, and reloads the menu; `MainMenuController` fades and loads the arena scene.
  - **Networking (milestone 3):** `NetworkedCombatArena` is the NGO playground (`NetworkManager` + `UnityTransport`; roles via the menu **HOST/JOIN GAME** buttons (localhost 127.0.0.1:7777, `NetworkSession.desiredMode`) or the command line — no args hosts, `-mlclient`/`-client` joins). `NetworkManager.PlayerPrefab` = `FemaleCharacter` (carries `NetworkObject` + **owner-authoritative** `NetworkTransform` (`AuthorityMode=Owner` — required for client movement; keep server authority for future server-simulated NPCs) + **owner-authoritative** `NetworkAnimator` (params/states sync automatically; triggers MUST route through `CharacterLocomotion.SetAnimatorTrigger` — `Animator.SetTrigger` never replicates; remote copies get `applyRootMotion=false`, the FSM disabled, and a rebuilt rig graph pointing at a local `RemoteAimTarget` — aim visuals replicate via `NetworkVariable<bool> _isAiming`) + `NetworkedPlayerComposition` — the owner composes the local rig in `OnNetworkSpawn`; remote players must early-out of input wiring). **Zombies are server-simulated** (`ZombieSpawner` spawns on the host with `NetworkObject.Spawn`; `NetworkTransform`/`NetworkAnimator` stay server-authoritative — zombie triggers route via `ZombieContext.SetAnimatorTrigger`; `NetworkedZombieController` disables the FSM on clients and despawns on death while the host keeps the ragdoll). **Health is server-authoritative** (`NetworkedHealth` server-write `NetworkVariable` + `ActorBrainBase.MirrorHitPoints` on peers) and **so is bullet damage** (`BulletProjectile` → `NetworkedDamage.Apply` → `NetworkedDamageRelay.ReportDamageServerRpc` on the victim; the shooter never applies HP locally in a session). **Bites replicate via the owner relay** (`IPlayerBiteRelay` → targeted ClientRpc to the victim's owner, whose FSM runs take-bite and replicates through its NetworkAnimator; the owner mirrors bite state back via owner-write NetworkVariables so `IBiteTarget` checks stay correct on the host; zombie `isPreparing` is server-replicated for the victim's pin phase). **Death replication**: mirrored deaths on FSM-less remote copies must call `ActorBrainBase.RunDeathTeardown()` (the Dead state never fires there); `GameStateManager.freezeTimeOnGameOver` is false in networked scenes (freezing one peer's time streams a frozen standing ragdoll to everyone); `RagdollUtils.EnableRagdoll` wakes + topples the standing skeleton (a balanced pose is a stable physics tower). **Zombie corpses** linger for `ZombieData.corpseDestroyDelay` (10 s) on every peer (no instant despawn) and dead actors move off the `LocalPlayer` layer in `OnRagdollEnabled` (the AI vision scan is layer-based — corpses must not be scannable). **Overlays gate input** (`IPlayerInputGate`/`PlayerInputGate` on the composition root, injected into the pause menu + GameStateManager): pause/game-over disable the `PlayerInput` and zero held values; `CharacterBrain` re-evaluates held sprint/aim from the live action state every frame (event-latched held states stick if a release is ever missed). Ragdoll teardown must **disable** (never destroy) the `NetworkAnimator` and `Animator` — destroying NetworkBehaviours on one peer shifts NGO's RPC routing indices ("NetworkBehaviour index out of bounds" NREs), and a disabled-but-alive Animator is safe for the NetworkAnimator's poll. Client builds: `unity command build` → run `Builds/NetworkClient.exe -mlclient`. The old `ZombieCombatArena` scene was removed. **Full command cheat-sheet + gotchas: `docs/networking_notes.md`.**
- **Player spawning architecture (composition root):**
  - Scenes contain **only** map + MainCamera (CinemachineBrain + `MousePosition` child) + baked NavMesh Surface + `PlayerSpawner`.
  - `PlayerSpawner.Awake` instantiates `FemaleCharacter` (the spawnable player prefab, with `NetworkObject` + `NetworkTransform` for the networked arena), `InputHandler`, and `PlayerCoreComponents` prefabs and wires ALL cross-references on the **instances** (never on prefab assets — mutating a prefab asset at runtime corrupts it for every future spawn).
  - Unity **strips prefab → scene references** on save. Any scene object a prefab needs must be re-injected at spawn time. See `docs/spawnable_player_requirements.md`.
  - **Animation Rigging:** `RigBuilder` builds its graph during `Instantiate`. After changing constraint data (e.g. `MultiAimConstraint.sourceObjects`) at runtime, call `rigBuilder.Clear(); rigBuilder.Build();` or the constraints silently ignore the new data. Details: `docs/spawnable_player_rigging_fixes.md`.
  - `PlayerCoreUI._aimTarget` is the **Crossair UI toggle**, not the world aim point; the world aim point is the `AimTarget` child of `PlayerCoreComponents`.
- **Shooting engine:**
  - **AIM FIRST:** the weapon's rest pose points **down**. Shooting without aiming fires along the muzzle's rest forward — bullets go into the ground. This is intended; the aim direction (`HandgunContext.aimDirection` toward `CharacterLocomotion._aimTarget`) is only meaningful while the crosshair is active. Details + debug workflow: `docs/shooting_engine_notes.md`.
  - Bullets are pooled (`ObjectPool<BulletProjectile>`), teleported via `Rigidbody.position` (never transform-only on re-activated bodies), use `ContinuousSpeculative` CCD, and score exactly one damage event per flight (`_hasHit` / `_isReleased` guards). Pooled bodies are **posed before activation** and `OnCollisionEnter` rejects stale contacts behind the bullet's velocity (see `BulletProjectile.Launch` + `docs/testing.md` §5) — re-firing must never score a phantom hit on the previous target.
  - `DebugHud` (F3 toggle, attached by `PlayerSpawner`) shows player HP, FSM states, clip/reserve, live bullets and the `CombatLog` ring buffer — the fastest way to diagnose combat issues.
  - **Ammo economy is finite:** `Weapon._reserveAmmo` (default 45) feeds the handgun context on `Prepare`; `AmmoPickup` grants reserve and zombies drop it (`ZombieData.ammoDropPrefab` → `ZombieBrain`); dry fire never consumes a clip round. `HandgunContext.reserveAmmo` still defaults to `int.MaxValue` (= infinite) as the struct default — gameplay always overrides it via `Weapon`.
- **Conventions:**
  - Follow standard C# naming conventions (PascalCase for public methods/properties, camelCase / `_camelCase` for private fields).
  - Always maintain corresponding `.meta` files when creating, moving, or deleting C# scripts and assets.
  - Test state machine changes incrementally.

---

## 🧪 Testing (Unity Test Framework — keep the suite green)

The project has a regression suite in `Assets/_Game/Tests/` (EditMode + PlayMode assemblies, 204 tests: 86 EditMode + 118 PlayMode). **Run it after every behaviour change** and **add/extend tests for any new gameplay logic** — see `docs/testing.md` for the full guide, coverage map and gotchas.

```bash
# via unity-cli while the Editor is open (poll test_status until "completed")
unity command run_tests --mode editmode --async_tests
unity command run_tests --mode playmode --async_tests
unity command test_status
```

Rules of thumb (details in `docs/testing.md`):
- **EditMode** (`Game.Tests.EditMode`) for pure logic/statics; **PlayMode** (`Game.Tests.PlayMode`) for anything needing `Awake`/`Start`/`Update`, physics, or singletons.
- In the Editor, `AddComponent` never runs `Awake`, and closed-generic MonoBehaviours cannot be added — test generic bases through non-generic subclasses (`TestFsm : StateMachine<...>`).
- PlayMode tests run in the currently open scene — spawn physics tests in clear airspace, one "lane" per test, and clean up singletons with `DestroyImmediate`.
- Set private serialized fields via `SerializedObject`; assert error logs with `LogAssert.Expect`.

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

