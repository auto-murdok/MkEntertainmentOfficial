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
2. **Optional: apply UV fixes** — if the config `Tools\UEImport\uvfixes.json`
   has entries for the exported meshes (kit meshes whose UVs are authored
   rotated), patch them before importing:
   ```powershell
   Tools\UEImport\Apply-UvFixes.ps1        # idempotent; rotates from .orig backups
   ```
   Flip `degrees` to −90 in the config if the corrected orientation is wrong.
3. **Import into Unity** — with the editor open:
   `Tools ▸ UE Import ▸ Import FBX Folder...` → pick
   `...\MyProject\Exports\Building_kit`.
   Wait for the `[UEImport] DONE ...` console line (progress bar shown).
4. **Verify** (optional, headless):
   ```powershell
   unity command eval_file --file Tools\UEImport\unity\run_import.cs      # re-run import
   unity command eval_file --file Tools\UEImport\unity\verify_import.cs   # dump material bindings
   ```
   Edit the folder/kit constants at the top of each script first.
5. **Use it** — prefabs live at
   `Assets/ImportedContent/<KitName>/Prefabs/<Mesh>.prefab` (FBX sources under
   `Meshes/`, materials under `Materials/`, textures under `Textures/`, ORM
   packs under `Generated/`). Drag prefabs into scenes as needed.
6. **Re-import after art changes** — repeat steps 1–3; everything is
   overwritten in place, material GUIDs are preserved, so scene references
   survive.
7. **Kill the headless runtime when done** — headless sessions leave a Unity
   editor (`-automated`) and the unity-cli identity helper running:
   ```powershell
   Tools\UEImport\Cleanup-Runtime.ps1            # add -DryRun to just list
   ```
   Deterministically kills the `-automated` editor + its child tree, the
   identity helper, and any bun server (word-bounded match, so unrelated
   paths containing "bundle" are never hit). Never touches interactive
   editors, Unity Hub, or opencode's MCP servers.

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
follows the documented category budgets:
- Modular kits & architecture: capped to **2048×2048 max**
- Props & shelves: capped to **1024×1024 max**
- Ammo pickups & small items: capped to **512×512 max**
- Packed ORM / mask maps: scaled to **1024×1024 max** (half-res rule)
- Standalone platform format: **BC5** for normals, **BC7** for albedo and packed ORMs.
- Physical source PNGs exceeding target resolution on disk are automatically downsampled to prevent Git blob / Git LFS bloat.

Material wiring is **layer-aware**: these UE master materials are layered, with
texture parameters grouped by a numeric prefix (`00_BaseColor`,
`04_Grunge_BaseColor`, `08_VCOL_BaseColor_A`, `12_AO_BaseColor`, ...). The
importer ignores placeholder defaults (`T_Base_*`, `T_Default_*`, any
`/Engine/...` texture) and picks the parameter layer whose BaseColor texture is
the **most detailed** — PNG byte size is the detail proxy (a flat tint at
2048² compresses to ~78 KB while a detailed concrete at the same resolution is
~7.8 MB), which is the better single-layer approximation of UE's blended
result. That layer's Normal/ORM come along with it (falling back across layers
per channel; first manifest occurrence wins within a layer, so instance
overrides beat parent defaults). Materials whose every parameter is a
placeholder are logged as flat.

Two generated texture variants live under `Generated/`:

- `<ORM>_Pack.png` — URP channel pack (R=metallic from ORM.B, G=AO from ORM.R,
  A=smoothness from 1−ORM.G)
- `<Normal>_N_Unity.png` — the normal map with the **green channel inverted**
  (UE stores DirectX-style Y− normals; Unity expects OpenGL-style Y+). The
  flipped copy is what gets wired to `_BumpMap`.

**Two-sided handling**: imported kit materials render **both faces** (`_Cull
Off`) — UE's master materials here are not flagged TwoSided, but modular kit
pieces that are mirrored/negatively-scaled in UE get their triangle winding
flipped when FBX export bakes the transform, which Unity otherwise culls from
the side that should be visible ("part of the model is transparent but not in
UE"). The exporter records each material's actual `two_sided` flag in the
manifest; the Unity importer's `ForceTwoSided` option (on by default) renders
both faces regardless.

**Glass materials**: slots whose material name contains `glass` (e.g.
`MI_Glass_Window_Roof`) have no texture parameters in the manifest — in UE they
are flat translucent panes. The importer configures them as URP transparent
(alpha-blended 0.35 tint, smoothness 0.9, ZWrite off, transparent queue) with
no textures; the `[UEImport] DONE ... errors=1` notice about a missing base
color for glass is expected and harmless.

Channel packing per URP version:

- URP shader exposes `_MaskMap` → mask map assigned directly.
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
- **Snapping Invariant**: Walls of width $W$ (2m, 4m, 6m, 10m) snap deterministically with $X_{next} = X_{curr} + \frac{W_{curr} + W_{next}}{2}$. Rotating pieces by 90° or 180° rotates around their exact center in-place without coordinate displacement.

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
| **Bumpy lighting looks inverted vs UE** | UE normal maps are DirectX-style (Y−), Unity expects OpenGL (Y+) — the importer wires green-flipped `_N_Unity` copies. Don't replace them with the originals. |
| **Texture orientation wrong on some faces** (e.g. bricks vertical instead of horizontal on `SM_ExternalWall_Baseflor_wall01` / `WindowFrame01`/`03`, fine in UE) | **Root cause is the source mesh**: submeshes contain a mixture of correctly-oriented and rotated UV0 faces (authored with vertical UV gradients or mixed during kit assembly). UE hides it because the kit master `MM_Building` projects its detailed grunge layer in **world space** (`WorldAlignedTexture` / `WorldAlignedNormal` — verified by graph traversal); Unity's standard URP/Lit samples mesh UV0 faithfully. **Two complementary solutions available**: (1) **`UEI/WorldAlignedLit` shader** (`Assets/ImportedContent/Shaders/WorldAlignedLit.shader`), which projects textures in world space coordinates identically to UE's `MM_Buildings_WorldAligned` and `MF_WorldAligned_BaseMAterial`, providing seamless snapping without UV seams; (2) **`Tools\UEImport\Apply-UvFixes.ps1`**, where headless Blender computes 3D differential tangents ($\vec{T}_U, \vec{T}_V$) per polygon in `mode: "auto"` per `uvfixes.json` and rotates only faces where $U$ is vertical around $(0.5, 0.5)$ for UV-based rendering. |
| **Baking layered materials (attempted & dropped)** | A G-buffer bake (`SceneCaptureComponent2D` + `SCS_BASE_COLOR` on a plane in a commandlet) was implemented and rolled back: the commandlet's SceneCapture renders materials **without their textures** (flat tint output, ~79 KB 2048² PNGs) regardless of master recompiles, texture-residency forcing, or `r.TextureStreaming 0` — commandlets never tick, so nothing streams/compiles into the capture. Layered materials therefore use the most-detailed-layer approximation. Pixel-perfect would require baking inside a live editor or a unique-UV baker (out of scope). |
| Part of the model see-through from one side (fine in UE) | Flipped triangle winding from mirrored/negatively-scaled kit pieces baked at FBX export (or a genuinely TwoSided UE material). Importer renders both faces (`_Cull Off`) by default — see Two-sided handling above. |
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
