# Blender Tests

Standalone Blender test scenes built from the repo's 3D content, for visual
inspection outside Unity. Nothing here is imported by the game — this folder
lives outside `Assets/` on purpose.

## indoor_room

A single room assembled from the building kit to verify the kit pieces
actually build a coherent indoor space:

- Floor: `SM_Wood_Internal_Floor01` (6.7 x 6.35 m)
- Walls: `SM_Internal_Wall_wall01` / `wall03` (5.5 m tall), with a
  `SM_Internal_Wall_DoorFrame01` entrance on the south wall

Files:

- `indoor_room.blend` — the assembled scene. Open it on your main machine to
  orbit/walk through the room.
- `build_indoor_room.py` — regenerates the scene + renders headlessly:
  `blender --background --python build_indoor_room.py`
- `renders/` — screenshot output of the script (regenerated on each run).

### Known issues when opening the .blend

- Each kit FBX ships LOD0–LOD5 plus `UCX_*` collision meshes. The headless
  build leaves the extra LODs and UCX objects in the scene (deleting them in
  background mode corrupts rendering). In the Blender UI, select and delete
  all `*_LOD1`…`*_LOD5` and `UCX_*` objects for a clean scene.
- Materials are flat placeholder colors. The real materials (wallpaper,
  wainscoting, wood floor) are applied in Unity via the WorldAlignedLit
  shader and are not reproduced here.
- The floor can show stripe artifacts in the headless renders from leftover
  LOD meshes z-fighting; this goes away once the LOD clutter is deleted.
