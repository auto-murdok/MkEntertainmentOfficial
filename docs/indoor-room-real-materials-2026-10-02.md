# Indoor Room — Real Materials Rebuild — 2026-10-02

The Blender test room (`_blender/tests/indoor_room.blend`, assembled from the
Building_kit interior pieces) rendered with flat placeholder colors and LOD
stripe artifacts. This session rebuilt it with the kit's real materials.
All work done with headless Blender 5.2.2 LTS; Unity-side validation is still
pending (see "Validation limits").

## 1. Real materials restored — DONE, in this PR

**Problem:** The room assembly had collapsed every LOD0 mesh to a single
placeholder material (`wall` / `floor`) and flattened every face's material
index to 0, so the wallpaper/wainscoting/floor split was lost. All LOD1–LOD5
and UCX copies (41 extra meshes) were also left render-visible, which produced
the stripe artifacts in the screenshots.

**Fix:** `_blender/tools/rebuild_indoor_room.py` (new) rebuilds the scene
without touching geometry:

- Harvests the canonical LOD0 material slots and per-face material indices
  from the source FBXs and copies them onto the room's LOD0 meshes. Each room
  mesh is first verified against its canonical mesh by per-face area ratios
  (invariant under rigid transforms); ≥99% of faces must match within 2%.
- Rebuilds the five materials — `MI_Wallpaper`, `MI_Wood_Wainscoting`,
  `MI_Wood_InternalFloor`, `MI_Inner_WoodFloor`, `MI_Ceiling` — from
  `_blender/polish/matlib_building_kit.json` using the same node recipe as
  `polish_render.py` (albedo + normal + ORM pack).
- Deletes the 41 LOD1–5 / UCX meshes.
- Fixes a units bug in the source scene: the FBX empties carry 0.01 scale
  while the cameras and lights sit at meter scale, so the room rendered as a
  speck. Empties are rescaled to 1.0 in the rebuilt file only — the tracked
  source scene still has the bug (see §3).
- Texture paths are stored relative to the .blend, so it opens in any
  checkout.

**Output:** `_blender/tests/indoor_room_real.blend`; proof renders
`_blender/polish/indoor_room_overview.png`, `indoor_room_inside_01.png`,
`indoor_room_inside_02.png` (the scene's own three cameras, Cycles, scene
lighting unchanged).

**Known deviation:** one of DoorFrame01's 379 faces measures ~10% larger in
area than the source FBX (the other 378 match exactly). It kept the canonical
`MI_Wallpaper` assignment. Worth a look in Unity — the room copy of the frame
may have been tweaked during assembly.

## 2. Gaps between pieces — MEASURED, not fixed

Raycast coverage check against the rebuilt scene (rays cast through each wall
line every 5 cm at heights z = 1.0 / 2.5 / 4.0 m; room floor spans
x 0–6.7 m, y 0–6.4 m). The pieces do not fully close the room:

- **South-west corner:** the south wall line (y ≈ −0.3) has no geometry for
  x 0.0–0.8 at any sampled height — a ~0.8 m hole west of the door frame.
  (The x 2.1–4.8 opening is the arched doorway itself, by design; there is
  no door leaf or threshold piece in it, and the floor slab's joist-like edge
  is exposed at the threshold — visible in the overview render.)
- **North-west corner:** the west wall stops at y ≈ 6.05; the strip
  y 6.05–7.0 is open above wainscot height — the corner is not closed
  (~0.95 m).
- **North-east corner:** same on the east wall — open strip y 6.05–7.0
  (~0.95 m).
- **South-east corner:** the east wall line has no geometry for y −0.6–0.0
  (~0.6 m).
- **North wall is fully closed** at every sampled height.
- Consistent with the gaps, a bright light-leak line shows at the base of the
  wall in `indoor_room_inside_01.png`.

**Suggested fix (follow-up, not in this PR):** the side walls appear placed
one segment short at the north end, and the south run is missing a short
wall03/wall01 segment west of the door frame. Repositioning/adding pieces
belongs in the room assembly step, then re-run the coverage check.

## 3. Also missing / open

- **No ceiling.** Nothing closes the room at the top (walls rise to ~5.8 m);
  the overview camera looks straight in. `MI_Ceiling` faces in Floor01 are
  the slab's underside, not a room ceiling. Add a ceiling/roof piece if the
  test room needs one.
- **Units bug in the tracked source scene.** `_blender/tests/indoor_room.blend`
  still carries the 0.01 empty scales. The rebuild fixes it only in the new
  file. Decide whether to fix the source scene too.
- **Wallpaper pattern phase shift** at wall-segment joints (visible on the
  north wall in `indoor_room_inside_01.png`). This comes from the kit's UV
  layout; in Unity these materials are world-aligned, so confirm in-engine
  whether the shift appears there before treating it as a defect.
- **Lighting is test-only** (one point light + one sun from the source
  scene). The renders are material checks, not game lighting.

## Validation limits

Blender-only, like the rest of the polish work: nothing here was opened in
Unity. The material restoration is verified geometrically (per-face area
profiles) and visually (the three proof renders), but the in-engine look —
especially the wallpaper seams under world-aligned mapping and the one
deviating DoorFrame01 face — needs a check on the Unity machine.
