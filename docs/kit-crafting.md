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
  for interiors) tiled + cut to the starter footprint **14.09 x 12.25**.
  Footprint derives from the wall-run module sum
  (1.04 + 4.00 + 4.01 + 4.00 + 1.04); slab cuts are buried under walls only.
- Raised living floor (kit pattern): slab top at **z 2.05** — the Baseflor
  door opening sills at ~2.05, served by the 2.02-tall exterior stairs.
  Walls rise from grade (z 0) as the plinth.
- Seal scan: slab-only raycast hits on an inset 0.5 m grid — **0 misses**.
  (Lesson: the ground plane also catches rays — count slab hits only.)
- Default camera is a south-east 3/4 view.

## South wall + entrance (step 2 — done, verified)

- Run (whole pieces, exact closure): `Corner01` + 2x `wall01` + `DoorFrame01`
  (centered) + 2x `wall01` + `Corner01` = 14.09, 7 pieces, zero residuals.
  Bisected filler strips removed with the rule change.
- Corners point-symmetric (left rot 0, right rot 180); exempt from the
  show-face assert (brick wraps the pier, 0.05 quoin asymmetry).
- Footing check (all bases z 0) + doorway ray (open passage) pass in-build.
- Entrance stairs: `External_Stairs01` centered, landing at the wall face,
  top 2.02 vs sill 2.05.
- Watch items: (a) hairline sliver left of the arch — confirm in GUI;
  (b) right corner shows a recessed pocket with interior finish on its south
  face (raycast proves solid behind — no through-gap; likely the rebate for
  the future perpendicular wall run, confirmed at that step).

## Pair matrix (stage 1)

| # | Pair | Verdict | Proof |
|---|------|---------|-------|
| 1 | floor + wall01 (footing) | clean (bases z 0, 7 pcs) | `_blender/polish/new_mansion_south.png` |
| 1b | wall + DoorFrame01 (entrance) | clean (opening ~2.5m, sill 2.05, stairs land 2.02) + facing fix: door bbox min is trim at local y −0.11, shift −0.11 so brick show-faces land coplanar (both face min-y, no turn; first attempt used the wrong sign) | `_blender/polish/new_mansion_door.png`, `_blender/polish/new_mansion_top.png` |
| 2 | wall01 + wall01 (straight run) | pending | — |
| 3 | wall01 + corner01 (right angle) | placed both ends (point-symmetric), SW joint clean; SE shows recessed pocket, raycast solid behind — perpendicular-run rebate suspected, verdict at that step | nm2_sw, nm3_se_s |
| 4 | Baseflor wall01 + SecondFloor wall01 (stacked) | pending | — |
| 5 | wall + WindowFrame01 (opening) | pending | — |
| 6 | Roof01 + Roof01 (slope continuation) | pending | — |

Method per pair: LOD0 import, snap on the kit grid on the real slab,
render 2–3 angles + raycast the seam. Mark clean / shim / incompatible.

## Assemblies (stage 2 — only from clean pairs)

L-corner, window wall, stacked bay, stair landing joint.

## Structure recipes (stage 3)

Full room, gate, porch v2 — from validated assemblies only.
