# UEI - Unreal -> Unity content import pipeline

Batch-convert Unreal `StaticMesh` assets to Unity-ready FBX + textures + a
material manifest, then import them into this Unity project with materials
wired to URP Lit.

**Full documentation: [`docs/ue_content_import.md`](../../docs/ue_content_import.md)**

## Quickstart

```powershell
# Leg #1 (preferred - needs the source .uproject):
Tools\UEImport\Run-Export.ps1 -UProject "C:\...\MyProject.uproject" -AssetPath "/Game/Mansion/Mesh/Assets/Building_kit"
# optional: apply configured UV fixes for meshes with rotated UV authoring
Tools\UEImport\Apply-UvFixes.ps1
# then in Unity: Tools > UE Import > Import FBX Folder... -> pick the output folder

# Leg #2 (no editor UI; needs a cook first):
Tools\UEImport\cook\Start-Cook.ps1 -UProject "C:\...\MyProject.uproject"
Tools\UEImport\cue4parse\setup-prereqs.ps1
Tools\UEImport\cue4parse\Convert-Cooked.ps1 -CookedContent "C:\...\Saved\Cooked\Windows\MyProject\Content" -Filter "Building_kit" -OutDir "C:\...\Exports\cue4parse_fbx"

# end of a headless session: kill the -automated editor + helpers
Tools\UEImport\Cleanup-Runtime.ps1
```

## Layout

```
Run-Export.ps1            leg #1: UE commandlet -> FBX + PNG + manifest
ue/export_fbx_and_map.py  the commandlet script (launched by Run-Export.ps1)
Apply-UvFixes.ps1         optional: rotate UVs for meshes with rotated UV authoring
uvfixes.json              UV-fix config (mesh + material + degrees)
ue/uvfix.py               the Blender headless patch script
cook/Start-Cook.ps1       loose-file cook (-skipzenstore) for leg #2
cue4parse/                leg #2: cooked -> glTF -> FBX (+ setup-prereqs.ps1)
unity/run_import.cs       headless re-import via unity-cli eval
unity/verify_import.cs    headless material-binding dump via unity-cli eval
Cleanup-Runtime.ps1       kill the -automated editor tree + helpers after sessions
vendor/                   gitignored prerequisites (dotnet/blender/CUE4Parse)
```

Unity-side pieces this tooling feeds: `Tools ▸ UE Import ▸ Import FBX
Folder...` (`Assets/ImportedContent/Editor/UEContentImporter.cs`) and the
`UEI/WorldAlignedLit` shader (`Assets/ImportedContent/Shaders/`).
