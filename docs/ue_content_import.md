# UE Content Import â€” Unreal â†’ Unity pipeline (UEI)

Batch-convert Unreal Engine `StaticMesh` assets to Unity-ready FBX + textures +
a material manifest, then import them into this project with URP/Lit materials
wired automatically. Works for any UE5 project, any `/Game/...` asset or
folder.

**Tool location:** `tools/ueimport/` in the sibling `blender-playground` repo (moved 2026-10-15; run the commands below from that checkout). Only `Tools/UEImport/unity/` and `Assets/ImportedContent/` stay here.
**Unity entry point:** `Tools â–¸ UE Import â–¸ Import FBX Folder...`

---

## Prerequisites

| Component | Needed for | Notes |
|---|---|---|
| Unreal Engine 5.x + the source `.uproject` | Leg #1, cooking | Engine auto-discovered from the `.uproject` `EngineAssociation` |
| Portable .NET 10 SDK | Leg #2 | `setup-prereqs.ps1` installs to `tools/ueimport/vendor/dotnet` in the sibling playground repo (gitignored) |
| Portable Blender 5.2.2 LTS | Blender MCP/CLI + leg #2 | glb->FBX conversion; `setup-prereqs.ps1` installs to `tools/ueimport/vendor/blender` |
| CUE4Parse sources | Leg #2 | `setup-prereqs.ps1` clones to `tools/ueimport/vendor/CUE4Parse` |

## Which leg do I need?

| Situation | Use |
|---|---|
| You have the UE project that owns the assets | **Leg #1** (UE-native FBX export â€” best material fidelity) |
| You only have a cooked/packaged build, or want full automation without opening the editor | **Leg #2** (cook â†’ CUE4Parse â†’ glTF â†’ Blender â†’ FBX) |
| You have loose **uncooked** `.uasset` files and *no* source project | Not possible for geometry â€” uncooked mesh data lives in the editor/DCC pipeline; CUE4Parse only converts **cooked** mesh render data. Bring the assets into a UE project first, then use leg #1. |

## Step-by-step walkthrough

### Leg #1 â€” from the UE project (recommended)

1. **Export** â€” in the repo root:
   ```powershell
   tools\ueimport\Run-Export.ps1 `
       -UProject "C:\Users\me\Documents\Unreal Projects\MyProject\MyProject.uproject" `
       -AssetPath "/Game/Mansion/Mesh/Assets/Building_kit" `
       -Filter "roof"          # optional: only names containing "roof"
   ```
   Wait for `UEI EXPORT OK`. Output: `...\MyProject\Exports\Building_kit\` with
   one `.fbx` per mesh, one `.png` per referenced texture, and
   `import_manifest.csv`.
2. **Optional: apply UV fixes** â€” if the config `tools\ueimport\uvfixes.json`
   has entries for the exported meshes (kit meshes whose UVs are authored
   rotated), patch them before importing:
   ```powershell
   tools\ueimport\Apply-UvFixes.ps1        # idempotent; rotates from .orig backups
   ```
   Flip `degrees` to âˆ’90 in the config if the corrected orientation is wrong.
3. **Import into Unity** â€” with the editor open:
   `Tools â–¸ UE Import â–¸ Import FBX Folder...` â†’ pick
   `...\MyProject\Exports\Building_kit`.
   Wait for the `[UEImport] DONE ...` console line (progress bar shown).
4. **Verify** (optional, headless):
   ```powershell
   unity command eval_file --file Tools\UEImport\unity\run_import.cs      # re-run import
   unity command eval_file --file Tools\UEImport\unity\verify_import.cs   # dump material bindings
   ```
   Edit the folder/kit constants at the top of each script first.
5. **Use it** â€” prefabs live at
   `Assets/ImportedContent/<KitName>/Prefabs/<Mesh>.prefab` (FBX sources under
   `Meshes/`, materials under `Materials/`, textures under `Textures/`, ORM
   packs under `Generated/`). Drag prefabs into scenes as needed.
6. **Re-import after art changes** â€” repeat steps 1â€“3; everything is
   overwritten in place, material GUIDs are preserved, so scene references
   survive.
7. **Kill the headless runtime when done** â€” headless sessions leave a Unity
   editor (`-automated`) and the unity-cli identity helper running:
   ```powershell
   tools\ueimport\Cleanup-Runtime.ps1            # add -DryRun to just list
   ```
   Deterministically kills the `-automated` editor + its child tree, the
   identity helper, and any bun server (word-bounded match, so unrelated
   paths containing "bundle" are never hit). Never touches interactive
   editors, Unity Hub, or opencode's MCP servers.

### Leg #2 â€” from a cooked build (no editor UI)

1. **Prerequisites (once)**: `tools\ueimport\cue4parse\setup-prereqs.ps1`
   (portable .NET 10 + Blender + CUE4Parse into gitignored `vendor/`).
2. **Cook**: `tools\ueimport\cook\Start-Cook.ps1 -UProject "C:\...\MyProject.uproject"`
   â†’ wait for `UEI COOK OK` and note the cooked content path it prints.
3. **Convert**: `tools\ueimport\cue4parse\Convert-Cooked.ps1 -CookedContent "<printed path>" -Filter "Building_kit" -OutDir "C:\...\Exports\cue4parse_fbx"`
   â†’ wait for `UEI CONVERT OK`.
4. **Import** â€” same as leg #1 step 2 (the folder shape is identical).
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
  come before parent-chain defaults â€” take the FIRST occurrence per
  (material, param)**.
- `texture_path` is the UE object path (leg #1) or provider path (leg #2);
  the Unity importer currently keys off `texture` names.

## Leg #1 â€” UE direct export (preferred)

```powershell
# a whole folder (all static meshes at that /Game path):
tools\ueimport\Run-Export.ps1 -UProject "C:\Users\me\Documents\Unreal Projects\MyProject\MyProject.uproject" `
    -AssetPath "/Game/Mansion/Mesh/Assets/Building_kit"

# a single mesh, custom output:
tools\ueimport\Run-Export.ps1 -UProject "...\MyProject.uproject" `
    -AssetPath "/Game/Mansion/Mesh/Assets/Building_kit/SM_Roof01" `
    -OutDir "D:\Exports\roof_test"
```

Default output: `<uproject>\Exports\<AssetPathLeaf>\` (the leaf doubles as the
Unity kit name). The script launches `UnrealEditor-Cmd.exe -run=pythonscript`
**detached** (WMI), polls the log, verifies FBX + manifest, and prints a
summary. Safe to re-run; exports are overwritten.

## Leg #2 â€” cook + CUE4Parse (no editor UI)

```powershell
# 1. cook to loose files (UE 5.6+ defaults to Zen storage, which nothing
#    external can read; -skipzenstore forces classic loose output):
tools\ueimport\cook\Start-Cook.ps1 -UProject "C:\...\MyProject.uproject"

# 2. one-time prerequisites (portable dotnet 10 + blender + CUE4Parse -> vendor\):
tools\ueimport\cue4parse\setup-prereqs.ps1

# 3. convert:
tools\ueimport\cue4parse\Convert-Cooked.ps1 `
    -CookedContent "C:\...\MyProject\Saved\Cooked\Windows\MyProject\Content" `
    -Filter "Building_kit" `
    -OutDir "C:\...\MyProject\Exports\cue4parse_fbx"
```

`--game`/`-Game` defaults to `GAME_UE5_8` â€” match it to the engine version the
project cooks with. Leg-#2 quality expectations: geometry identical to leg #1,
but materials come from **cooked** material instances; plain `Material` assets
expose no readable texture parameters, so some materials may end up untextured
(the importer logs every unwired slot). Use leg #1 when fidelity matters.

## Unity import

1. `Tools â–¸ UE Import â–¸ Import FBX Folder...`
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

Texture import rules by name suffix: `_N` â†’ normal map (linear), `_ORM`/`_EM`/
`_M`/`_MASK` â†’ default + linear, everything else â†’ default + sRGB. Import size
follows the documented category budgets:
- Modular kits & architecture: capped to **2048Ã—2048 max**
- Props & shelves: capped to **1024Ã—1024 max**
- Ammo pickups & small items: capped to **512Ã—512 max**
- Packed ORM / mask maps: scaled to **1024Ã—1024 max** (half-res rule)
- Standalone platform format: **BC5** for normals, **BC7** for albedo and packed ORMs.
- Physical source PNGs exceeding target resolution on disk are automatically downsampled to prevent Git blob / Git LFS bloat.

Material wiring is **layer-aware**: these UE master materials are layered, with
texture parameters grouped by a numeric prefix (`00_BaseColor`,
`04_Grunge_BaseColor`, `08_VCOL_BaseColor_A`, `12_AO_BaseColor`, ...). The
importer ignores placeholder defaults (`T_Base_*`, `T_Default_*`, any
`/Engine/...` texture) and picks the parameter layer whose BaseColor texture is
the **most detailed** â€” PNG byte size is the detail proxy (a flat tint at
2048Â² compresses to ~78 KB while a detailed concrete at the same resolution is
~7.8 MB), which is the better single-layer approximation of UE's blended
result. That layer's Normal/ORM come along with it (falling back across layers
per channel; first manifest occurrence wins within a layer, so instance
overrides beat parent defaults). Materials whose every parameter is a
placeholder are logged as flat.

Two generated texture variants live under `Generated/`:

- `<ORM>_Pack.png` â€” URP channel pack (R=metallic from ORM.B, G=AO from ORM.R,
  A=smoothness from 1âˆ’ORM.G)
- `<Normal>_N_Unity.png` â€” the normal map with the **green channel inverted**
  (UE stores DirectX-style Yâˆ’ normals; Unity expects OpenGL-style Y+). The
  flipped copy is what gets wired to `_BumpMap`.

**Two-sided handling**: imported kit materials render **both faces** (`_Cull
Off`) â€” UE's master materials here are not flagged TwoSided, but modular kit
pieces that are mirrored/negatively-scaled in UE get their triangle winding
flipped when FBX export bakes the transform, which Unity otherwise culls from
the side that should be visible ("part of the model is transparent but not in
UE"). The exporter records each material's actual `two_sided` flag in the
manifest; the Unity importer's `ForceTwoSided` option (on by default) renders
both faces regardless.

**Glass materials**: slots whose material name contains `glass` (e.g.
`MI_Glass_Window_Roof`) have no texture parameters in the manifest â€” in UE they
are flat translucent panes. The importer configures them as URP transparent
(alpha-blended 0.35 tint, smoothness 0.9, ZWrite off, transparent queue) with
no textures; the `[UEImport] DONE ... errors=1` notice about a missing base
color for glass is expected and harmless.

Channel packing per URP version:

- URP shader exposes `_MaskMap` â†’ mask map assigned directly.
- Otherwise (e.g. URP in Unity 6000.3): `_MetallicGlossMap` (R=metallic,
  A=smoothness) + `_OcclusionMap` (G=AO) + `_GlossMapScale=1` and the
  `_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A` keyword disabled.

**World-Aligned Snapping Shader (`UEI/WorldAlignedLit`)**:
For modular architecture pieces (external bricks, concrete walls, interior wallpaper, wainscoting panels, floors, ceilings), `UEContentImporter` automatically assigns `UEI/WorldAlignedLit` (`Assets/ImportedContent/Shaders/WorldAlignedLit.shader`).
- Implements world-space triplanar coordinates mirroring Unreal's `MM_Buildings_WorldAligned` and `MF_WorldAligned_BaseMAterial`.
- Prevents visible seams and orientation breaks when modular walls or floors meet.
- Texture scale (`_TextureSize` in cm) is automatically tuned by surface category (e.g. 300 cm for exterior bricks/concrete, 250 cm for floors/ceilings, 200 cm for interior wallpaper/wainscoting).
- Forward+, SRP Batcher, and GPU Resident Drawer compatible.

**Modular Prefab Pivot Calibration (BottomCenter Snapping Standard)**:
In Unreal, modular meshes are often authored with pivots at origin/corners. When exported to Unity, this causes rotation-induced shifts and awkward fractional placement.
- `UEContentImporter` applies automatic pivot normalization via `CenterModularPivots = true` (`RecenterPrefabToBottomCenter`).
- The prefab root transform sits at the exact **BottomCenter** (`X = 0`, `Y = 0` at floor level, `Z = 0` at wall midline).
- Child meshes (`SM_*`) and colliders (`UCX_*`) are shifted inside the prefab so physics, visual meshes, and LODs stay 100% aligned.
- **Snapping Invariant**: Walls of width $W$ (2m, 4m, 6m, 10m) snap deterministically with $X_{next} = X_{curr} + \frac{W_{curr} + W_{next}}{2}$. Rotating pieces by 90Â° or 180Â° rotates around their exact center in-place without coordinate displacement.

**UCX collision**: meshes named `UCX_*` inside imported FBXs automatically get
their renderer disabled and a convex `MeshCollider` (postprocessor scoped to
`Assets/ImportedContent/` â€” it does not touch assets elsewhere in the project).

Re-running with the same folder is **idempotent**: assets are overwritten in
place, material asset GUIDs are preserved.

## Level layout dump + batch export (mansion estate)

Beyond single-folder mesh export, two scripts cover whole-estate work:

**Actor inventory** (`ue/export_level_actors.py` via `Run-ExportActors.ps1`):

```powershell
tools\ueimport\Run-ExportActors.ps1 -UProject "C:\...\MyProject.uproject" `
    -MapPath "/Game/Mansion/Levels/LV_Haunted_Mansion"
# -> <uproject>\Exports\level_actors\<Map>_actors.json
```

Loads the map in a commandlet and records every visible actor: label, class,
mesh + mesh path, location (cm), rotator, scale, plus light
(intensity/color/attenuation/cones) and CineCamera (focal/sensor) properties.
`kind` is `staticmesh`, `instanced` (ISM/HISM with per-instance transforms),
or `other`. **World Partition caveat**: actors that are not streamed in are
invisible to the dump â€” compare the total against the map's
`__ExternalActors__` file count; if short, rerun with the map open in the
editor UI. StaticMeshActors with no readable mesh are flagged `mesh_missing`
(missing/renamed assets â€” verified skips, not export bugs).

**Batch mesh export** (`ue/export_fbx_batch.py` via `Run-ExportBatch.ps1`):

```powershell
tools\ueimport\Run-ExportBatch.ps1 -UProject "C:\...\MyProject.uproject" `
    -AssetPaths @("/Game/.../Books", "/Game/.../Sconce01") `
    -OutDir "C:\...\Exports\MansionProps"
```

Loops many folders in **one** commandlet boot into a single accumulating
manifest (avoids ~2 min boot per folder). Per-folder failures don't stop the
batch; verify FBX count + manifest rows after (a silent mid-list death shows
up as missing tail folders â€” rerun just those into a second dir, manifests
merge). Trim folder strings (a trailing newline breaks the generated
bootstrap). The JSON dump doubles as a **spawn manifest** for rebuilding the
level elsewhere: mesh + transform per actor, pivots UE-authored.

The playground consumes both: `blender-playground/docs/mansion-rebuild.md`
records the Blender-side faithful rebuild (2,615 actors placed via MCP).

## Troubleshooting / hard-won gotchas

| Symptom | Cause / fix |
|---|---|
| Cook finishes but `Saved\Cooked\...\Content` has almost no files | UE 5.6+ cooked into Zen storage. Cook with `-skipzenstore` (Start-Cook.ps1 does this). |
| `run_asset_export_task` missing on AssetTools | UE 5.8 moved it onto the exporter classes (`StaticMeshExporterFBX.run_asset_export_task`). |
| Python property errors on `StaticMaterial.material` / `parameter_name` | UE 5.8 renames: use `material_interface` and `parameter_info.name`. |
| Textures missing from export | Materials reference textures outside the kit folder. Leg #1 resolves every referenced texture via the asset registry â€” re-run with the current tool. |
| Wrong/flat textures on some slots | Layered master material: the instance's real textures sit under a different parameter layer (e.g. `04_Grunge_BaseColor`) than the master defaults (`00_BaseColor` â†’ `T_Base_*` placeholders). The importer's layer-aware picker handles this; if a material still wires placeholders, check the manifest rows for that material. |
| **Bumpy lighting looks inverted vs UE** | UE normal maps are DirectX-style (Yâˆ’), Unity expects OpenGL (Y+) â€” the importer wires green-flipped `_N_Unity` copies. Don't replace them with the originals. |
| **Texture orientation wrong on some faces** (e.g. bricks vertical instead of horizontal on `SM_ExternalWall_Baseflor_wall01` / `WindowFrame01`/`03`, fine in UE) | **Root cause is the source mesh**: submeshes contain a mixture of correctly-oriented and rotated UV0 faces (authored with vertical UV gradients or mixed during kit assembly). UE hides it because the kit master `MM_Building` projects its detailed grunge layer in **world space** (`WorldAlignedTexture` / `WorldAlignedNormal` â€” verified by graph traversal); Unity's standard URP/Lit samples mesh UV0 faithfully. **Two complementary solutions available**: (1) **`UEI/WorldAlignedLit` shader** (`Assets/ImportedContent/Shaders/WorldAlignedLit.shader`), which projects textures in world space coordinates identically to UE's `MM_Buildings_WorldAligned` and `MF_WorldAligned_BaseMAterial`, providing seamless snapping without UV seams; (2) **`tools\ueimport\Apply-UvFixes.ps1`**, where headless Blender computes 3D differential tangents ($\vec{T}_U, \vec{T}_V$) per polygon in `mode: "auto"` per `uvfixes.json` and rotates only faces where $U$ is vertical around $(0.5, 0.5)$ for UV-based rendering. |
| **Baking layered materials (attempted & dropped)** | A G-buffer bake (`SceneCaptureComponent2D` + `SCS_BASE_COLOR` on a plane in a commandlet) was implemented and rolled back: the commandlet's SceneCapture renders materials **without their textures** (flat tint output, ~79 KB 2048Â² PNGs) regardless of master recompiles, texture-residency forcing, or `r.TextureStreaming 0` â€” commandlets never tick, so nothing streams/compiles into the capture. Layered materials therefore use the most-detailed-layer approximation. Pixel-perfect would require baking inside a live editor or a unique-UV baker (out of scope). |
| Part of the model see-through from one side (fine in UE) | Flipped triangle winding from mirrored/negatively-scaled kit pieces baked at FBX export (or a genuinely TwoSided UE material). Importer renders both faces (`_Cull Off`) by default â€” see Two-sided handling above. |
| Unity material has base+normal but no metallic/occlusion | URP in Unity 6000.3 has **no `_MaskMap`** property; the importer detects this and uses `_MetallicGlossMap` + `_OcclusionMap`. Don't hand-add `_MaskMap` on this URP version. |
| `unity command eval_file` reports "Main thread operation timed out" | The eval's 5 s HTTP budget expired; long operations still complete on the main thread. Verify via console log markers instead of the exit code. |
| Long-running UE commandlet killed when a shell tool times out | Shell kills its whole process tree. The PS scripts launch via `Win32_Process.Create` (WMI) â€” detached, no inherited pipes. Keep using that pattern for anything that can exceed ~2 min. |
| CUE4Parse: "Mesh has no LOD data" | You pointed it at **uncooked** editor assets. Cook first (`Start-Cook.ps1`) and point `--content` at the cooked output. |
| CUE4Parse NuGet restore fails on net8/net9 | Current CUE4Parse targets **net10.0** only; use the SDK from `setup-prereqs.ps1`. |

## Scope / limitations

- Static meshes only (no skeletal meshes, animations, niagara, levels).
- Leg #1 exports FBX without embedded texture references (UE 5.8 dropped the
  FBX "export textures" option) â€” textures always come as sibling PNGs, wired
  by the Unity importer via the manifest.
- Material graphs are not translated â€” only texture parameters plus the
  fixed URP/Lit property mapping above. Materials that build color procedurally
  (grunge blending etc.) will look approximate; bake them in UE if fidelity is
  critical.
