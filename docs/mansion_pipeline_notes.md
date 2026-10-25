# Mansion pipeline notes — import frame, the gap bug, and the census gate

Companion to `Assets/_Game/Environments/Mansion/README.md` (layout and
architecture). This file records how the generated mansion was verified
against the Blender ground truth, the root cause of the "tons of gaps"
defect fixed in PR #59 (2026-10-25), and the traps that cost three
iterations — so nobody has to rediscover them.

## Pipeline in one paragraph

`Source/level_actors.json` (2,615 placements, UE conventions,
centimetres) → `MansionPrefabFactory` builds one wrapper prefab per
unique mesh (211 base wrappers + 30 mirrored `_M` variants) →
`MansionSceneBuilder`
instantiates the manifest into chunk scenes (Shell 518 / Interior
1,465 / Grounds 632) → `MansionSceneLoader` loads them additively via
the `IMapLoader` seam. Generated output is committed; regeneration is
idempotent (Tools ▸ Mansion, or batchmode `-executeMethod
MansionPrefabFactory.GenerateAll` then `MansionSceneBuilder.BuildAll`).

## UE → Unity transform conversion (validated)

Per placement, with `A` = the (x, z, y) axis permutation and `M_ue` =
UE's `FRotationMatrix` from `rot_pyr` [pitch, yaw, roll]:

- `position = (UE.x, UE.z, UE.y) / 100`
- `R_unity = A · M_ue · Aᵀ`
- `scale = (|sx|, |sz|, |sy|)`; if `det(scale) < 0` the placement uses
  the baked `_M` mirrored wrapper with positive scale — no negative
  scales ship. Mirror sign patterns in this level: 140 × UE `(-,+,+)`,
  13 × `(-,-,-)` (all `SM_Ivy_Wall_C`). 30 unique meshes / 153
  placements are mirrored.

## The gap bug (PR #59): pieces mirrored on their pivots

Symptom: straight wall runs tiled, but corners, junctions, window
openings and roofs misaligned — visible gaps everywhere two
non-symmetric pieces met.

Root cause: **Unity's FBX importer composes UE geometry X-flipped
relative to the actor frame the manifest transforms assume.**
Measured on all 178 parts: imported wrapper geometry = actor-frame
geometry with X negated (175 exact, 2 within LOD noise). The scene
builder's rotations were correct all along; the geometry *inside*
each wrapper sat flipped on its pivot.

Fix (in `MansionPrefabFactory.BuildWrapper` / `BuildMirroredWrapper`):

- Bake `F_x · L` into every wrapper mesh, where `F_x = Scale(-1,1,1)`
  and `L` is the renderer's local matrix chain inside the wrapper;
  then flatten the renderers under `PivotOffset` at identity. Baked
  meshes land in `GeneratedMeshes/<Part>.asset`.
- Mirrored `_M` variants bake `Scale(signs) · L` on the corrected
  base, with `signs` in Unity axis order (UE sx, UE sz, UE sy) from
  `ManifestData.MirrorSignByMesh`.

### The two serialization traps (read this before touching the bake)

In-memory bakes verified *exact* while the saved prefabs were still
wrong by exactly one extra node transform (`L · F_x · L`). Both
traps are now avoided in code; the pins below keep them avoided.

1. **Live world matrices lie during restructuring.** In a batchmode
   factory run, `renderer.bounds` / `localToWorldMatrix` lag behind
   reparenting — renderers still measure under their old parents.
   All bake matrices and `ComputeBounds` therefore compose
   **serialized local TRS chains** (`LocalChainMatrix`: walk
   `localPosition/localRotation/localScale` up to the wrapper root).
   Never use live world matrices for generation math.
2. **A nested prefab instance does not serialize restructuring.**
   `BuildWrapper` originally kept the instantiated model as a nested
   prefab instance. `SaveAsPrefabAsset` on such a wrapper keeps the
   model's original node hierarchy and records only *property
   modifications* — the flattening (reparent to `PivotOffset`,
   identity locals) is structural, so it was silently dropped, and
   the node-composed baked geometry got its node transform applied a
   second time at render. Fix: `PrefabUtility.UnpackPrefabInstance(
   instance, PrefabUnpackMode.Completely, …)` immediately after
   instantiation. Plain GameObjects serialize literally.

Where the FBX conversion node sits varies per file (measured:
`SM_Roof01` on the asset root, `SM_Window01` mid-chain,
`SM_ExternalWall_SecondFloor_wall01` at the leaf) — another reason
the bake composes the full chain instead of assuming a fixed node.

## Plane actors are not 1:1 (also PR #59)

The 5 manifest `Plane` actors (`Watter` pond sheet + 4
`BP_FXFogCard*`) are recreated natively by the builder. UE's engine
Plane is **1×1 m**; Unity's primitive Plane is **10×10 m** at scale
1. Applying the manifest scale verbatim made them exactly 10×
oversize (the pond covered 357×403 m instead of 35.7×40.3 m). The
builder divides the horizontal scale components by 10 for Plane
actors; the five footprints are pinned in
`MansionSceneBuilderTests.PlaneActors_MatchGroundTruthFootprints`.

## The census gate (acceptance method for placement work)

Bounding-box comparisons of *bounding boxes* are not evidence:
rotated placements inflate both sides differently, and AABB-of-AABB
hid real errors early in this investigation. The gate that closed
the gap hunt compares **exact vertices**:

- Blender side: evaluated mesh vertices (`evaluated_get` +
  `to_mesh`, numpy `foreach_get`), world-transformed, mapped to the
  Unity frame as `(x, y, z) → (x, z, −y)`.
- Unity side: LOD0 vertices of every placed instance in the three
  chunk scenes, transformed by serialized local TRS chains composed
  to the scene root.
- Per placement, compare world AABBs (min/max, 6 values).

Final result on PR #59: **2,615 / 2,615 placements within 0.05 m.**

Census bookkeeping notes:

- Blender contains one helper object, `Ground` (a 120×120 m catcher
  plane), that is not a manifest actor — exclude it.
- Two manifest placements share the label `TrashBag`; Blender's
  second copy is named `TrashBag__177`. Match by AABB set, not by
  name alone. (`TrashBag` was also the one placement whose Blender
  transform deviates slightly from the manifest — a hand tweak in
  the rebuild. The manifest is the source of truth; the builder
  follows it.)
- The 132 `SM_ExternalBarrier*` actors and 13 label-only actors are
  **verified skips**, not missing pieces: their mesh assets do not
  exist anywhere in the UE project's `Content/` (migration
  leftovers; they render as nothing in UE too). 63
  decals/volumes/Niagara/PlayerStart actors are
  inventoried-not-placed by design.

Test pins that guard all of this (`Assets/_Game/Tests/EditMode/
Environments/`): `MansionFactoryTests` pins wrapper bounds against
Blender-measured actor-frame values (wall01, WindowFrame01, Roof01,
Window01) and mirror consistency over every `_M` wrapper;
`MansionSceneBuilderTests` pins chunk populations (518/1465/632)
and the five Plane footprints. **A test is the specification: if a
pin fails, fix the factory/builder, never the pin.**

## Verified non-causes (don't re-investigate)

- Missing placements: none — every manifest mesh actor is placed.
- Import scale (100×/0.01×) errors: none — per-part size census is
  clean.
- Builder rotation formula: exact (per-actor matrix algebra, 400
  sampled placements, error 0.0) — it was never the problem.

## Regeneration workflow

1. PC clone, Task Scheduler task `MansionFactory` (fetch → checkout
   → **`git reset --hard <remote>/<branch>`** → batchmode
   `MansionPrefabFactory.GenerateAll`), then `MansionBuilder`
   (`MansionSceneBuilder.BuildAll`). The reset is mandatory:
   `git checkout` of an already-local branch does not fast-forward
   it (bit this project twice).
2. Commit the generated output (`Assets/_Game/Environments`) with
   the factory/builder code change that produced it — code and
   generation move together.
3. CI expectation: EditMode all pass; PlayMode fails only with the
   documented baseline pair (`BulletProjectilePlayTests.
   MaxTravelDistance_BulletIsReleasedIntoEmptySpace`,
   `PauseMenuControllerPlayTests.SetOpen_TogglesVisibilityAndCursor`)
   — verify from the `playmode-test-results` artifact XML, not the
   job colour.

## Still open (deliberate, per the phased plan)

- Plane-actor *materials* (pond water / fog cards currently render
  as plain sheets — geometry is correct and pinned).
- Lighting: interiors are unbaked; lightmap + occlusion bake is its
  own phase (bake group = load group = chunk scenes).
- Forward+ / GPU Resident Drawer enablement, the UUM-156125
  repeated chunk-switch soak test, the NGO `NetworkSceneManager`
  implementation behind `IMapLoader`, per-floor/sector chunk
  refinement (`ChunkFor` is the one-function seam), and the Unity
  6000.3 patch bump.
