# Blender workspace

All Blender-side working material for this project, consolidated in one
place (outside `Assets/`, so Unity never imports it).

| Folder | Contents |
|---|---|
| `tools/` | Headless Blender/Python scripts: FBX inspection (`inspect_*.py`), the polish render pipeline (`polish_render.py`), and the material-library builder (`build_matlib.py`). |
| `polish/` | Output of the 2026-10 mesh-polish sessions: QA renders (`renders/`), material libraries (`matlib_building_kit.json`, `matmap_*.json`, `fbx_slots.json`), packed roughness textures, and repaired normal maps (`repaired_normals/`). |
| `tests/` | Standalone test scenes (`.blend` + build script + renders) for visually checking the 3D content outside Unity. See `tests/README.md`. |

Toolchain: Blender 4.0.2 portable (headless `--background --python`),
trimesh, ImageMagick. Everything runs locally.
