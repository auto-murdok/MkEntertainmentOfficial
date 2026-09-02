---
name: ue-commandlet-python
description: Automate Unreal Editor via headless pythonscript commandlets (UnrealEditor-Cmd -run=pythonscript, unreal python) - use when writing or debugging unreal python scripts, UE commandlets, batch asset export/audit scripts, or when UE python logs, exceptions, API renames, or env-variable issues appear. Covers boot flags, log sinks, detach patterns, UE 5.8 API renames, and the probe-first method.
---

# UE pythonscript commandlet automation

Headless UE automation runs `UnrealEditor-Cmd.exe "<uproject>" -run=pythonscript
-script="<script.py>" -unattended -nop4 -nosplash -stdout`. Add
`-AllowCommandletRendering` only when the script renders (SceneCapture etc.).
This is how the UEI pipeline (`Tools/UEImport/`) runs its exporter; these
patterns are engine-version-hardened against UE 5.8.

## The rules that keep this from being painful

1. **Log sinks are unreliable — use markers.** `unreal.log()` may land in
   redirected stdout OR the project log (`<uproject>\Saved\Logs\<Proj>.log`,
   falling back to `<Proj>_2.log` when the primary is locked by a live editor).
   Never decide success from `print()` visibility: end scripts with a greppable
   marker (`UEI EXPORT DONE ok=N fail=N`) and read BOTH sinks.
2. **Environment variables do not inherit reliably** — commandlets and
   WMI-spawned processes get a fresh environment. The working pattern
   (Run-Export.ps1): PowerShell writes a per-run bootstrap `.py` that sets
   `os.environ[...]` then `exec(compile(open(r'<generic script>').read(), ...))`.
3. **Shell tool timeouts kill the whole process tree** — anything that can run
   >2 min must be launched detached via
   `Invoke-CimMethod Win32_Process Create 'cmd.exe /c "<run.cmd>"'` where the
   `.cmd` redirects output to a file itself (no inherited pipes). Poll the log
   in ≤60 s cycles for markers; the runner exiting without markers = failure.
4. **Probe before you call.** UE python API surface shifts every release.
   `unreal.log('PROBE ' + str([m for m in dir(obj) if ...]))` before using any
   property/method, and guard calls with `hasattr`/try-except. Python `print()`
   does not surface in commandlet output — always `unreal.log`.

## UE 5.8 python API renames / gotchas (each one cost debugging time)

| Old / expected | UE 5.8 reality |
|---|---|
| `StaticMaterial.material` | `material_interface` |
| `TextureParameterValue.parameter_name` | `parameter_info` (nested) `.name` |
| `AssetTools.run_asset_export_task` | gone from AssetTools — `run_asset_export_task` lives on exporter instances (`unreal.StaticMeshExporterFBX()`, `unreal.TextureExporterPNG()`, ...) |
| `MaterialEditingLibrary.get_material_texture_parameter_names` | `get_texture_parameter_names`; value getter `get_material_default_texture_parameter_value` |
| `AssetData.object_path` | `package_name` (getattr fallback chain recommended) |
| `actor.set_actor_rotation(rot)` | `set_actor_rotation(rot, False)` — teleport_physics arg required |
| `unreal.TextureRenderTargetFormat.TF_*` | members are `RTF_*` (e.g. `RTF_RGBA8_SRGB`) |
| `RenderingLibrary.create_render_target_2d` | `create_render_target2d` (lowercase 2d), 5-arg form `(world, w, h, fmt, clear)` |
| `RenderTarget2D.export(file)` | `export_to_disk(filename, options)` needs an options object — prefer `RenderingLibrary.export_render_target(world, rt, path, name)` (path/name split) |
| `unreal.RenderTarget2D` | `unreal.TextureRenderTarget2D` |
| `MI.get_editor_property('two_sided')` on instance | walks nothing — walk the `parent` chain to the base Material |
| `MaterialInstanceConstant.TextureParameterValues[i].ParameterValue` | `FPackageIndex` — resolve with `.Load<UTexture>()` (CUE4Parse) or `.parameter_value` object directly (UE python) |

## Commandlet world & rendering

- A pythonscript commandlet has **no world by default**: load one with
  `unreal.EditorLoadingAndSavingUtils.load_map('/Engine/Maps/Entry')` (tiny
  engine map, never saved) before spawning actors
  (`EditorActorSubsystem.spawn_actor_from_class` — `spawn_actor_from_object`
  returns None in commandlets; class-based spawn works).
- SceneCapture G-buffer capture (`SCS_BASE_COLOR`, `SCS_NORMAL`) works, but
  **materials render without their textures** — commandlets never tick, so
  texture streaming and async shader compilation never complete (master
  recompiles, residency forcing and `r.TextureStreaming 0` do not help). Do
  not build texture bakes on this; it was attempted and rolled back
  (see `docs/ue_content_import.md`).
- No roughness/metallic capture source exists (`SCS_*` list: BASE_COLOR,
  NORMAL, FINAL_COLOR_*, SCENE_COLOR_*, *_DEPTH).

## Debugging loop

1. Run the commandlet, then read the newest `Saved\Logs\*.log` (primary or
   `_2`) filtering for the script's marker prefix.
2. Python tracebacks appear as interleaved `LogPython: Error:` lines — the
   failing line number matches the script on disk at run time.
3. Script-level try/except that logs the traceback
   (`traceback.print_exc()`) beats relying on log-only reporting.
