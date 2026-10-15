# UEImport (Unity side — thin remainder)

The UE→FBX pipeline moved to `blender-playground/tools/ueimport/` on
2026-10-15 (repo split: Blender + importers live there). Run exports from
that checkout; outputs still land next to the `.uproject`.

What stays here:

- `unity/run_import.cs`, `unity/verify_import.cs` — headless Unity-side
  re-import / material-binding dump via `unity command eval_file`
  (see sibling `.opencode/skills/ue-unity-import/SKILL.md`).
- `Assets/ImportedContent/` — the menu importer
  (`Tools ▸ UE Import ▸ Import FBX Folder...`), URP materials, shaders.

Full pipeline docs: `docs/ue_content_import.md`.
