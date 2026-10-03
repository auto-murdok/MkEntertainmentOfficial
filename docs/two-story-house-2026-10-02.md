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

## Pieces: 50 of 50 used (180 placements)

Every FBX in the building kit is placed at least once. The original
build used 30 pieces (85 placements): Roof01 ×7, Trim01 ×8, internal
wall03 ×8, wall02 ×6, Baseflor WindowFrame01 ×5, SecondFloor
WindowFrame01/02 ×4 each, internal DoorFrame01 ×4, Trim01_Corner ×4,
FloorPlank01 ×3, every exterior corner ×2 per floor, Baseflor
DoorFrame01 ×2 (front + garden doors), Baseflor wall01 / WindowFrame02
×2, SecondFloor wall01 ×2, FloorPlank02 ×2, internal corner01 ×2,
Wood_External_Floor01/03 ×2 each (platform), Internal_Stairs01,
External_Stairs01, Roof03 dormer, and all five internal floor slabs
(Wood01/02/03, Ceramic01/02).

The "use every kit piece" pass (stages 1–3 + the mansard/site stage,
below) added the remaining 20: WindowFrame03 ×2 (the bay, both floors),
SecondFloor DoorFrame01 (balcony), Internal_Stair02 (mezzanine flight),
internal wall01 ×2 + wall01_torn ×3 + wall01_torn02 (spines + decay
room), Wood_Internal_Floor04 (decay-room ceiling), Wood_External_Floor02
×2 (bay floors), and the whole mansard family — Roof08 ×4, Roof09 ×4,
Roof10 ×2, Roof_corner01 ×4, Roof02/05/06/07 + Roof_corner02/03 ×1 each,
Roof04 ×2 — while Roof01 rose to ×11 as the mansard's upper tier.

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

## Measurement audit & fixes (2026-10-02, follow-up)

Every room was re-measured from the built geometry (wall face planes,
raycast stair profiles, through-ray aperture grids) against the IRC-based
brief. The plan dimensions mostly match real-home ranges (living 20.7 m²,
bed2 11.2 m², bed3 14.2 m², guest WC 3.8 m², roof pitch 6.05/12, stair maths
closes: 18 risers × ~0.303 = 5.46 floor-to-floor), but measuring caught four
defects the proof views had missed. All four are fixed in the tool:

1. **Two interior walls protruded outside the east facade.** Run-fill
   overshoot let the last piece of two runs exit the building (guest-WC run
   to x 8.66, hall run to x 8.86 — damask-wallpaper fins on the brick). The
   tool now clips any internal-wall object that crosses the facade box back
   to the interior clear face (x ±6.65 / y ±5.66). Verified: zero facade
   violations; both pieces end flush at 6.65.
2. **Upper hall pinched to 0.36 m** at the main-bath corner post (corner01,
   1.19 m deep, protruding past the hall wall into the corridor, blocking
   the route to the suite and bath doors). The post is removed (spine + hall
   wall form a flush T-junction) and the hall wall moved 0.25 south
   (centerline −2.30 → −2.55): hall now 1.25 m at the west leg and 1.15 m at
   body height on the east leg (was 1.00 / 0.90; code minimum 0.914).
3. **Study 5 cm under the habitable minimum dimension** (2.08 vs IRC 2.13).
   Its spine moved 0.10 east (x 3.9 → 4.0): study is now 2.18 m wide, the
   main bath 2.34 m (still generous).
4. **Suite split inverted vs real homes** (primary bedroom 10.1 m², ensuite
   13.6 m²; norms ~18 / ~6). The divider moved y 2.06 → 3.26: bedroom
   4.14 × 3.63 = 15.0 m², ensuite 4.14 × 2.09 = 8.7 m².

Not fixable with this kit (documented, accepted): ceiling heights 5.15 /
5.50 m (norms 2.74 / 2.44), floor-to-floor 5.46, plinth 2.04, stair module
(risers 0.20–0.35 non-uniform vs 0.197 max, goings 0.47, width 3.2 — a
ceremonial stair), windows 1.75 × 2.60 with the upper sill at 1.18 (6 cm over
the 1.12 egress maximum), and the Roof03 dormer cap peaking ~1.57 m above
the main ridge (module designed for the source mansion's taller roof).

New/updated proof renders: `two_story_house_east_elevation.png` (fins
gone), `two_story_house_int_hall_upper.png` and
`two_story_house_ext_front_se.png` (regenerated).

## Expansion — +8 m east wing (2026-10-02)

The house grows along its length only (the Roof01 modules fix the 12.25 m
depth): footprint **22.22 × 12.25 m** (outer faces x −7.11 … +15.11), two
more roof modules per slope, gables and platform extended (terrace edge
column cut to fit). ≈ 5,460 sq ft total. The west block is unchanged; the
old east wall line (x 6.65) becomes interior, joined by kit arches.

New rooms (clear dimensions, measured from the built geometry):

- **Ground wing:** family room 7.69 × 4.36 ≈ 33.5 m² off the kitchen
  (arch), laundry 3.38 × 2.43 = 8.2 m² off the family room, and a guest
  wing: sitting 3.38 × 3.29 = 11.1 m², bedroom 3.69 × 3.29 = 12.1 m²,
  ensuite 3.69 × 2.43 = 9.0 m².
- **Upper wing — the primary suite:** bedroom 5.13 × 3.42 = 17.5 m² (the
  real-home build-to size), walk-in closet 1.94 × 4.36 = 8.5 m² absorbing
  the hall's east end, dressing room 3.38 × 6.34 = 21.4 m², ensuite
  3.69 × 2.43 = 9.0 m², private sitting room 3.69 × 3.29 = 12.1 m². The
  hall now runs the full length at 1.15–1.25 m wide; the mid-east suite
  (15.0 + 8.7 m²) becomes a second upstairs suite.
- **Wet stacking:** the primary ensuite sits directly over the guest
  ensuite; laundry sits under the dressing room.

**Also fixed — corner seam slots (pre-existing).** Outside-in raycasts
found vertical through-slots where three wall runs meet their corner
pieces: the corner arms are shorter than the runs assume (Corner01 reaches
1.04/1.02 m from its vertex, Corner02 1.21/1.18), leaving gaps of 0.12 m
(north face at the NE corner), 0.19 m (east face at NE) and 0.03 m (west
face at NW), on both floors — present in the pre-expansion house too. The
tool now plugs all three with flush brick piers (`SeamPlug`); re-probed:
every outside-in ray at the seams hits wall or plug at the facade plane,
and the facade-box check reports zero violations (149 objects).

Proof renders: `two_story_house_ext_front_se.png`,
`two_story_house_ext_back_nw.png`, `two_story_house_east_elevation.png`
and `two_story_house_int_hall_upper.png` (regenerated), plus new
`two_story_house_int_family.png` and `two_story_house_int_primary.png`.

## Bay window + primary balcony (2026-10-03)

Stage 1 of the "use every kit piece" pass — placements derived from
geometry measured off the FBXs (fit-check, 2026-10-03), not from the
file names:

- **Two-story canted bay (WindowFrame03, both floors).** The piece is
  not the corner turret its name suggests: it is a 10.0 m bay wall —
  flat three-window front, angled facets, short straight returns —
  brick outside, damask inside. One per floor, stacked on the south
  facade from x ≈ 3.94 to the SE corner, back plane flush with the
  interior face so the bay projects ≈ 1.85 m past the facade line. It
  serves the kitchen + family room below (the arch wall between them
  stops at the old facade line, so the bay reads as one open sun-bay)
  and the main bath + primary suite above. Kit-true floors: the ground
  bay floor is two `Wood_External_Floor02` pieces bisect-cut to the bay
  trapezoid (the tool first reassigns the piece's ~1% of faces on
  "Fbx Default Material" to its own wood material); the upper bay floor
  is two Wood02 slabs cut the same way. The bay projects past the main
  roof's eave line, so it carries a flat trapezoid lid (`BayCap`, roof
  material) just above its wall top; the south string-course trim stops
  at the bay, as real bays break the course.
- **Primary balcony (SecondFloor DoorFrame01).** Swapped into the
  upper east run — DoorFrame01 + wall01 + WindowFrame01 tiles the run
  exactly — opening from the primary suite's walk-in closet end. The
  deck is a `Wood_External_Floor01` slab cut to 1.40 × 4.61 m (its
  platform skirt bisected away) carried on two timber posts and a beam;
  the railing is built from prisms in the deck's wood material.

Object count 149 → 163. Verified: no geometry protrudes past the outer
envelope outside the designed projections (bay, balcony, roof, terrace,
stairs); outside-in rays seal at the bay facets and returns (the window
apertures are open frames, like every window piece in the kit); the
balcony rails are hit at their design heights.

Proof renders: `two_story_house_ext_front_se.png` and
`two_story_house_east_elevation.png` (regenerated), plus new
`two_story_house_int_bay_family.png` and
`two_story_house_int_primary_bay.png`.

## Interior stage — wall01 spines, decay room, mezzanine (2026-10-03)

Stage 3 of the "use every kit piece" pass:

- **wall01 spines.** The kit's 6.0 m single-piece interior wall forms
  the first 6 m of the two long wing runs (family-room north wall at
  ground, second-suite hall wall above), replacing wall02/wall03
  chains with the same coverage.
- **Decay-gradient room (guest bedroom).** The route in stays pristine
  (sitting room), then decays: the bedroom's south wall is
  `wall01_torn02`, its torn geometry protruding past the standard wall
  band; its west wall is `wall01_torn` with the room's doorway cut from
  the piece itself (two side pieces plus a header above the opening,
  keeping the original gap at y 3.53..4.42); its ceiling is the
  underside of a `Wood_Internal_Floor04` sandwich — intact wood floor
  for the room above, part-broken ceiling boards over the bedroom.
- **Family-room mezzanine.** `Internal_Stair02` is a 16.4 m imperial
  stair that fits nowhere whole; its centre flight is bisected out
  (3.2 m wide, torn carpet runner and all) and laid along the family
  room's north wall, rising east — treads probed at 2.93 / 3.49 / 4.07
  to a landing plateau at 5.25 — onto a Wood02 gallery deck (top
  z 5.15) in the room's east end, carried on a post and beam with a
  prism guard rail along its open edge. Headroom: 2.80 m under the
  deck, 2.04 m on it.

Proof renders: new `two_story_house_int_mezzanine.png` and
`two_story_house_int_decay_room.png`.

## Polish pass — seal audit, floor-slot fix, refreshed interiors (2026-10-03)

A full audit of the merged build (169 objects), with one real defect found and fixed:

- **Envelope seal sweep** — ~1,100 outside-in rays on the solid wall bands of all four
  facades plus junction probes at the bay returns and balcony door: **no through-slots**.
  The only deep readings are Corner02's sculpted quoin pockets (every such ray still
  hits the corner piece itself) and the roof eaves shadowing the z 12.2 band.
- **Floor-coverage scan** — a down-ray grid over the whole upper floor found a
  **0.48 m through-slot in the primary closet's floor** (x 13.34..13.82, full strip
  depth): the south strip's Wood02 slab really ends at x 13.34 and the filler's clean
  edge starts at 13.82, so the nominal 13.79 placement left a gap you could fall
  through to the family room (or onto the mezzanine deck). The filler now starts at
  **13.35** — slot verified closed, rescan clean. (Every other low reading on the scan
  is accounted for: the stairwell and its treads, and Floor04's ragged north rim.)
- **Noted kit character, left as designed**: Floor04's top edge has plank pinholes
  along the dressing room's north wall line (the piece's broken rim — reads as
  damage, matches its broken-ceiling underside), and the Wood02 floor piece carries
  a 0.25 m wide, 0.22 m deep recessed channel across the primary bedroom (its own
  surface detail, solid throughout).
- **Refreshed renders**: `two_story_house_int_family.png` (reframed — wide elevated
  shot: flight, gallery deck, and bay wall in one frame) and
  `two_story_house_int_primary.png` (reframed to include the bay and, through the
  closet arch, the balcony door) regenerated; new
  `two_story_house_int_decay_ceiling.png` shows the Floor04 ceiling from the decay
  room — cracked plaster, cornice, and the torn walls' peeled tops.

## Mansard conversion + site structures (2026-10-03)

Stage 2 of the "use every kit piece" pass — the mansard family and the
remaining roof pieces, all placed from LOD0 geometry measured in
Blender (see the LOD warning at the end):

- **Main roof → mansard.** The kit's mansard sections (Roof09 eave,
  Roof08 dormer, Roof10 dormer, Roof08, Roof09 per side) form a steep
  bell-cast lower tier on both slopes, eaves at the wall line, cut at
  the spring line (|y| 2.15) where the bell curve meets the existing
  Roof01 slope plane — the Roof01 modules (×11, incl. the Roof03 dormer
  module) continue as the shallow upper tier to the unchanged ridge
  (16.28). The gable ends are re-profiled to the same curve (sampled
  from the pieces, dense to the spring line, analytic above it).
- **Pavilion (south garden).** Roof05 is a complete mansard roof for a
  ~14 × 8 building: set on six posts over a Floor03 plinth, glazed
  dormer to the garden. A Roof06 slope band, sliced from the fragment,
  cantilevers a canopy off its north eave on two posts.
- **Carport (north garden).** Roof04 is a mansard *half*-section (one
  slope + flat deck + one brick gable end + cresting); two halves
  back-to-back on six posts + twin beams form the complete roof —
  verified by a raycast heightmap of the finished footprint (paired
  slopes, central deck, eaves both sides).
- **Kiosk (west garden).** Four Roof_corner01 hip corners quartered to
  a centre point make a slate pyramid roof on posts, closed by a cap
  prism.
- **Entrance gate (south).** Roof07 is a complete small hip cap (bell
  slopes, rusty deck, timber eave beam) — set whole over two brick
  piers. The piers are built by a dedicated helper that remaps the
  brick UVs per face; the generic prism UVs (from plan x/y) smear on
  tall faces.
- **Entry porch.** Roof_corner02 is a mansard *end module*: brick
  gable + pitched cross-gable bay that morphs into a half-section
  along its length (its LOD0 sections were measured station by
  station). One module roofs the porch — gable to the street, open end
  embedded in the facade, east slope sliced clear of the bay with the
  cut edge landed on a raised beam + post (raycast-verified closure).
- **East pavilion.** Roof_corner03 (the morphing twin) as an open
  garden folly: only the gable bay is kept, the cut end closed by a
  timber profile plate (`profile_plate`, outline traced from the
  measured section) carried on posts — a raw bisect end on these
  shell pieces leaves the roof cavity open, as the first attempt
  showed.
- **Garden-door hood.** Roof02 is mostly void in source coordinates;
  its one solid sloped band is sliced into a hood over the east garden
  door on two wood corbels.

Final build: 256 objects, 180 imports — 50 of 50 pieces placed.

**LOD warning (cost a day):** the kit's LOD1+ meshes *simplify away
the corner modules' morphing sections* — LOD1 of Roof_corner03 is a
uniform half-section its whole length, while LOD0 transitions from a
pitched cross-gable to the bell section. Any probe that imports an
FBX and raycasts the whole stack measures the simplified LODs, not
the geometry the build actually places (LOD0). All fit-checks for
build geometry must isolate LOD0.

Proof renders: refreshed `two_story_house_ext_front_se.png`,
`two_story_house_ext_front.png`, `two_story_house_ext_back_nw.png`,
plus new `two_story_house_site_structures.png` and
`two_story_house_east_pavilion.png`.
