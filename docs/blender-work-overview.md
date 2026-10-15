# Blender work — complete overview (2026-10-01 → 2026-10-14)

One record of **all** Blender work done for `MkEntertainmentOfficial`: the FBX
polish batches, the `unity-fbx-polish` skill, the indoor test room, the
performance POCs, both house builds, and the Blender-side experiments that
never landed in this repo.

This is the index and the history. The detail lives in the linked documents —
this file does not replace them:

| Detail document | Covers |
|---|---|
| `docs/session-fixes-2026-10-01.md` | Batch 1/2 session: reveals, brick UVs, WorldAlignedLit replication |
| `docs/blender_cli_macos.md` | macOS Blender 5.2.2 LTS CLI environment, probe results, known issues |
| `.agents/skills/unity-fbx-polish/SKILL.md` | The polish skill: helpers, workflow, operating rules |
| `docs/indoor-room-real-materials-2026-10-02.md` | Indoor room rebuilt with real kit materials; measured gaps |
| `docs/kit-crafting.md` | New mansion: golden rule, piece datasheets, pair matrix, continuity checkpoint |
| `AGENTS.md` (§ Headless Blender CLI) | Standing Blender rules for agents in this repo |

Status words used below: **landed** = on `main`; **workspace-only** = done and
verified locally, never committed to this repo; **removed** = landed, then
deliberately taken out (recoverable from git history).

---

## 1. Ground rules (standing, learned the hard way)

1. **Blender-only for 3D work.** Headless Unity on the Linux VM was abandoned
   2026-10-02 (licence/entitlement failures, exit 198, ~700 MB RAM). Unity
   validation happens on the owner's Unity machine; everything model-side is
   done and proof-rendered in Blender.
2. **Never invent materials.** Preview materials are built only from the
   repo's real `.mat` textures (`_blender/polish/matlib_building_kit.json`,
   built by `_blender/tools/build_matlib.py`). An early correction — renders
   made with guessed/placeholder materials — is why this rule exists.
3. **Rigid UV transforms only.** Rotate/scale UV *space* about the fixed
   point (0.5, 0.5). Per-face-centre transforms break UV continuity across
   triangulation diagonals and produce visible seams (caught in-session on
   DoorFrame01, redone, see the session doc §2).
4. **A world-space shader ignores mesh UVs.** `UEI/WorldAlignedLit` samples
   triplanar in world space, so mesh-UV fixes affect Blender QA and
   standard-material fallbacks only — never claim an in-game change from a
   UV fix alone. The authoritative preview is the triplanar replication
   (`worldspace_preview.py`).
5. **Measure, don't assume.** Exact piece geometry is measured from the FBXs
   in Blender (evaluated mesh, not `matrix_world` reads — see §8) before
   anything is built from it.
6. **Kit-only builds.** House/mansion builds use only the repo's building
   kit (50 FBX) — no external/downloaded asset packs (owner decision
   2026-10-03).
7. **Local, commercial-free toolchain only.** Blender (GPL), trimesh,
   ImageMagick — no paid tools, no cloud services.
8. **Every change via PR, merged via rebase**, with render proof before the
   PR and explicit approval before merge. `.meta` files are preserved and
   never hand-edited; Unity YAML (`.mat`/`.prefab`/`.unity`) is never
   hand-edited either.

## 2. Toolchain

| Environment | Blender | Notes |
|---|---|---|
| macOS (owner's machine, Apple Silicon) | **5.2.2 LTS** via Homebrew cask | `blender` on PATH; the repo's primary Blender. Full record: `docs/blender_cli_macos.md` |
| Linux VM (agent machine) | **5.2.2 LTS** portable | Started on portable 4.0.2 (Batch 1/2, 2026-10-01), upgraded to 5.2.2 LTS portable 2026-10-02; 4.0.2 removed. `trimesh` 5.1.0 + `pygltflib`, ImageMagick 6 |
| Windows (UE import pipeline) | **5.2.2 LTS** portable | `Tools/UEImport/vendor/blender` (setup-prereqs.ps1); standardised to 5.2.2 on 2026-10-15 — probe green on Windows (legacy `io_scene_fbx` ops, cm scale, UV names, full helper dry-run + triplanar render). Helpers stay 4.x-compatible (e.g. `use_nodes`) so they run unmodified everywhere |

Verified on 5.2.2: the legacy `io_scene_fbx` add-on is still bundled and
factory-enabled, so all helpers (`bpy.ops.import_scene.fbx` /
`export_scene.fbx`) behave as on 4.x. Do **not** switch helpers to the new
`bpy.ops.wm.fbx_import` without re-validating the cm object-space scale and
material/UV naming. Re-probe after any Blender upgrade
(`.agents/skills/unity-fbx-polish/bin/probe_blender.py`). Headless renders
need absolute output paths; denoising stays off for comparable proof shots;
headless mesh ops use `bmesh`, not `bpy.ops`.

## 3. Where the Blender work lives (in this repo)

| Path | Contents |
|---|---|
| `_blender/tools/` | `inspect_fbx.py`, `inspect_all.py`, `inspect_rig.py` (audit), `build_matlib.py` (material library from real `.mat` textures), `polish_render.py` (QA render pipeline), `rebuild_indoor_room.py`, `build_character_lods.py` (POC 1 productised), `strip_dead_ucx.py` (POC 3), `build_kit_overview.py` (kit census), `build_new_mansion.py` (current mansion) |
| `_blender/polish/` | Committed session output: material libraries (`matlib_building_kit.json`, `matmap_*.json`, `fbx_slots.json`), packed roughness textures, `repaired_normals/`, and curated proof renders (normal-fix before/after, flipped-faces join, indoor room, mansion set, `zombie_lod_compare.png`). `_blender/**/renders/` is gitignored — proof images go directly in `polish/` |
| `_blender/tests/` | Test scenes: `indoor_room.blend` (+ build script), `indoor_room_real.blend` (real-materials rebuild), `kit_overview.blend` (all 50 pieces, labelled 10×5), `new_mansion.blend` (current build) |
| `.agents/skills/unity-fbx-polish/` | The skill: `SKILL.md` + `bin/` helpers — `uv_rotate90.py`, `uv_scale.py`, `worldspace_preview.py`, `remove_degenerate.py`, `flip_faces.py`, `probe_blender.py` |

## 4. Initial audit — 2026-10-01 (landed as inspection tooling)

72 FBXs scanned headless (Blender + trimesh). Verdict: geometry healthy
overall.

- Characters are Fuse→Mixamo exports: clean 65-bone rigs, 23–25k tris,
  normalised skin weights, all UVs present.
- The building kit ships UE LOD chains plus `UCX_*` collision hulls that
  import as extra render meshes (they must be stripped for previews, never
  deleted from repaired FBXs).
- Main suspect for "models not working": the zombie's `.meta` had **no**
  texture remapping (the survivor's does) while its FBX references dead
  Mixamo-server absolute texture paths → zombie likely rendered
  untextured/grey.
- All character textures are legacy specular/gloss (Blinn-Phong) in a URP
  project.

## 5. Batch 1 + Batch 2 — FBX polish (PRs #1–#11, all landed via rebase)

Full detail: `docs/session-fixes-2026-10-01.md`.

| PR | Fix |
|---|---|
| #1 | Zeroed blue channel in 11 normal maps — rebuilt as `z = sqrt(1−x²−y²)` |
| #2 | Missing interior window reveal strips on WindowFrame01 (4 strips) and WindowFrame02 (8 strips), repo material `MI_Wood_Wainscoting02`, UV density 0.126 uv/m matched to trim, per-strip normals verified, UCX preserved |
| #3 | Base-wall brick size matched to upper wall: measured density 0.074 vs 0.127 uv/m (ratio 1.857, identical on WF01/WF02/DoorFrame01), base UVs scaled ×1.857 about the fixed point |
| #4 | The 2026-10-01 session document itself |
| #5 | `unity-fbx-polish` skill added (see §3) |
| #6 | Vertical base bricks on WF01/WF02/DoorFrame01: base-wall UVs rotated 90° about (0.5, 0.5) so bricks run horizontally |
| #7 | One degenerate zero-area wallpaper face removed on WF01 |
| #8 | Degenerate zero-area faces removed from floors and stairs (32 faces total across the batch) |
| #9 | `WorldAlignedLit` fixes: baked-lightmap, Meta and DepthNormals passes |
| #10 | Flipped-face sweep: faces whose normal opposes all neighbours reversed across 19 building-kit FBXs (`flip_faces.py`; proof `_blender/polish/flipped_faces_join.png`) |
| #11 | `flip_faces` / `remove_degenerate` helpers synced into the repo skill |

Also in this batch: the WorldAlignedLit shader was **replicated in
Blender** (Cycles node material reproducing its projection math exactly —
`uvX=(−Z,Y)/3`, `uvY=(X,Z)/3`, `uvZ=(X,Y)/3`, weights `|n|⁴` normalised,
`_TextureSize: 300`). Result on joined WF01 + DoorFrame01: uniform 15 cm
brick courses running straight through the piece seam — the QA method still
used for every world-aligned material.

**Standing caveat from this batch:** PR #9 was never compile-tested in
Unity at the time. The shader's own review (normal-map projection on angled
faces, GPU instancing/batching, shadow caster) remains an open thread (§11).

## 6. Skill, macOS environment, consolidation (PRs #12–#15, landed)

- **#12 / #14** (gralmurdok) — macOS Blender 5.2.2 LTS CLI codified: skill
  pointed at the Homebrew cask, environment recorded as AGENTS rules +
  `docs/blender_cli_macos.md`, probe script added and run (round-trip +
  all five helpers verified on 5.2.2).
- **#13** — All Blender work consolidated into `_blender/` (tools / polish /
  tests, see §3) and the first indoor test room scene added.
- **#15** (gralmurdok) — Follow-ups: `build_matlib.py` ported to resolve the
  repo root from `__file__` (any checkout), the PR #10 proof render that had
  been lost to the `renders/` gitignore restored to `_blender/polish/`,
  `_blender/` layout documented. Render output gitignored in the same
  period (8d75fae).

## 7. Indoor test room (PRs #13, #17, landed)

Full detail: `docs/indoor-room-real-materials-2026-10-02.md`.

- First assembly (PR #13) rendered with flat placeholder colours and LOD
  stripe artefacts — the room meshes had been collapsed to a single
  placeholder material and all LOD1–5 / UCX copies (41 extra meshes) left
  render-visible.
- Rebuild (PR #17, `_blender/tools/rebuild_indoor_room.py` →
  `indoor_room_real.blend`): canonical LOD0 material slots and per-face
  material indices harvested from the source FBXs (verified per mesh by
  per-face area ratios, ≥99% within 2%), five materials rebuilt from the
  real matlib, the 41 extra meshes deleted, and a units bug fixed in the
  rebuilt file (FBX empties at 0.01 scale vs metre-scale cameras/lights).
  Texture paths stored relative to the `.blend`.
- **Measured, not fixed:** raycast coverage (5 cm grid) shows the pieces do
  not fully close the room — ~0.8 m hole SW, ~0.95 m open strips NW and NE,
  ~0.6 m SE; north wall fully closed; no ceiling piece; a light-leak line at
  the wall base. Suggested fix (one segment short on the side walls, a short
  segment west of the door frame) is recorded in the detail doc and still
  open (§11). One DoorFrame01 face measures ~10% larger than source — kept
  the canonical material, flagged for the Unity check.
- The tracked source scene `indoor_room.blend` still carries the 0.01-scale
  bug; the rebuild fixed it only in `indoor_room_real.blend`.

## 8. Performance POCs 1–6 (2026-10-02 → 2026-10-04)

Blender POC work ran on the Linux VM; Unity-side checks on the owner's
machine. POC scripts and outputs are workspace-side (`poc-outputs/`,
`.perf-sample/`); the winners were productised into the PRs noted.

| POC | Question | Verdict |
|---|---|---|
| 1 | Zombie LODs + skin-weight cap | Cap 7→4 influences; Decimate LODs LOD0 25,108 / LOD1 12,552 / LOD2 6,272 tris, 65 bones. **Productised as PR #16** (`build_character_lods.py`; LOD metas completed later by PR #33) |
| 2 | LOD-chain padding | Duplicate detection must be order-independent. Trimmed Trim01, Roof01, wall03 LOD padding — sample total −38.4% bytes |
| 3 | UCX collision census (72 FBXs) | 55 UCX objects: 37 faced hulls convertible to convex MeshColliders as-is; 18 faceless point clouds (4 need hand-authored BoxColliders). **Productised as PR #18**: 18 faceless UCX stripped (`strip_dead_ucx.py`) + UCX→collider import postprocessor |
| 4 | Texture sizes | 18 textures at 4096² (305 MB source); the AmmoCase set is the outlier. **Productised as PR #19**: `TextureImportEnforcer` caps AmmoCase at 1024 + streaming mipmaps ON for `Assets/ImportedContent` — carried through to Unity 6000 by PR #33 (§10) |
| 5 | LOD pipeline choice | Budget allocation beats tool choice: whole-mesh beats per-material-part by 2–3×. At 50%: meshopt-whole 6.4 mm and Decimate-whole 7.5 mm mean error both beat the shipped UE LOD1 (9.2 mm). **Decision: keep shipped kit chains (trim padding per POC 2); Blender Decimate whole-mesh is the default for chain-less meshes; Unity Mesh LOD is the zero-maintenance option for props without chains** |
| 6 | Instant Meshes 2026 (2026-10-04) | **Rejected** for hard-surface kit meshes: asked for 1,677 quads, got 1,329 at 315 mm mean error (19.1% of points within 20 mm) vs 6.4–7.5 mm for the POC 5 winners, and the output has zero UVs and no skinning. Only lane: smooth organic sculpts before UV/rig work. No repo changes |

## 9. House builds

### 9a. Use-every-piece research (2026-10-03, workspace-only)

Research into building with **all 50 kit pieces** (owner constraint: local
kit assets only, no external packs — §1 rule 6). Caveat recorded with the
research and still binding: its piece geometry was *not* measured — exact
geometry must be probed from the FBXs in Blender before building (that
probing is what `docs/kit-crafting.md` later did for the new mansion).

### 9b. First build — two-story house (PRs #20–#27, landed, then **removed**)

Assembled from the building kit in `_blender/tests/` (build script, `.blend`,
15 proof renders):

- **#20** Two-story house: 14.22 × 12.25 m footprint on a raised terrace,
  85 placements, 30 of the 50 kit pieces, ridge at 16.28 m.
- **#21** Measurement fixes: facade fins clipped, hall pinch and room
  proportions corrected (a measurement audit against real-home ranges found,
  among others, a 0.36 m impassable hall pinch and a study 5 cm under the
  2.13 m habitable minimum — both fixed in this pass).
- **#22** +8 m east wing → 22.22 m mansion: primary suite, guest wing,
  laundry; corner seam slots plugged.
- **#23** Use-every-piece stage 1: two-story canted bay + primary balcony.
- **#24/#25** Stage 3 (interiors): wall01 spines, decay room, Stair02
  mezzanine (two PRs carried this title; one stage-3 landing is on `main`).
- **#26** Polish pass: closet floor slot closed, seal audit, refreshed
  interior renders.
- **#27** Stage 2: mansard conversion + site structures.

### 9c. Restart — kit census (PR #28, landed)

The house build was **removed from `main`** at the author's direction to
restart the mansion from scratch: `build_two_story_house.py`, the house doc,
the `.blend` and the 15 proof renders are gone from `main` (recoverable from
git history at 8b76d2d; the stage-2 final blend is also backed up
workspace-side). Added instead: `_blender/tools/build_kit_overview.py` +
`_blender/tests/kit_overview.blend` — the kit census: all 50 pieces, LOD0,
labelled 10×5 grid, real materials. This census is the reference the new
mansion is built against.

### 9d. New mansion (PRs #29–#32, landed — current build)

Bottom-up, pair-by-pair, whole pieces only. Full record, datasheets and the
pair matrix: `docs/kit-crafting.md`. Build:
`_blender/tools/build_new_mansion.py` → `_blender/tests/new_mansion.blend`.

- **#29 Step 1** — Foundation + south wall: `Wood_Internal_Floor01` slab
  (external Floor0x rejected — open deck platforms), living floor z 2.05,
  slab raycast-sealed (0 misses); door show-face fix (−0.11 y) so brick
  faces sit coplanar.
- **#30 Step 2** — Refined foundation: footprint 14.08 × 12.25 derived from
  the wall-run module sum; slab grid shifted so cuts bury under walls;
  cap-UV repair for bisect fills (`repair_cap_uvs` — `edgenet_fill` caps are
  born UV-less); south run exact 5-piece closure
  (Corner01 + WindowFrame01 + DoorFrame01 + WindowFrame01 + Corner01, zero
  residuals); Corner01 handedness resolved (left rot 0, right rot 90;
  Corner02 probed same-hand — no mirror-hand corner exists in the kit).
  The crafting doc gains the **golden rule** (pieces snap, sizes never
  adjust) and a datasheet for every placed/probed piece.
- **#31 Step 3** — Full ground-floor perimeter on whole-piece math: 16 wall
  pieces + slab + entrance stairs, footprint 14.08 × 12.07 (north run's 1 cm
  surplus centred, 5 mm/side). Corner01 is chiral — all four rotations used
  exactly once (SW 0 / SE 90 / NE 180 / NW 270). No-cut slab (4 whole
  tiles), seal scan 0 misses, in-build asserts (run-sum closure, show-face
  coplanarity ×4, footings, doorway ray, stairs landing 2.02 vs sill 2.05).
- **#32 Offset pass** — Walls inset 0.025, measured from probed quoin relief
  0.024, so wall flats sit coplanar with corner flats and quoins stand
  uniformly proud (a 0.15 inset was tried first and reverted — it broke the
  quoin-flush rhythm; corner seating by raw brick extremes was also tried
  and reverted — it drags piers ~3 cm off their butt joints). Pale corner
  caps confirmed as kit concrete coping trim awaiting the upper floor, not
  defects.

**Continuity checkpoint** (from `docs/kit-crafting.md`, current as of
PR #32): ground-floor perimeter complete — 14.08 × 12.07, slab top z 2.05,
16 wall pieces, stairs centred. Ranked next steps: **pair 4** (stacked
second-floor joint, covers the coping caps) > **pair 2** (straight
wall+wall joint — no such joint exists yet) > **glass pass** (the kit ships
no glass for wall windows) > **pair 6** (roof modules). Open threads:
hairline sliver left of the arch (GUI confirm), WindowFrame01 raycast
anomaly (fresh-import rays report its opening blocked while renders show it
clean — renders are truth for this piece until the triangles are explained).

**Measurement lesson (PR #31):** plain `matrix_world` reads can lag data
transforms in background mode — measure via the evaluated mesh
(`brick_extreme()`).

## 10. Unity-side follow-through of the Blender findings (PR #33, landed)

Not Blender work itself, but it closes two Blender-era threads on
Unity 6000.3.23f1: the `TextureImportEnforcer` from PR #19 was ported to the
Unity 6000 TextureImporter API (`SetPlatformTextureSettings`), the POC 4
caps were re-enforced across the ImportedContent metas (streaming mipmaps
ON, AmmoCase 1024), and the missing `ZombieModel_LOD1/2.fbx.meta` files for
the POC 1 / PR #16 LOD chain were added. Author-verified: recompile clean,
EditMode 108/108 + PlayMode 136/136, StandaloneWindows64 build OK.

## 11. Workspace-only Blender experiments (not in this repo)

- **Quaternius animation retarget — PASSED (2026-10-09).** UAL2
  `Zombie_Walk_Fwd_Loop` (41 frames @ 30 fps, CC0) retargeted onto the
  zombie's 65-bone mixamorig rig in Blender 5.2.2 headless: UAL2's Unity FBX
  uses UE bone names — 52/52 deform bones mapped 1:1, baked with world-space
  rest-delta retargeting, hips travel scaled by height ratio 1.068. Loop
  closes (frame 1 = frame 41); exported FBX re-imports with live animation.
  Traps: the source faces +Y vs Mixamo's −Y and rotating the source object
  silently fails — align with Rz(180) in the matrix math; Workbench/EEVEE
  rendering crashes on the Linux VM (no libEGL) — Cycles CPU works. Script
  and outputs are workspace-side (`quaternius-test/`); nothing was committed
  here, and no decision to adopt the animations is recorded.
- **Kiln procedural props — tested, cleaned up (2026-10-09 → 2026-10-11).**
  Six low-poly props (lantern, barrel, campfire ring, mushroom cluster,
  arcade cabinet, treasure chest) built with Kiln, all QA-accepted and
  Blender 5.2.2 re-import-verified with identical poly counts; gltfpack
  polish cut sizes ~40–50%. The test tree was deleted at the owner's word
  (models were test-only); the learnings stand: Kiln is real and usable for
  procedural low-poly props, and its Unity handoff is Built-in RP only, not
  URP-qualified. No repo changes.

## 12. Validation limits (standing)

- Everything above was verified in Blender (geometry checks, raycast/seal
  scans, proof renders). Except where a PR says otherwise (PR #33), none of
  it was opened in Unity by the Blender sessions — in-engine look,
  especially world-aligned seams, needs the check on the Unity machine.
- Albedo-only renders are the colour truth; the full PBR path is trusted
  only where the normal/ORM path has already been verified.
- Renders are truth over raycast for WindowFrame01 (anomaly in §9d).

## 13. Open threads (consolidated)

1. **New mansion next steps** — pair 4 stacking > pair 2 straight joint >
   glass pass > pair 6 roof (§9d), plus the arch-sliver GUI confirm.
2. **Indoor room gaps** — the measured holes and missing ceiling (§7);
   suggested fix recorded in the detail doc; source scene's 0.01-scale bug
   also still open there.
3. **WorldAlignedLit shader review** — normal projection on angled faces,
   instancing/batching, shadow caster (§5). WF03 (angled geometry) still
   unassessed for reveals/UVs — do not reuse the WF01/02 repair blindly.
4. **Zombie textures** — the audit's root suspect (§4): dead Mixamo paths +
   missing `.meta` remapping; legacy specular/gloss maps in a URP project.
5. **POC follow-ups not productised** — POC 2's trim pattern is the standing
   guidance for shipped chains; POC 3's 4 faceless hulls still need
   hand-authored BoxColliders if those pieces are used with collision.
6. **Quaternius animations** — retarget proven (§11); adoption into the
   game is undecided.

## 14. PR index (Blender and Blender-adjacent, all merged via rebase)

#1 normal-map blue channel · #2 reveal strips · #3 brick-size match ·
#4 session doc · #5 polish skill · #6 vertical bricks · #7 WF01 degenerate
face · #8 degenerate faces, floors/stairs · #9 WorldAlignedLit passes ·
#10 flipped faces ×19 FBXs · #11 helpers into skill · #12 macOS Blender CLI
(skill) · #13 `_blender/` consolidation + indoor room · #14 macOS Blender
CLI (AGENTS + reference doc) · #15 consolidation follow-ups · #16 zombie
LOD chain + weight cap · #17 indoor room real materials · #18 UCX strip +
collider postprocessor · #19 texture import enforcer · #20 two-story house ·
#21 house measurement fixes · #22 east-wing expansion · #23 canted bay +
balcony (stage 1) · #24/#25 interior stage 3 · #26 house polish pass ·
#27 mansard + site structures (stage 2) · #28 kit census + house removal ·
#29 new mansion step 1 · #30 new mansion step 2 · #31 full perimeter ·
#32 wall offset pass · #33 Unity 6000 enforcer port + LOD metas.
