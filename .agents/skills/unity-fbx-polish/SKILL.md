---
name: "unity-fbx-polish"
description: "Repair and visually verify Unity-bound FBX meshes with headless Blender: UV orientation/density fixes that preserve triangulation continuity, and world-space triplanar previews replicating Unity world-aligned shaders."
metadata: { "includeInPrompt": true }
---

# Unity FBX Polish

## Purpose
Fix and visually QA FBX meshes destined for Unity when the Unity Editor is
unavailable. Covers UV-orientation and UV-density repairs plus a Blender
replication of world-aligned triplanar shaders so previews show the true
in-game look.

## Tooling
- Headless Blender 4.0.2 portable:
  `~/workspace/blender-portable/blender-4.0.2-linux-x64/blender`
  (persists across VM replacements; never use apt Blender). Run scripts with
  `blender --background --python <script> -- <args>`.
- Helpers in `bin/`:
  - `uv_rotate90.py <in.fbx> <out.fbx> <MatSubstring>` — rotate one
    material's UVs 90° (e.g. fix vertical bricks).
  - `uv_scale.py <in.fbx> <out.fbx> <MatSubstring> <factor>` — uniform-scale
    one material's UVs (e.g. match brick size between materials).
  - `worldspace_preview.py <out.png> <albedo.png> <texSizeCm> <sharpness>
    <in1.fbx> [xoff1] [in2.fbx [xoff2]...]` — render with a triplanar
    world-space material replicating `UEI/WorldAlignedLit`
    (`uvX=(-Z,Y)`, `uvY=(X,Z)`, `uvZ=(X,Y)` over tile `_TextureSize/100` m,
    weights `|n|^sharpness` normalized). Ignores mesh UVs, like the shader.
- Python `trimesh` 5.1.0 for fast mesh stats; ImageMagick 6 for texture work.

## Workflow
1. **Inspect first.** Import the FBX headless, list material slots, and
   measure per-material UV density (world-space edge length vs UV edge
   length — remember FBX object-space may be cm; use `matrix_world`).
2. **Repair UVs with rigid transforms only.** Both helpers transform UV
   *space* about the fixed point (0.5, 0.5) — rotation and uniform scale
   about a fixed point are rigid/affine, so shared triangle edges stay
   shared. Verify with a render.
3. **Preview the shader truth.** If the target materials use a world-aligned
   shader, render with `worldspace_preview.py` — mesh UVs are irrelevant
   there, so this is the authoritative preview.
4. **Render for verification.** Albedo-only Cycles renders for color truth;
   full PBR only when the normal/ORM path is already trusted. Frame whole
   models, not just closeups; join adjacent pieces to check seams.

## Operating Rules
1. **Never rotate/scale UVs about per-face centers.** It breaks UV
   continuity across triangulation diagonals and produces visible seams.
   Always transform about a fixed point.
2. **Never invent materials.** Build preview materials only from the repo's
   real `.mat` textures (resolve texture GUIDs to files first).
3. **Strip `UCX_*` collision meshes** before preview renders; never delete
   them from repaired FBXs. Preserve `.meta` files; never hand-edit Unity
   `.mat`/`.prefab`/`.unity` YAML.
4. **A world-space shader ignores mesh UVs.** UV fixes then only affect
   Blender QA and standard-material fallbacks — say so explicitly instead of
   implying an in-game change.
5. Blender 4.0.2 has no OpenImageDenoiser — keep denoising off. Headless
   mesh ops must use `bmesh`, not `bpy.ops`. `SeparateRGB` has no Alpha
   output; use the image node's Alpha socket.
6. Every repo change goes through a PR (rebase merges); never push to the
   default branch. Render proof before opening the PR, and never merge
   without explicit approval.
