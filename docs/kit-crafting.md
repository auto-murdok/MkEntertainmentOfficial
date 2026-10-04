# Kit crafting list — bottom-up construction of the new mansion

Reference: `_blender/tests/kit_overview.blend` (all 50 pieces, labeled).
Build: `_blender/tools/build_new_mansion.py` → `_blender/tests/new_mansion.blend`.
Rule: nothing joins the mansion until its joint is proven below.

## Foundation (step 1 — done, verified)

- Ground-floor slab: `SM_Wood_Internal_Floor01` (6.71 x 6.36 solid interior
  slab — the external Floor0x pieces are 2 m tall open deck platforms, wrong
  for interiors) tiled + cut to the starter footprint **14.22 x 12.25**
  (old west block; room to grow east).
- Raised living floor (kit pattern): slab top at **z 2.05** — the Baseflor
  door opening sills at ~2.05, served by the 2.02-tall exterior stairs.
  Walls rise from grade (z 0) as the plinth.
- Seal scan: slab-only raycast hits on an inset 0.5 m grid — **0 misses**.
  (Lesson: the ground plane also catches rays — count slab hits only.)
- Default camera is a south-east 3/4 view.

## South wall + entrance (step 2 — done, verified)

- Run: `Baseflor wall01` (2.00) x4 + `DoorFrame01` (4.01) centered at x 0 +
  2 bisected 1.105 strips. Outer faces flush with slab edge, 7 pieces.
- Footing check (all bases z 0) + doorway ray (open passage) pass in-build.
- Entrance stairs: `External_Stairs01` centered, landing at the wall face,
  top 2.02 vs sill 2.05.
- Watch item: possible hairline sliver left of the arch (door/strip joint) —
  confirm in GUI.

## Pair matrix (stage 1)

| # | Pair | Verdict | Proof |
|---|------|---------|-------|
| 1 | floor + wall01 (footing) | clean (bases z 0, 7 pcs) | nm_south |
| 1b | wall + DoorFrame01 (entrance) | clean (opening ~2.5m, sill 2.05, stairs land 2.02) + facing fix: door trim reaches local y −0.11, shift +0.11 so brick show-faces align (both face min-y, no turn needed) | nm_door, door_top |
| 2 | wall01 + wall01 (straight run) | pending | — |
| 3 | wall01 + corner01 (right angle) | pending | — |
| 4 | Baseflor wall01 + SecondFloor wall01 (stacked) | pending | — |
| 5 | wall + WindowFrame01 (opening) | pending | — |
| 6 | Roof01 + Roof01 (slope continuation) | pending | — |

Method per pair: LOD0 import, snap on the kit grid on the real slab,
render 2–3 angles + raycast the seam. Mark clean / shim / incompatible.

## Assemblies (stage 2 — only from clean pairs)

L-corner, window wall, stacked bay, stair landing joint.

## Structure recipes (stage 3)

Full room, gate, porch v2 — from validated assemblies only.
