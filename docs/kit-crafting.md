# Kit crafting list — bottom-up construction of the new mansion

Reference: `_blender/tests/kit_overview.blend` (all 50 pieces, labeled).
Build: `_blender/tools/build_new_mansion.py` → `_blender/tests/new_mansion.blend`.
Rule: nothing joins the mansion until its joint is proven below.

## Golden rule — pieces snap, sizes never adjust

1. Whole pieces only: no bisect, no scale, no filler strips.
2. Dimensions flow piece → design: runs close on module sums
   (`wall01` 2.00, `DoorFrame01` 4.01, `Corner01` 1.04); openings move,
   pieces never cut. Enforced in-script (`assert` on the run sum).
3. Show-faces align, not bboxes: brick planes coplanar (probed per piece;
   doors/corners carry trim past their bbox — offset, don't cut).
   Enforced in-script (`assert_showface`, corners exempt: brick wraps them).
4. Substrate carve-out: slab tiles may be cut only where buried under walls.
   Open floor stays whole tiles.
5. Sub-cm residuals distribute symmetrically and are documented per joint.

## Foundation (step 1 — done, verified)

- Ground-floor slab: `SM_Wood_Internal_Floor01` (6.71 x 6.36 solid interior
  slab — the external Floor0x pieces are 2 m tall open deck platforms, wrong
  for interiors) tiled + cut to the starter footprint **14.08 x 12.07**.
  Footprint derives from wall-run module sums: south/north
  (1.04 + 4.00 + 4.01 + 4.00 + 1.03), depth from the side runs
  (1.03 + 4.00 + 2.00 + 4.00 + 1.04 — corner spans are rotation-dependent).
  South edge pinned at −6.125 (door/stairs/verified work); growth absorbs north.
  Tile grid shifted +0.33 in x so the unavoidable remainders become narrow
  perimeter strips buried under wall bands, never slices across open floor.
- Cut-cap lesson: `edgenet_fill` caps are born UV-less (single-texel sample
  = white patches). `repair_cap_uvs` gives zero-UV-area faces a top-down
  planar map — rule for every future bisect.
- Raised living floor (kit pattern): slab top at **z 2.05** — the Baseflor
  door opening sills at ~2.05, served by the 2.02-tall exterior stairs.
  Walls rise from grade (z 0) as the plinth.
- Seal scan: slab-only raycast hits on an inset 0.5 m grid — **0 misses**.
  (Lesson: the ground plane also catches rays — count slab hits only.)
- Default camera is a south-east 3/4 view.

## South wall + entrance (step 2 — done, verified)

- Run (whole pieces, exact closure): `Corner01` + `WindowFrame01` +
  `DoorFrame01` (centered) + `WindowFrame01` + `Corner01` = 14.08,
  5 pieces, zero residuals. Windows (4.00) swap 1:1 for wall pairs.
- `Corner01` is HANDED (probed): finished outer faces S+W, receiving
  rebates N+E — left rot 0, right rot 90 (outers S+E, rebates N+W into both
  runs). `Corner02` probed same-hand (bigger variant, 1.18) — held in
  reserve, no mirror-hand corner exists in the kit.
- Corners exempt from the show-face assert (brick wraps the pier).
  Tried + reverted: seating corners by raw brick extremes drags whole piers
  ~3 cm off their butt joints — quoin relief dominates the extreme. Corners
  align by placement + visual/top-ortho check only. The 11 mm SE step is
  quoin pattern relief, not offset (joint audit: all interfaces ≤3 mm).
  The "pocket" seen earlier was the rot-180 misorientation, now gone.
- Footing check (all bases z 0) + doorway ray (open passage) pass in-build.
- Entrance stairs: `External_Stairs01` centered, landing at the wall face,
  top 2.02 vs sill 2.05.
- Window finding: opening ~2.5 m wide, sill ~2.9, NO glass in the kit piece
  (open hole, sky through). Raycast anomaly: fresh-import rays report the
  opening blocked and face inspection shows wild triangles + impossible
  areas near the jamb — yet all renders show a clean opening. Renders are
  truth for this piece; raycast not trusted on it until the triangles are
  explained. Glass stays a later pass (kit ships none for wall windows).
- Watch item: hairline sliver left of the arch — confirm in GUI.

## Full perimeter (step 3 — done, verified)

- West run (faces −x, theta −90): window + wall + window between the SW
  corner and a NEW NW corner (rot 270: outers W+N). East run mirrored
  (theta +90, NEW NE corner rot 180: outers N+E). 16 wall pieces total.
- North run (faces +y, theta 180): 3 windows between NW/NE corners.
  Span 12.01 vs 12.00 of glass modules: centered, 5 mm each side (rule 5 —
  the 1 cm is the door's surplus showing up where no door sits).
- Corner rotation table (chiral piece, all four used exactly once):
  SW rot 0 (outers S+W), SE rot 90 (S+E), NE rot 180 (N+E), NW rot 270 (W+N).
- Measurement finding: plain `matrix_world` reads can lag data transforms a
  step behind in background mode (chased a phantom 5 cm offset through three
  theories). `brick_extreme` measures via the evaluated mesh — evaluation IS
  the refresh. The seating nudge built on top never triggers (all runs pass
  unseated); kept as harness for future flipped pieces.
- Doorway ray now terminates ON the north run masonry (aimed below the
  window sill — at walking height it flies through both openings).
- Slab strips verified buried: top view shows continuous wood, legitimate
  tile seams only.

## Piece datasheets (all probed, LOD0, meters)

| Piece | BBox (x y z) | Module role | Facing | Quirks | Status |
|---|---|---|---|---|---|
| `SM_Wood_Internal_Floor01` | 6.71 x 6.36 x 0.30 | slab tile unit | top = walking surface | cuts bury under walls only; caps need `repair_cap_uvs` | in slab, sealed 0 misses |
| `SM_ExternalWall_Baseflor_wall01` | 2.00 x 0.46 x 7.50, base z 0 | 2.00 run module | brick show-face local y 0 (min side), no offset | — | clean (retired from run by window swap, math unchanged) |
| `SM_ExternalWall_Baseflor_DoorFrame01` | 4.01 x 0.60 x 7.50, base z 0 | 4.01 opening module (= 2 walls + 1 cm, symmetric) | brick local y 0; trim to −0.11 → −0.11y offset | opening ~2.5 wide centered, sill ~2.05 (raised-floor driver) | clean, centered |
| `SM_ExternalWall_Baseflor_Corner01` | 1.04 x 1.03 x 7.53, base z 0 | run ends | **handed:** outers S+W, rebates N+E; left rot 0, right rot 90 (counts 1.03 along run) | exempt from plane assert (brick wraps pier); quoin asymmetry 0.05 | clean both ends |
| `SM_ExternalWall_Baseflor_Corner02` | 1.18 x 1.21 x 7.52, base z 0 | — | same hand as Corner01 (bigger variant; no mirror hand exists in kit) | — | reserve, unplaced |
| `SM_ExternalWall_Baseflor_WindowFrame01` | 4.00 x 0.46 x 7.50, base z 0 | 4.00 opening module, 1:1 wall-pair swap | brick min-y, no offset | opening ~2.5, sill ~2.9, NO kit glass; raycast anomaly (renders are truth) | clean, 2 placed |
| `SM_External_Stairs01` | 7.67 wide x 5.37 run x 2.02, run along local y, landing on top | entrance stair | low end south, landing north to the sill | top 2.02 vs sill 2.05 = 5 cm lip | clean, centered on door |
| `SM_ExternalWall_Baseflor_WindowFrame02` | 6.00 x 0.50 x 7.50 | — | — | breaks the 4.0 module | rejected for this run (reason recorded) |
| `SM_ExternalWall_Baseflor_WindowFrame03` | 10.00 x 2.31 x 7.50-ish bay | — | — | curved bay, not a flat-run piece | rejected for this run (reason recorded) |

Other floor tiles, measured for later (unplaced): internal Wood02/03 and
Ceramic01/02 ≈ 6.7 x 6.35 x 0.3 class; external Floor01/02/03 = 7.4 x 6.42
x 2.05 open deck platforms (interiors never); FloorPlanks are loose boards.

## Pair matrix (stage 1)

| # | Pair | Verdict | Proof |
|---|------|---------|-------|
| 1 | floor + wall01 (footing) | clean (bases z 0, 7 pcs) | `_blender/polish/new_mansion_south.png` |
| 1b | wall + DoorFrame01 (entrance) | clean (opening ~2.5m, sill 2.05, stairs land 2.02) + facing fix: door bbox min is trim at local y −0.11, shift −0.11 so brick show-faces land coplanar (both face min-y, no turn; first attempt used the wrong sign) | `_blender/polish/new_mansion_door.png`, `_blender/polish/new_mansion_top.png` |
| 2 | wall01 + wall01 (straight run) | pending — no straight wall joint exists yet (side runs pair wall with windows, never wall-to-wall) | — |
| 3 | wall01 + corner01 (right angle) | clean — left rot 0, right rot 90 (handed piece, rebates N+W into both runs); quoins read both ends | `_blender/polish/new_mansion_sw_corner.png`, `_blender/polish/new_mansion_se_corner.png` |
| 5 | wall + WindowFrame01 (opening) | clean — 4.00 swaps 1:1 for wall pairs, opening ~2.5m sill ~2.9, no kit glass; raycast anomaly documented (renders are truth) | `_blender/polish/new_mansion_window.png` |
| 4 | Baseflor wall01 + SecondFloor wall01 (stacked) | pending | — |
| 6 | Roof01 + Roof01 (slope continuation) | pending | — |

Method per pair: LOD0 import, snap on the kit grid on the real slab,
render 2–3 angles + raycast the seam. Mark clean / shim / incompatible.

## Assemblies (stage 2 — only from clean pairs)

L-corner, window wall, stacked bay, stair landing joint.

## Structure recipes (stage 3)

Full room, gate, porch v2 — from validated assemblies only.
