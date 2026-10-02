# 3D Fixes — Session 2026-10-01

Everything repaired, investigated, or demonstrated in this session for
`MkEntertainmentOfficial` (`Assets/ImportedContent/Building_kit/`).
All work done with headless Blender 4.0.2; Unity Editor could not be used
(license/entitlement failure — see "Validation limits").

## 1. Missing interior window reveal strips — DONE, merged (PR #2)

**Problem:** Window openings in `SM_ExternalWall_Baseflor_WindowFrame01` and
`..._WindowFrame02` had no interior lining on the sill/top/jambs — the wall
core was visible from inside.

**Fix:**
- WF01: added 4 reveal strips (sill, top, left jamb, right jamb).
- WF02: added 8 reveal strips (two windows).
- Material: `MI_Wood_Wainscoting02` (repo material, not invented).
- UV density matched to surrounding trim: 0.126 uv/m.
- Normals verified per strip: sill `+Z`, top `-Z`, left jamb `+X`, right jamb `-X`.
- `UCX_*` collision meshes preserved; `.meta` files untouched.

## 2. Vertical base bricks — DONE, merged (PR #2)

**Problem:** Base-wall faces (`MI_Bricks_External_BaseWall`) on WF01, WF02 and
`SM_ExternalWall_Baseflor_DoorFrame01` rendered bricks vertically (U axis
pointed up the wall).

**Fix:** Rotated base-wall UVs 90° so bricks run horizontally.
Critical detail: the rotation is applied to **UV space about the fixed point
(0.5, 0.5)** — a rigid transform. This preserves UV continuity across the
triangulation diagonals that all base faces use.

**Regression caught and fixed in-session:** the first attempt rotated each
face about its *own* UV center. That broke continuity across shared triangle
edges and produced visible diagonal divider lines in the base bricks
(especially obvious on DoorFrame01's fan triangulation). Redone about the
fixed point; diagonals gone, verified by render.

## 3. Base bricks larger than upper-wall bricks — implemented, render-verified, NOT committed

**Investigation:**
- Measured UV density on WF01: base `0.074 uv/m`, upper wall `0.127 uv/m`
  → ratio **1.857** (identical on WF01, WF02, DoorFrame01).
- Scaled base-wall UVs ×1.857 about fixed (0.5, 0.5) (uniform scale about a
  fixed point is affine, so no seams).
- Render-verified: base and upper bricks now identical size.

**Important nuance:** both brick materials already use the world-aligned
shader with the same texture and `_TextureSize: 300`, and that shader
**ignores mesh UVs entirely**. So in Unity the sizes already matched — the
mismatch existed only in UV-based views (Blender previews, standard-material
fallback). The mesh fix keeps Blender QA truthful and fixes the fallback.

**Status:** fixed FBXs render-verified; committed on branch
`fix/brick-uv-density-match`, open as **PR #3** — awaiting merge decision.

## 4. World-aligned brick shader — investigated, replicated in Blender

**Finding:** `Assets/ImportedContent/Shaders/WorldAlignedLit.shader`
(`UEI/WorldAlignedLit`, GUID `68d1487f55f242d4a13d0a9b3d11b223`) already
implements world-space triplanar sampling (albedo, normal, packed ORM),
mirroring Unreal's `MM_Buildings_WorldAligned`. Both brick materials
(`MI_Bricks_External_BaseWall`, `MI_Bricks_External_Wall`) use it with
`_TextureSize: 300` (300 cm = 3 m tile). 8 other materials (concrete,
wallpaper, wood floors/wainscoting, ceiling) also use it.

**Blender replication for visual QA:** built a Cycles node material
reproducing the shader's projection math exactly —
`uvX=(-Z,Y)/3`, `uvY=(X,Z)/3`, `uvZ=(X,Y)/3`, blend weights `|n|^4`
normalized — and rendered WF01 + DoorFrame01 joined. Result: uniform
15 cm brick courses, courses running straight through the piece seam.
Proof: `_polish_demo/renders/worldbricks_join.png`.

**Not yet done:** the shader itself is unvalidated — normal-map projection
correctness, triplanar blend on angled faces, GPU instancing/batching
compatibility, and shadow-caster behavior still need review against current
Unity 6 URP practice. Since 10 materials depend on it, this is the
highest-leverage remaining investigation.

## 5. PRs

- **PR #1** — "Fix zeroed blue channel in 11 normal maps" (`z = sqrt(1-x²-y²)`).
  Open, awaiting merge decision. (Predates this session's active work; listed
  for completeness.)
- **PR #2** — "Add missing interior window reveal strips" — commits: reveal
  strips; base-brick UV orientation fix (WF01/WF02/DoorFrame01).
  **Approved and merged** this session.
- **PR #3** — "Match base-wall brick size to upper-wall brick size" — the
  ×1.857 base UV density fix from §3. Open, awaiting merge decision.

## Validation limits (standing)

- No Unity render or project validation was possible: `unity auth login`
  succeeded but headless Editor runs failed (no `com.unity.editor.headless`
  entitlement / no valid license afterwards).
- Blender is the visual-QA path. Rules that kept renders honest:
  - **Albedo-only renders for color truth** — full PBR path turned
    wallpaper/bricks near-black (normal/ORM path suspect, root cause not
    isolated).
  - **Never invent materials** — all Blender materials built from the repo's
    real `.mat` textures via `_polish_demo/matlib_building_kit.json`.
  - Blender 4.0.2 build has no OpenImageDenoiser — denoising stays off;
    headless mesh ops use `bmesh`, not `bpy.ops`.

## Open items

1. PR for the brick-size UV fix (v3 FBXs) — needs approval.
2. `WorldAlignedLit` shader review (normals, instancing, shadows).
3. WF03 (angled geometry) still unassessed for reveals/UVs — do not reuse the
   axis-aligned WF01/02 repair blindly.
4. WF01 has one flipped wallpaper face near `(4.0, 0.3, 6.33)` — unfixed.
5. Batch 2 not declared complete; do not start Batch 3.
