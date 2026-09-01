# UE Content Import — Unreal → Unity pipeline (UEI)

Batch-convert Unreal Engine `StaticMesh` assets to Unity-ready FBX + textures +
a material manifest, then import them into this project with URP/Lit materials
wired automatically. Works for any UE5 project, any `/Game/...` asset or
folder.

**Tool location:** `Tools/UEImport/` (repo root — not compiled by Unity)
**Unity entry point:** `Tools ▸ UE Import ▸ Import FBX Folder...`

---

## Prerequisites

| Component | Needed for | Notes |
|---|---|---|
| Unreal Engine 5.x + the source `.uproject` | Leg #1, cooking | Engine auto-discovered from the `.uproject` `EngineAssociation` |
| Portable .NET 10 SDK | Leg #2 | `setup-prereqs.ps1` installs to `Tools/UEImport/vendor/dotnet` (gitignored) |
| Portable Blender 4.2 | Leg #2 | glb→FBX conversion; `setup-prereqs.ps1` installs to `vendor/blender` |
| CUE4Parse sources | Leg #2 | `setup-prereqs.ps1` clones to `vendor/CUE4Parse` |

## Which leg do I need?

| Situation | Use |
|---|---|
| You have the UE project that owns the assets | **Leg #1** (UE-native FBX export — best material fidelity) |
| You only have a cooked/packaged build, or want full automation without opening the editor | **Leg #2** (cook → CUE4Parse → glTF → Blender → FBX) |
| You have loose **uncooked** `.uasset` files and *no* source project | Not possible for geometry — uncooked mesh data lives in the editor/DCC pipeline; CUE4Parse only converts **cooked** mesh render data. Bring the assets into a UE project first, then use leg #1. |

## Step-by-step walkthrough

### Leg #1 — from the UE project (recommended)

1. **Export** — in the repo root:
   ```powershell
   Tools\UEImport\Run-Export.ps1 `
       -UProject "C:\Users\me\Documents\Unreal Projects\MyProject\MyProject.uproject" `
       -AssetPath "/Game/Mansion/Mesh/Assets/Building_kit" `
       -Filter "roof"          # optional: only names containing "roof"
   ```
   Wait for `UEI EXPORT OK`. Output: `...\MyProject\Exports\Building_kit\` with
   one `.fbx` per mesh, one `.png` per referenced texture, and
   `import_manifest.csv`.
2. **Import into Unity** — with the editor open:
   `Tools ▸ UE Import ▸ Import FBX Folder...` → pick
   `...\MyProject\Exports\Building_kit`.
   Wait for the `[UEImport] DONE ...` console line (progress bar shown).
3. **Verify** (optional, headless):
   ```powershell
   unity command eval_file --file Tools\UEImport\unity\run_import.cs      # re-run import
   unity command eval_file --file Tools\UEImport\unity\verify_import.cs   # dump material bindings
   ```
   Edit the folder/kit constants at the top of each script first.
4. **Use it** — prefabs live at `Assets/ImportedContent/<KitName>/<Mesh>.prefab`
   (materials under `Materials/`, textures under `Textures/`, ORM packs under
   `Generated/`). Drag prefabs into scenes as needed.
5. **Re-import after art changes** — repeat steps 1–2; everything is
   overwritten in place, material GUIDs are preserved, so scene references
   survive.

### Leg #2 — from a cooked build (no editor UI)

1. **Prerequisites (once)**: `Tools\UEImport\cue4parse\setup-prereqs.ps1`
   (portable .NET 10 + Blender + CUE4Parse into gitignored `vendor/`).
2. **Cook**: `Tools\UEImport\cook\Start-Cook.ps1 -UProject "C:\...\MyProject.uproject"`
   → wait for `UEI COOK OK` and note the cooked content path it prints.
3. **Convert**: `Tools\UEImport\cue4parse\Convert-Cooked.ps1 -CookedContent "<printed path>" -Filter "Building_kit" -OutDir "C:\...\Exports\cue4parse_fbx"`
   → wait for `UEI CONVERT OK`.
4. **Import** — same as leg #1 step 2 (the folder shape is identical).
5. Remember the quality note: leg-#2 material wiring is best-effort (cooked
   materials expose fewer readable parameters). Prefer leg #1 when available.

Both legs produce the **same output folder shape**, and the same Unity importer
consumes either.

## Output folder shape (both legs)

```
<OutDir>\
  <MeshName>.fbx
  <TextureName>.png            every texture referenced by exported materials
  import_manifest.csv          mesh,slot,material,param,texture,texture_path
```

Manifest notes:
- Texture parameter rows repeat per (material, param); **instance overrides
  come before parent-chain defaults — take the FIRST occurrence per
  (material, param)**.
- `texture_path` is the UE object path (leg #1) or provider path (leg #2);
  the Unity importer currently keys off `texture` names.

## Leg #1 — UE direct export (preferred)

```powershell
# a whole folder (all static meshes at that /Game path):
Tools\UEImport\Run-Export.ps1 -UProject "C:\Users\me\Documents\Unreal Projects\MyProject\MyProject.uproject" `
    -AssetPath "/Game/Mansion/Mesh/Assets/Building_kit"

# a single mesh, custom output:
Tools\UEImport\Run-Export.ps1 -UProject "...\MyProject.uproject" `
    -AssetPath "/Game/Mansion/Mesh/Assets/Building_kit/SM_Roof01" `
    -OutDir "D:\Exports\roof_test"
```

Default output: `<uproject>\Exports\<AssetPathLeaf>\` (the leaf doubles as the
Unity kit name). The script launches `UnrealEditor-Cmd.exe -run=pythonscript`
**detached** (WMI), polls the log, verifies FBX + manifest, and prints a
summary. Safe to re-run; exports are overwritten.

## Leg #2 — cook + CUE4Parse (no editor UI)

```powershell
# 1. cook to loose files (UE 5.6+ defaults to Zen storage, which nothing
#    external can read; -skipzenstore forces classic loose output):
Tools\UEImport\cook\Start-Cook.ps1 -UProject "C:\...\MyProject.uproject"

# 2. one-time prerequisites (portable dotnet 10 + blender + CUE4Parse -> vendor\):
Tools\UEImport\cue4parse\setup-prereqs.ps1

# 3. convert:
Tools\UEImport\cue4parse\Convert-Cooked.ps1 `
    -CookedContent "C:\...\MyProject\Saved\Cooked\Windows\MyProject\Content" `
    -Filter "Building_kit" `
    -OutDir "C:\...\MyProject\Exports\cue4parse_fbx"
```

`--game`/`-Game` defaults to `GAME_UE5_8` — match it to the engine version the
project cooks with. Leg-#2 quality expectations: geometry identical to leg #1,
but materials come from **cooked** material instances; plain `Material` assets
expose no readable texture parameters, so some materials may end up untextured
(the importer logs every unwired slot). Use leg #1 when fidelity matters.

## Unity import

1. `Tools ▸ UE Import ▸ Import FBX Folder...`
2. Pick the output folder (`...\Exports\<KitName>`).
3. Result lands in `Assets/ImportedContent/<KitName>/`:

```
Assets/ImportedContent/<KitName>\
  <MeshName>.fbx                  imported model
  <MeshName>.prefab               prefab with URP materials assigned by slot
  Materials\<Material>.mat        URP/Lit, one per unique manifest material
  Textures\<Texture>.png          import rules by suffix (see below)
  Generated\<ORM>_Pack.png        ORM channel pack (cached)
  Generated\<Normal>_N_Unity.png  green-flipped normal (wired to _BumpMap)
```

Texture import rules by name suffix: `_N` → normal map (linear), `_ORM`/`_EM`/
`_M`/`_MASK` → default + linear, everything else → default + sRGB. Import size
follows the source PNG resolution (power-of-two steps, capped at 4096).

Material wiring is **layer-aware**: these UE master materials are layered, with
texture parameters grouped by a numeric prefix (`00_BaseColor`,
`04_Grunge_BaseColor`, `08_VCOL_BaseColor_A`, `12_AO_BaseColor`, ...). The
importer ignores placeholder defaults (`T_Base_*`, `T_Default_*`, any
`/Engine/...` texture) and picks the parameter layer that has a real BaseColor,
taking that same layer's Normal/ORM with it (falling back across layers per
channel; first manifest occurrence wins within a layer, so instance overrides
beat parent defaults). Materials whose every parameter is a placeholder are
logged as flat.

Two generated texture variants live under `Generated/`:

- `<ORM>_Pack.png` — URP channel pack (R=metallic from ORM.B, G=AO from ORM.R,
  A=smoothness from 1−ORM.G)
- `<Normal>_N_Unity.png` — the normal map with the **green channel inverted**
  (UE stores DirectX-style Y− normals; Unity expects OpenGL-style Y+). The
  flipped copy is what gets wired to `_BumpMap`.

Channel packing per URP version:

- URP shader exposes `_MaskMap` → mask map assigned directly.
- Otherwise (e.g. URP in Unity 6000.3): `_MetallicGlossMap` (R=metallic,
  A=smoothness) + `_OcclusionMap` (G=AO) + `_GlossMapScale=1` and the
  `_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A` keyword disabled.

**UCX collision**: meshes named `UCX_*` inside imported FBXs automatically get
their renderer disabled and a convex `MeshCollider` (postprocessor scoped to
`Assets/ImportedContent/` — it does not touch assets elsewhere in the project).

Re-running with the same folder is **idempotent**: assets are overwritten in
place, material asset GUIDs are preserved.

## Troubleshooting / hard-won gotchas

| Symptom | Cause / fix |
|---|---|
| Cook finishes but `Saved\Cooked\...\Content` has almost no files | UE 5.6+ cooked into Zen storage. Cook with `-skipzenstore` (Start-Cook.ps1 does this). |
| `run_asset_export_task` missing on AssetTools | UE 5.8 moved it onto the exporter classes (`StaticMeshExporterFBX.run_asset_export_task`). |
| Python property errors on `StaticMaterial.material` / `parameter_name` | UE 5.8 renames: use `material_interface` and `parameter_info.name`. |
| Textures missing from export | Materials reference textures outside the kit folder. Leg #1 resolves every referenced texture via the asset registry — re-run with the current tool. |
| Wrong/flat textures on some slots | Layered master material: the instance's real textures sit under a different parameter layer (e.g. `04_Grunge_BaseColor`) than the master defaults (`00_BaseColor` → `T_Base_*` placeholders). The importer's layer-aware picker handles this; if a material still wires placeholders, check the manifest rows for that material. |
| Bumpy lighting looks inverted vs UE | UE normal maps are DirectX-style (Y−), Unity expects OpenGL (Y+) — the importer wires green-flipped `_N_Unity` copies. Don't replace them with the originals. |
| Unity material has base+normal but no metallic/occlusion | URP in Unity 6000.3 has **no `_MaskMap`** property; the importer detects this and uses `_MetallicGlossMap` + `_OcclusionMap`. Don't hand-add `_MaskMap` on this URP version. |
| `unity command eval_file` reports "Main thread operation timed out" | The eval's 5 s HTTP budget expired; long operations still complete on the main thread. Verify via console log markers instead of the exit code. |
| Long-running UE commandlet killed when a shell tool times out | Shell kills its whole process tree. The PS scripts launch via `Win32_Process.Create` (WMI) — detached, no inherited pipes. Keep using that pattern for anything that can exceed ~2 min. |
| CUE4Parse: "Mesh has no LOD data" | You pointed it at **uncooked** editor assets. Cook first (`Start-Cook.ps1`) and point `--content` at the cooked output. |
| CUE4Parse NuGet restore fails on net8/net9 | Current CUE4Parse targets **net10.0** only; use the SDK from `setup-prereqs.ps1`. |

## Scope / limitations

- Static meshes only (no skeletal meshes, animations, niagara, levels).
- Leg #1 exports FBX without embedded texture references (UE 5.8 dropped the
  FBX "export textures" option) — textures always come as sibling PNGs, wired
  by the Unity importer via the manifest.
- Material graphs are not translated — only texture parameters plus the
  fixed URP/Lit property mapping above. Materials that build color procedurally
  (grunge blending etc.) will look approximate; bake them in UE if fidelity is
  critical.
