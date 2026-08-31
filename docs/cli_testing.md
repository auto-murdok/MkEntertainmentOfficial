# CLI Testing — Automated Agent Harness

The game is entirely testable via CLI args (Editor and built Player).
No clicks or menu navigation required — an automated agent controls scene,
networking role, gameplay overrides, and lifetime from the command line.

Single source of truth: `Assets/_Game/Scripts/Core/Cli/GameCliArgs.cs`
(`Game.Core`). Every system reads the same normalized parser.

## Gold standard

- **Unity Manual – Command line arguments** (batchmode, `-executeMethod`,
  `Environment.GetCommandLineArgs`, `Application.isBatchMode`).
- **NGO quickstart / Boss Room** for the networking role split.
- Normalized keys: leading dashes stripped, case-insensitive, both
  `-key value` and `--key=value` accepted. No hand-rolled `GetCommandLineArgs`
  loops elsewhere.

## Bootstrap

`Assets/_Game/Scripts/Composition/GameCliBootstrap.cs` is created via
`RuntimeInitializeOnLoadType.BeforeSceneLoad` (a hidden DontDestroyOnLoad
object), so it runs before any `Awake/Start`:

- logs `--help` and exits (`Application.Quit` / `EditorApplication.Exit`) in batchmode
- handles `--verbose` dump
- pushes networking overrides into `NetworkSession` before
  `NetworkArenaBootstrap.Start`
- redirects to `--scene <name>` when the active scene differs
- handles `--autoStart` in `MainMenu`
- schedules auto-quit (`--autoQuit`/`--quitAfter`/`--maxDuration`/etc.)
- applies gameplay overrides after each `sceneLoaded`
  (`--noSpawning`, `--maxZombies`, `--spawnInterval`, `--timeScale`, `--seed`,
  `--godMode`, `--infiniteAmmo` — the last two read directly in
  `ActorBrainBase`/`Weapon`/`HandgunReloadingState`)

`NetworkSession` now exposes mutable `OverrideAddress/OverridePort` +
`EffectiveAddress/EffectivePort` and `ResetOverrides()` (tests). Legacy
consts remain for source compatibility.

`NetworkArenaBootstrap.IsCommandLineClient()` delegates to `GameCliArgs`
so `--mode client|host` and legacy `-mlclient`/`-client` stay consistent.
`ClientLaunchRedirect` defers to `GameCliBootstrap` when `--scene` is set.

## CLI reference

`--help` / `-h` prints this list at runtime:

```
Game CLI (automated agent harness)
  --help, -h                 Show help.
  --verbose, -v              Verbose dump of parsed args.
  --scene <name>             Load scene by short name (MainMenu | ExpandedCombatArena |
                             NetworkedCombatArena | <any in Build Settings>).
  --mode <host|client|auto>  Network session role (overrides menu + legacy flags).
  --host                     Host (alias for --mode host).
  --client, --mlclient       Join as client (alias for --mode client). Legacy kept.
  --connect <host:port>      Override address/port (default 127.0.0.1:7777).
  --address <host> --port <n>  Split form of --connect.
  --autoStart                Skip MainMenu and start immediately.
  --autoQuit <seconds>       Auto-quit after N seconds (aliases: --quitAfter,
                             --exitAfter, --maxDuration, --duration).
  --godMode                  Player ignores damage.
  --noSpawning               Disable ZombieSpawner.
  --maxZombies <n>           Override max zombie count.
  --spawnInterval <f>        Override spawn interval (seconds).
  --infiniteAmmo             Infinite reserve ammo.
  --timeScale <f>            Override Time.timeScale.
  --seed <n>                 Random seed for determinism.
  --automated                Mark run as automated (implies batchmode).
```

Notes:

- All keys are case-insensitive, accept one or two dashes, and allow `=` or
  space: `-scene=X` == `--scene X`.
- Unity reserved flags (`-batchmode`, `-nographics`, `-projectPath`, `-logFile`,
  `-executeMethod`, `-quit`, `-buildTarget`, `-runTests`, `-testResults`, etc.)
  are parsed but ignored unless they carry a game-relevant meaning (`isBatchMode`
  feeds `IsAutomated`).
- Menu choice wins only after it has run; on boot, CLI mode wins over `Auto`.

## Agent recipes

**Headless smoke** (no window, auto-exit, deterministic):

```
Unity.exe -batchmode -projectPath . -scene ExpandedCombatArena -noSpawning -seed 123 -autoQuit 10 -quit -logFile smoke.log
```

**Host + client on one box** (two processes, localhost):

```
Builds/Official.exe --scene NetworkedCombatArena --mode host --autoQuit 60 --verbose
Builds/Official.exe --scene NetworkedCombatArena --mode client --connect 127.0.0.1:7777 --autoQuit 60 --verbose
# legacy alias still works:
Builds/NetworkClient.exe -mlclient -screen-width 960 -screen-height 540
```

**Fast iteration in Editor** (skips menu, no zombies, infinite ammo):

```
Unity.exe -projectPath . --scene NetworkedCombatArena --mode host --autoStart --noSpawning --infiniteAmmo --godMode
```

**CI – run Unity Test Framework suites**:

```
unity test --mode editmode --output test-edit.xml
unity test --mode playmode --output test-play.xml
# or via Editor CLI:
Unity.exe -batchmode -projectPath . -runTests -testPlatform EditMode -testResults test-edit.xml -quit
```

**Show help and exit** (batchmode):

```
Builds/Official.exe --help
Unity.exe -batchmode -projectPath . --help -quit
```

## Exit codes & logs

- Default auto-quit exits `0`.
- `--help` in batchmode exits `0` after printing.
- Unity batchmode exits non-zero on unhandled exceptions (standard `1`).
- `Application.isBatchMode` sends a minimal console log; tails live in
  `Editor.log` (Editor) or `%USERPROFILE%/AppData/LocalLow/DefaultCompany/Official/Player.log` (Player) and in any `-logFile` you pass.
- Use `--verbose` to echo the normalized parse at startup.

## Test seam

`GameCliArgs.SetArgsForTesting(params string[])` and `ResetForTesting()` are
public for EditMode tests. `Game.Tests.EditMode.GameCliArgsTests` covers the
parser, scene/mode/connect aliases, auto-quit gameplay overrides, help,
`NetworkArenaBootstrap.IsCommandLineClient` delegation, and
`NetworkSession.EffectiveAddress/Port`. Call `Initialize()` after a reset to
re-bind the live process args.

## Files

- `Assets/_Game/Scripts/Core/Cli/GameCliArgs.cs` — parser + typed conveniences + help
- `Assets/_Game/Scripts/Composition/GameCliBootstrap.cs` — runtime side-effects
- `Assets/_Game/Scripts/Core/NetworkSession.cs` — mutable address/port + reset
- `Assets/_Game/Scripts/Composition/NetworkArenaBootstrap.cs` — delegates to `GameCliArgs`
- `Assets/_Game/Scripts/Composition/ClientLaunchRedirect.cs` — legacy redirect defers to bootstrap
- `Assets/_Game/Scripts/Characters/ActorBrainBase.cs` — `--godMode` guard
- `Assets/_Game/Scripts/Items/Weapon.cs` / `HandgunReloadingState.cs` — `--infiniteAmmo`
- `Assets/_Game/Tests/EditMode/Core/GameCliArgsTests.cs` — coverage
