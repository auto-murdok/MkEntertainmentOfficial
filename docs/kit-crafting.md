# Kit crafting list — bottom-up construction of the new mansion

Reference: `_blender/tests/kit_overview.blend` (all 50 pieces, labeled).
Build: `_blender/tools/build_new_mansion.py` → `_blender/tests/new_mansion.blend`.
Rule: nothing joins the mansion until its joint is proven below.

## Foundation (step 1 — done)

- Ground-floor slab: `SM_Wood_Internal_Floor01` (6.71 x 6.36 solid interior
  slab — the external Floor0x pieces are 2 m tall open deck platforms, wrong
  for interiors) tiled + cut to the starter footprint **14.22 x 12.25**
  (old west block; room to grow east), top at z 0.
- Seal scan: 0.5 m raycast grid over the footprint — **0 misses**.
- Default camera is top-down (floor-plan view).

## Pair matrix (stage 1)

| # | Pair | Verdict | Proof |
|---|------|---------|-------|
| 1 | floor + wall01 (footing) | pending | — |
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
