# UEI - Unreal -> Unity content import pipeline

Batch-convert Unreal `StaticMesh` assets to Unity-ready FBX + textures + a
material manifest, then import them into this Unity project with materials
wired to URP Lit.

**Full documentation: [`docs/ue_content_import.md`](../../docs/ue_content_import.md)**

## Quickstart

```powershell
# Leg #1 (preferred - needs the source .uproject):
Tools\UEImport\Run-Export.ps1 -UProject "C:\...\MyProject.uproject" -AssetPath "/Game/Mansion/Mesh/Assets/Building_kit"
# then in Unity: Tools > UE Import > Import FBX Folder... -> pick the output folder

# Leg #2 (no editor UI; needs a cook first):
Tools\UEImport\cook\Start-Cook.ps1 -UProject "C:\...\MyProject.uproject"
Tools\UEImport\cue4parse\setup-prereqs.ps1
Tools\UEImport\cue4parse\Convert-Cooked.ps1 -CookedContent "C:\...\Saved\Cooked\Windows\MyProject\Content" -Filter "Building_kit" -OutDir "C:\...\Exports\cue4parse_fbx"
```

## Layout

```
Run-Export.ps1            leg #1: UE commandlet -> FBX + PNG + manifest
ue/export_fbx_and_map.py  the commandlet script (launched by Run-Export.ps1)
cook/Start-Cook.ps1       loose-file cook (-skipzenstore) for leg #2
cue4parse/                leg #2: cooked -> glTF -> FBX (+ setup-prereqs.ps1)
```
