# Two-Story House from the Building Kit — 2026-10-02

A complete two-story house assembled in Blender from the building kit's own
pieces and real materials — the follow-up to the single-room test in
`docs/indoor-room-real-materials-2026-10-02.md`. The layout was designed from
standard residential construction practice first (public rooms down, bedrooms
up, wet rooms stacked in one plumbing core, stair beside the foyer, bearing
walls stacked, gable ridge parallel to the long axis), then fitted exactly
onto the kit's module grid — no wall piece is stretched or cut anywhere.

## Run it

```bash
blender --background --python _blender/tools/build_two_story_house.py
# -> _blender/tests/two_story_house.blend  (85 piece placements)
```

The tool resolves the repo root from `__file__` and stores texture paths
relative to the .blend, so the scene opens in any checkout. Materials are
rebuilt from `_blender/polish/matlib_building_kit.json` with the polish
node recipe (same approach as the indoor room and Roof01).

## The house

- Footprint 14.22 × 12.25 m on the kit's raised terrace platform (deck ≈ z 2.0),
  floor-to-floor 5.5 m, eaves z 13.0, gable ridge z 16.28.
- **Ground:** foyer (S-center), living SW, kitchen SE, dining NE, guest WC in
  the dining corner. Front door south, garden door east, entry stair to the
  terrace.
- **Stair:** `Internal_Stairs01` is a three-flight wrap — entry off the foyer,
  flights climb N → W → S, exit at the west end of the upper hall onto a
  FloorPlank landing; the stairwell is left open through the upper slab.
- **Upper:** hall strip along the stair block, bed2 / bed3 / study in the
  south strip, main bath (ceramic slab) over the kitchen, primary suite in
  the east block (bedroom S + ensuite N, ceramic slab) — ensuite and main
  bath stack over the ground-floor wet zone.
- **Roof:** Roof01 modules on both slopes, bisect-cut at the ridge; Roof03
  dormer module on the south slope; gable ends are brick infill prisms that
  follow the measured roof profile (sampled from a plain Roof01 only — the
  dormer module's cap tiles corrupt the profile bins).

## Pieces: 30 of 50 used (85 placements)

Used: Roof01 ×7, Trim01 ×8, internal wall03 ×8, wall02 ×6, Baseflor
WindowFrame01 ×5, SecondFloor WindowFrame01/02 ×4 each, internal
DoorFrame01 ×4, Trim01_Corner ×4, FloorPlank01 ×3, every exterior corner ×2
per floor, Baseflor DoorFrame01 ×2 (front + garden doors), Baseflor wall01 /
WindowFrame02 ×2, SecondFloor wall01 ×2, FloorPlank02 ×2, internal
corner01 ×2, Wood_External_Floor01/03 ×2 each (platform), Internal_Stairs01,
External_Stairs01, Roof03 dormer, and all five internal floor slabs
(Wood01/02/03, Ceramic01/02).

Not used, with reasons:

- **Damage variants** — wall01_torn, wall01_torn02, Wood_Internal_Floor04,
  and Internal_Stair02 (also a 16.4 m mansion-hall piece; torn carpet).
- **WindowFrame03 (both floors)** — curved turret/bay sections; they cannot
  join the rectangular wall grid.
- **SecondFloor DoorFrame01** — an upper door needs a balcony/terrace to
  serve; the kit has no balcony piece.
- **Internal wall01** — its 6.0 m length fit no partition run without
  blocking a needed arch opening; wall02/wall03/DoorFrame01 cover every run.
- **Wood_External_Floor02** — ~11% of faces carry the unassigned
  "Fbx Default Material"; Floor01/03 cover the platform.
- **Roof02/04/05** — complete pre-built roofs for a different 14 × 8
  footprint; **Roof06/07** are fragments in source-mansion coordinates;
  **Roof08/09/10 + Roof_corner01/02/03** are an alternative roof family the
  Roof01/03 module system already closes.

## Kit defect found: Corner02 vertex detection

`Corner01` carries concrete quoins at its outer vertex, so the build detects
the vertex from the MI_Concrete_Wall_Details centroid. **`Corner02` has no
concrete at all** — a centroid fallback over all faces lands on the wrong
AABB corner and the piece goes in rotated 180°, showing its interior
wallpaper/wainscot panel on the facade (SE + NW corners, both floors).
Corner02's wainscot cluster sits at its *inner* (concave) vertex instead, so
the tool now takes the diagonally opposite AABB corner when no concrete is
present. Verified by corner close-up renders and a raycast material grid
over both outer planes (all brick after the fix).

Two things that look like defects but are not: the red damask panels visible
through the upper east windows are the rooms' interior finishes seen through
the openings (confirmed with hide/isolate renders), and the loose/lifted
boards on the platform deck and some floor slabs are the kit pieces' own
modeled wear — the standalone pieces raycast solid.

## Proof renders

In `_blender/polish/`: `two_story_house_ext_front_se.png`,
`two_story_house_ext_front.png`, `two_story_house_ext_back_nw.png`,
`two_story_house_int_foyer.png`, `two_story_house_int_kitchen.png`,
`two_story_house_int_hall_upper.png` (Cycles, real materials throughout).
