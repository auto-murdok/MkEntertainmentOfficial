---
name: ue-unity-import
description: Import or investigate Unreal Engine .uasset/.umap content into this Unity project (Mansion/Building_kit style meshes) - use when the user mentions uasset, Unreal, UE, CUE4Parse, cooked/cook, FBX export, glTF, import Unreal assets, or textures/materials from UE kits. Covers the decision tree, exact commands, and known pitfalls.
---

# UE → Unity content import (UEI pipeline)

Batch-convert Unreal `StaticMesh` content to FBX + textures + a material
manifest, then import into Unity with URP/Lit materials wired. Full reference:
`docs/ue_content_import.md`. Tooling: `Tools/UEImport/`. Unity menu:
`Tools ▸ UE Import ▸ Import FBX Folder...` (or headless via the eval helpers
in `Tools/UEImport/unity/`).

## Decide the route first (investigate before acting)

1. Does the user have the **source .uproject** that owns the assets?
   → Use **leg #1** (`Run-Export.ps1`). Best quality. This is almost always
   the right answer.
2. Only a **cooked build** (or full automation, no editor UI)?
   → **leg #2**: `Start-Cook.ps1` + `Convert-Cooked.ps1` (needs `setup-prereqs.ps1`
   once). Material wiring is best-effort: cooked plain `Material` assets expose
   no readable texture parameters in CUE4Parse; `MaterialInstanceConstant`
   params may or may not deserialize depending on UE version.
3. Only **loose uncooked .uasset files, no project**?
   → Geometry extraction is **impossible** without the engine or a cook
   (CUE4Parse deliberately skips `RenderData` for non-filtered editor packages;
   see `UStaticMesh.cs` early-return in the CUE4Parse source). Say so and ask
   for the project or a cook. Do NOT burn time trying CUE4Parse on them.

To probe an unknown `.uasset`: read the first bytes — tag `9E 2A 83 C1`,
`LegacyFileVersion` (-8 = UE5), and search ASCII for `++UE5+Release-<ver>` /
`++UE4+Release-<ver>` to get the engine version. A single `.uasset` with no
`.uexp/.ubulk` siblings and `/Script/UnrealEd` imports = **uncooked editor
asset** (route 3 unless the owning project exists).

## Leg #1 commands

```powershell
Tools\UEImport\Run-Export.ps1 `
  -UProject "C:\Users\me\Documents\Unreal Projects\MyProject\MyProject.uproject" `
  -AssetPath "/Game/<path>/<to>/<folder>" `   # folder or single mesh
  -Filter "roof"                               # optional name filter
# then Unity: Tools > UE Import > Import FBX Folder... -> pick the printed OutDir
```

Engine is auto-discovered from the `.uproject` `EngineAssociation`. Output
default: `<uproject>\Exports\<AssetPathLeaf>\` — that leaf doubles as the
Unity kit name (`Assets/ImportedContent/<KitName>/`).

## Leg #2 commands

```powershell
Tools\UEImport\cue4parse\setup-prereqs.ps1                  # once (vendor/, gitignored)
Tools\UEImport\cook\Start-Cook.ps1 -UProject "C:\...\<Proj>.uproject"
Tools\UEImport\cue4parse\Convert-Cooked.ps1 `
  -CookedContent "C:\...\<Proj>\Saved\Cooked\Windows\<Proj>\Content" `
  -Filter "Building_kit" -OutDir "C:\...\<Proj>\Exports\cue4parse_fbx"
```

Match `-Game`/`--game` (default `GAME_UE5_8`) to the cooking engine version.

## Hard-won pitfalls (do not rediscover these)

- UE 5.6+ **cooks to Zen storage by default** → external tools see nothing.
  Always cook with `-skipzenstore` (Start-Cook.ps1 already does).
- UE 5.8 Python renames: `StaticMaterial.material_interface`,
  `TextureParameterValue.parameter_info.name`, `run_asset_export_task` lives on
  exporter classes (not AssetTools), `AssetData` has no `object_path` (use
  `package_name`). The shipped `ue/export_fbx_and_map.py` handles all of this.
- UE 5.8 FBX export embeds **no texture references** — textures travel as
  sibling PNGs and get wired from the manifest by the Unity importer.
- Unity 6000.3 URP Lit has **no `_MaskMap`** — the importer detects this and
  uses `_MetallicGlossMap` (R=metal, A=smooth) + `_OcclusionMap` (G=AO) +
  `_GlossMapScale=1`. Don't hand-add `_MaskMap`.
- Unity 6.3 URP occlusion channel is **G** (see LitInput.hlsl), metallic map is
  **R/A**. The ORM pack (R=metal, G=AO, A=1-rough) feeds both slots.
- Long UE commandlets must be launched **detached via WMI**
  (`Win32_Process.Create`, pattern in Run-Export.ps1/Start-Cook.ps1) — shell
  tool timeouts kill child process trees otherwise.
- `unity command eval_file` has a **5 s main-thread budget**; long imports keep
  running anyway — verify via the `[UEImport] DONE` log line, not the exit
  code.
- Textures referenced by kit materials often live **outside** the kit folder;
  leg #1 resolves them via the asset registry (never copy just the local
  Textures subfolder).
- Kit master materials are **layered** — an instance's real textures may sit
  under `04_Grunge_*` / `08_VCOL_*` / `12_AO_*` params while `00_BaseColor`
  holds flat placeholder defaults (`T_Base_*`, tiny 160-byte PNGs). The Unity
  importer picks the first non-placeholder layer; don't "fix" it back to
  `00_*`.
- Kit parts **see-through from one side only** (fine in UE) = flipped triangle
  winding from mirrored/negatively-scaled pieces baked at FBX export (UE
  masters here are NOT flagged TwoSided). The importer renders both faces
  (`_Cull Off`, `ForceTwoSided`) - don't set kit materials back to Cull Back.
- UE normal maps are DirectX-style: the importer wires green-flipped
  `Generated/<name>_N_Unity.png` copies. The originals in `Textures/` are for
  reference only.

## Verification

- Export: wait for `UEI EXPORT OK` / `UEI CONVERT OK`; then check the output
  folder has `*.fbx`, `*.png`, `import_manifest.csv`.
- Import: `[UEImport] DONE ... errors=N` console line; then
  `unity command eval_file --file Tools\UEImport\unity\verify_import.cs`
  (edit `KIT_NAME` first) — every slot must show non-NULL base/normal/metal.
- Tests: EditMode suite must stay green (`unity command run_tests --mode
  editmode --async_tests`, poll `test_status`).
