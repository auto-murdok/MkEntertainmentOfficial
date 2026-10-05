"""New mansion, from scratch — bottom-up construction on a validated foundation.

Step 1 (this file, current): ground-floor slab only. Starter footprint is the
old west block (14.22 x 12.25, centered at origin), slab top at z = 0, so
later walls sit at 0 and the building can still grow east like before.
Walls/joints land in later steps, each proven on this slab first.

Usage:
  blender --background --python _blender/tools/build_new_mansion.py
Output: _blender/tests/new_mansion.blend
"""
import bpy, bmesh, os, re, json, math, glob
from mathutils import Vector, Matrix

TOOLS_DIR = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(TOOLS_DIR, "..", ".."))
MESHDIR = os.path.join(REPO, "Assets/ImportedContent/Building_kit/Meshes")
MATLIB = os.path.join(REPO, "_blender/polish/matlib_building_kit.json")
OUT_BLEND = os.path.join(REPO, "_blender/tests/new_mansion.blend")
matmap = json.load(open(MATLIB))

# Starter footprint (m): DERIVED from the wall-run module sum (golden rule —
# dimensions flow piece->design). South run, whole pieces, exact closure:
# Corner01 + 2 walls + door + 2 walls + Corner01.
# Corner01 is HANDED (probed): finished outer faces S+W, receiving rebates
# N+E — left corner rot 0; right corner rot 90 (outers S+E, rebates N+W).
# Rotated footprint along x is 1.03, hence per-side halves.
# Total: 1.04 + 4.00 + 4.01 + 4.00 + 1.03 = 14.08.
CORNER_L, CORNER_R, WALL_W, DOOR_W = 1.04, 1.03, 2.00, 4.01
HALF_L = CORNER_L + 2 * WALL_W + DOOR_W / 2
HALF_R = CORNER_R + 2 * WALL_W + DOOR_W / 2
FP_W = HALF_L + HALF_R  # 14.08
# Depth derives from the SIDE runs the same way: corner + window + wall +
# wall + window + corner = 1.03 + 4 + 2 + 2 + 4 + 1.04 = 14.07.
FP_D = 1.03 + 4.00 + 2.00 + 2.00 + 4.00 + 1.04  # 14.07
SLAB_TOP = 2.05
# South edge pinned (door/stairs/verified work); growth absorbs north.
SLAB_EDGE = -6.125
NORTH_EDGE = SLAB_EDGE + FP_D  # 7.945
WEST_EDGE, EAST_EDGE = -FP_W / 2, FP_W / 2  # -7.04 .. 7.04
# Wall inset: runs sit inside the slab edges (foundation ledge), corners
# stay flush (proud quoin piers). INSET equals the measured quoin relief
# (0.024): wall flats land coplanar with corner flats, quoins stand uniformly
# proud. Corners overlap the shifted runs — embedded, never a gap.
INSET = 0.025
SOUTH_PLANE = SLAB_EDGE + INSET
NORTH_PLANE = NORTH_EDGE - INSET
WEST_PLANE = WEST_EDGE + INSET
EAST_PLANE = EAST_EDGE - INSET

bpy.ops.wm.read_factory_settings(use_empty=True)

# ---------------------------------------------------------------- pieces ----
def import_piece(fbx):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=os.path.join(MESHDIR, fbx))
    kept = []
    for o in [o for o in bpy.data.objects if o not in before]:
        if o.type == 'MESH' and not o.name.upper().startswith("UCX") \
                and not re.search(r"_LOD[1-9]\d*$", o.name, re.I):
            kept.append(o)
        else:
            bpy.data.objects.remove(o, do_unlink=True)
    for o in kept:
        mw = o.matrix_world.copy()
        o.parent = None
        o.matrix_basis = mw
    if not kept:
        raise RuntimeError("no LOD0 mesh kept for " + fbx)
    return kept

def combined_bbox(objs):
    mn = Vector((1e18,) * 3); mx = Vector((-1e18,) * 3)
    for o in objs:
        for c in o.bound_box:
            w = o.matrix_world @ Vector(c)
            mn = Vector(map(min, mn, w)); mx = Vector(map(max, mx, w))
    return mn, mx

def place(objs, theta_deg, aabb_min_target):
    mn, mx = combined_bbox(objs)
    anchor = mn
    R = Matrix.Rotation(math.radians(theta_deg), 4, 'Z')
    corners = [Vector((x, y, z)) for x in (mn.x, mx.x) for y in (mn.y, mx.y) for z in (mn.z, mx.z)]
    rot = [R @ (c - anchor) for c in corners]
    rmin = Vector((min(c[i] for c in rot) for i in range(3)))
    t = Vector(aabb_min_target) - rmin
    M = Matrix.Translation(t) @ R @ Matrix.Translation(-anchor)
    for o in objs:
        o.matrix_basis = M @ o.matrix_basis
    bpy.context.view_layer.update()
    return combined_bbox(objs)

def bake(objs):
    for o in objs:
        mw = o.matrix_world.copy()
        o.matrix_basis = Matrix.Identity(4)
        o.data.transform(mw)
        o.data.update()

def bisect_cut(objs, plane_co, plane_no):
    for o in objs:
        bm = bmesh.new(); bm.from_mesh(o.data)
        bmesh.ops.bisect_plane(bm, geom=list(bm.verts) + list(bm.edges) + list(bm.faces),
                               plane_co=plane_co, plane_no=plane_no, clear_outer=True)
        bnd = [e for e in bm.edges if len(e.link_faces) == 1]
        if bnd:
            try:
                bmesh.ops.edgenet_fill(bm, edges=bnd)
            except Exception:
                pass
        bm.to_mesh(o.data); bm.free(); o.data.update()
    bpy.context.view_layer.update()

# ------------------------------------------- step 1: terrace platform --
# The slab is DECOUPLED from the wall footprint: a 3x3 grid of WHOLE tiles,
# zero cuts, building sitting on top with a terrace margin (the old mansion's
# pattern). Wall modules (2.00/4.01/1.04) and slab tiles (6.71/6.36) are
# incommensurate — no shared span closes on both, so the platform overhangs
# instead of matching. Stairs land on the terrace (top steps merge flush).
print("== step 1: terrace platform (3x3 whole tiles, zero cuts)")
slab_objs = []
SLAB_FBX, TILE_X, TILE_Y = "SM_Wood_Internal_Floor01.fbx", 6.71, 6.36
PLAT_X0, PLAT_Y0 = -10.065, -6.2  # centered x; south edge just off the wall
for ix in range(3):
    for iy in range(3):
        objs = import_piece(SLAB_FBX)
        place(objs, 0, (PLAT_X0 + ix * TILE_X, PLAT_Y0 + iy * TILE_Y, -2.0))
        bake(objs)
        slab_objs += objs
assert len(slab_objs) == 9, f"platform must be 9 whole tiles, got {len(slab_objs)}"

def repair_cap_uvs(objs, scale=0.15):
    # edgenet_fill caps are born without UVs (all loops at one texel ->
    # white patches). Give zero-UV-area faces a top-down planar map so cut
    # faces sample real wood instead of a single pale texel.
    for o in objs:
        me = o.data
        uv = me.uv_layers.active
        if uv is None:
            continue
        for p in me.polygons:
            loops = [uv.data[i].uv for i in range(p.loop_start, p.loop_start + p.loop_total)]
            umin = min(u[0] for u in loops); umax = max(u[0] for u in loops)
            vmin = min(u[1] for u in loops); vmax = max(u[1] for u in loops)
            if (umax - umin) + (vmax - vmin) < 1e-6:
                for i in range(p.loop_start, p.loop_start + p.loop_total):
                    v = me.vertices[me.loops[i].vertex_index]
                    uv.data[i].uv = (v.co.x * scale, v.co.y * scale)
repair_cap_uvs(slab_objs)  # no-op safeguard: no bisect ran, no caps exist
print("  slab: 9 whole tiles, zero cuts")
# rest slab top exactly on SLAB_TOP
mn, mx = combined_bbox(slab_objs)
dz = SLAB_TOP - mx.z
if abs(dz) > 1e-6:
    M = Matrix.Translation(Vector((0, 0, dz)))
    for o in slab_objs:
        o.matrix_basis = M @ o.matrix_basis
    bpy.context.view_layer.update()
mn, mx = combined_bbox(slab_objs)
print(f"  slab: x {mn.x:.2f}..{mx.x:.2f} y {mn.y:.2f}..{mx.y:.2f} top z {mx.z:.3f}")

# ------------------------------------------------------------ seal scan ----
# Drop rays on a 0.5 m grid over the footprint; every ray must hit the slab
# (the PR 26 closet-slot lesson: scan on day one, not at the end).
bpy.context.view_layer.update()
deps = bpy.context.evaluated_depsgraph_get()
slab_names = set(o.name for o in slab_objs)
# seal scan over the whole platform: whole tiles => any miss is a bad tile
misses = []
gx = PLAT_X0 + 0.75
while gx < PLAT_X0 + 3 * TILE_X - 0.5:
    gy = PLAT_Y0 + 0.75
    while gy < PLAT_Y0 + 3 * TILE_Y - 0.5:
        hit, loc, no, idx, ob, mat = bpy.context.scene.ray_cast(
            deps, Vector((gx, gy, 8.0)), Vector((0, 0, -1)))
        # only slab hits count — ground-plane hits are gaps, not floor
        if not hit or ob.name not in slab_names:
            misses.append((round(gx, 2), round(gy, 2)))
        gy += 0.5
    gx += 0.5
print(f"  seal scan: {len(misses)} misses")
for m in misses[:10]:
    print(f"    miss at {m}")
if misses:
    raise RuntimeError(f"slab has {len(misses)} through-slots — fix before walls")

# platform coverage: building must sit on the platform with terrace margin
mn, mx = combined_bbox(slab_objs)
print(f"  platform: x {mn.x:.2f}..{mx.x:.2f} y {mn.y:.2f}..{mx.y:.2f}")
assert mn.x < WEST_EDGE - 2.5 and mx.x > EAST_EDGE + 2.5, "terrace margins"

# --------------------------------------- step 2: south wall run + entrance --
# GOLDEN RULE: whole pieces only — no bisecting, no scaling, no filler.
# The run closes on module sums: corner + 2 walls + door + 2 walls + corner.
# The footprint derives from the pieces (never the reverse). Sub-cm
# residuals distribute symmetrically and are documented, not cut away.
print("== step 2: south wall + entrance")
assert abs(FP_W - (HALF_L + HALF_R)) < 0.01, f"footprint {FP_W} != run sum"
assert abs(FP_D - (1.03 + 4.00 + 2.00 + 2.00 + 4.00 + 1.04)) < 0.01, "depth != side sum"
wall_objs = []

THICK = 0.46  # wall/window piece thickness

def run_piece(fbx, x0, y0, theta=0):
    objs = import_piece(fbx)
    # facing convention (probed): brick show-face at local y 0 for walls,
    # windows and door; the door piece's bbox min is trim at y -0.11 — shift
    # it so the BRICK faces land coplanar (trim ends proud: the surround)
    if "DoorFrame01" in fbx:
        if theta == 0:
            y0 -= 0.11
    place(objs, theta, (x0, y0, 0.0)); bake(objs)
    wall_objs.extend(objs)
    return objs

def brick_extreme(o, deps, axis, side):
    # truthful world-frame brick extreme via the evaluated mesh (plain
    # matrix_world reads can lag data transforms a step behind in
    # background mode — evaluation IS the refresh, so this can't be stale)
    o_eval = o.evaluated_get(deps)
    mesh = o_eval.to_mesh()
    vals = []
    slots = [s.material for s in o.material_slots]
    for p in mesh.polygons:
        m = slots[p.material_index] if p.material_index < len(slots) else None
        if m and "Brick" in m.name:
            for vi in p.vertices:
                vals.append(mesh.vertices[vi].co[axis])
    o_eval.to_mesh_clear()
    if not vals:
        return None
    return max(vals) if side > 0 else min(vals)

def seat_object(o, deps, axis, plane, side):
    # seat one object by its measured brick extreme (rigid data nudge).
    # Returns the residual, or None when the piece has no brick.
    got = brick_extreme(o, deps, axis, side)
    if got is not None and abs(got - plane) > 0.02:
        print(f"  seat {o.name}: brick {got:.3f} -> {plane:.3f}")
        for v in o.data.vertices:
            v.co += (plane - got) * Vector((1 if axis == 0 else 0, 1 if axis == 1 else 0, 0))
        o.data.update()
        return brick_extreme(o, bpy.context.evaluated_depsgraph_get(), axis, side)
    return got

def assert_showface(label, new_objs, axis, plane, side):
    # each run piece's brick show-face must sit on its edge plane —
    # except corner piers (brick wraps them — seated explicitly per-axis
    # below instead). Trim/moldings can stand proud of the brick on flipped
    # orientations: seat by measured extreme, never resize.
    deps = bpy.context.evaluated_depsgraph_get()
    for o in new_objs:
        if "Corner" in o.name:
            continue
        seat_object(o, deps, axis, plane, side)
    deps = bpy.context.evaluated_depsgraph_get()
    for o in new_objs:
        if "Corner" in o.name:
            continue
        got = brick_extreme(o, deps, axis, side)
        if got is not None and abs(got - plane) > 0.02:
            raise RuntimeError(f"{label}: show-face off plane on {o.name} (got {got:.4f})")
    print(f"  {label}: show-faces coplanar")

def seat_corners(label, specs):
    # corners seat per-axis against their adjacent runs' planes:
    # specs = [(objs, axis, plane, side)]. Brick wraps the pier, so each
    # corner aligns once per outward face and never moves laterally.
    deps = bpy.context.evaluated_depsgraph_get()
    for objs, axis, plane, side in specs:
        for o in objs:
            got = seat_object(o, deps, axis, plane, side)
            if got is not None and abs(got - plane) > 0.02:
                raise RuntimeError(f"{label}: corner off plane {o.name} (got {got:.4f})")
    print(f"  {label}: corners seated")

# south run (brick faces -y): corner + window + door + window + corner.
# Straight pieces on SOUTH_PLANE; corners stay on the slab edges (proud).
sq = []
cSW = run_piece("SM_ExternalWall_Baseflor_Corner01.fbx", -HALF_L, SLAB_EDGE, theta=0)
sq += cSW
sq += run_piece("SM_ExternalWall_Baseflor_WindowFrame01.fbx", -HALF_L + CORNER_L, SOUTH_PLANE)
sq += run_piece("SM_ExternalWall_Baseflor_DoorFrame01.fbx", -DOOR_W / 2, SOUTH_PLANE)
sq += run_piece("SM_ExternalWall_Baseflor_WindowFrame01.fbx", DOOR_W / 2, SOUTH_PLANE)
cSE = run_piece("SM_ExternalWall_Baseflor_Corner01.fbx", HALF_R - CORNER_R, SLAB_EDGE, theta=90)
sq += cSE
assert_showface("south run", sq, 1, SOUTH_PLANE, -1)

print("== step 3: west + east runs (bury the slab strips)")
# west run faces -x (theta=-90: local brick min-y -> world min-x).
# Straight pieces on WEST_PLANE; corners stay.
SW_TOP = SLAB_EDGE + 1.03  # SW corner (rot 0) north extent
wq = []
wq += run_piece("SM_ExternalWall_Baseflor_WindowFrame01.fbx", WEST_PLANE, SW_TOP, theta=-90)
wq += run_piece("SM_ExternalWall_Baseflor_wall01.fbx", WEST_PLANE, SW_TOP + 4.00, theta=-90)
wq += run_piece("SM_ExternalWall_Baseflor_wall01.fbx", WEST_PLANE, SW_TOP + 6.00, theta=-90)
wq += run_piece("SM_ExternalWall_Baseflor_WindowFrame01.fbx", WEST_PLANE, SW_TOP + 8.00, theta=-90)
cNW = run_piece("SM_ExternalWall_Baseflor_Corner01.fbx", WEST_EDGE, SW_TOP + 12.00, theta=270)
wq += cNW
assert_showface("west run", wq, 0, WEST_PLANE, -1)
# east run faces +x (theta=+90: local brick min-y -> world max-x)
SE_TOP = SLAB_EDGE + 1.04  # SE corner (rot 90) north extent
eq = []
eq += run_piece("SM_ExternalWall_Baseflor_WindowFrame01.fbx", EAST_PLANE - THICK, SE_TOP, theta=90)
eq += run_piece("SM_ExternalWall_Baseflor_wall01.fbx", EAST_PLANE - THICK, SE_TOP + 4.00, theta=90)
eq += run_piece("SM_ExternalWall_Baseflor_wall01.fbx", EAST_PLANE - THICK, SE_TOP + 6.00, theta=90)
eq += run_piece("SM_ExternalWall_Baseflor_WindowFrame01.fbx", EAST_PLANE - THICK, SE_TOP + 8.00, theta=90)
cNE = run_piece("SM_ExternalWall_Baseflor_Corner01.fbx", EAST_EDGE - 1.04, SE_TOP + 12.00, theta=180)
eq += cNE
assert_showface("east run", eq, 0, EAST_PLANE, +1)

print("== step 4: north run (3 windows between the new corners)")
# north run faces +y (theta=180: local brick min-y -> world max-y).
# Infill span 12.01 vs 3 windows 12.00: centered, 5 mm each side (rule 5).
# Straight pieces on NORTH_PLANE; corners stay.
nq = []
NX0 = -6.01 + 0.005
for i in range(3):
    nq += run_piece("SM_ExternalWall_Baseflor_WindowFrame01.fbx",
                    NX0 + i * 4.00, NORTH_PLANE - THICK, theta=180)
assert_showface("north run", nq, 1, NORTH_PLANE, +1)

# NOTE (tried + reverted): seating corners by raw brick extremes is invalid —
# quoin relief (±3 cm) dominates the extreme, so "seating" drags whole piers
# centimetres off their butt joints. Corners align by placement + visual
# check only; the 11 mm SE step is quoin pattern, not offset.

# footing check: every wall base must sit on the slab (z 0, no daylight)
bpy.context.view_layer.update()
for o in wall_objs:
    ws = [o.matrix_world @ Vector(c) for c in o.bound_box]
    base = min(v.z for v in ws)
    if abs(base) > 0.02:
        raise RuntimeError(f"footing gap under {o.name}: base z {base:.3f}")
print(f"  {len(wall_objs)} wall pieces, footings on slab")

# doorway check: the entrance ray must now TERMINATE on the north run
# (proving the passage crosses the whole interior). Aim below the north
# windows' sill (~2.9) so the ray meets masonry, not the far opening.
deps = bpy.context.evaluated_depsgraph_get()
wall_names = set(o.name for o in wall_objs)
hit, loc, no, idx, ob, mat = bpy.context.scene.ray_cast(
    deps, Vector((0.0, -14.0, SLAB_TOP + 0.5)), Vector((0, 1, 0)))
print(f"  doorway ray: hit {ob.name if hit else 'nothing'}")
if not hit or ob.name not in wall_names or loc.y < 0:
    raise RuntimeError("doorway ray did not terminate on the north run")
print("  entrance passage proven end to end")

# exterior stairs: run along y, low end south, top landing (z 2.0) meeting
# the doorway sill (2.05). Shifted with the south run (landing on SOUTH_PLANE).
print("== step 2b: entrance stairs")
stair_objs = import_piece("SM_External_Stairs01.fbx")
place(stair_objs, 0, (-3.835, SOUTH_PLANE - 5.37, 0.0)); bake(stair_objs)
mn, mx = combined_bbox(stair_objs)
print(f"  stairs: x {mn.x:.2f}..{mx.x:.2f} y {mn.y:.2f}..{mx.y:.2f} top z {mx.z:.2f}")
if abs(mx.z - 2.02) > 0.1:
    raise RuntimeError(f"stairs top {mx.z:.2f} != sill 2.05 — landing mismatch")
# terrace junction: stair top must lap onto the platform (no gap, no step)
if not (mx.y - PLAT_Y0 > 0.05 and abs(mx.z - SLAB_TOP) < 0.06):
    raise RuntimeError("stairs do not land on the terrace")
print("  stairs land on the terrace")

# ---------------------------------------------------------------- ground ----
bpy.ops.mesh.primitive_plane_add(size=120, location=(0, 0, -2.05))
gp = bpy.context.active_object
gp.name = "Ground"
gm = bpy.data.materials.new("GroundMat")
gm.use_nodes = True
gm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.16, 0.18, 0.13, 1)
gm.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = 1.0
gp.data.materials.append(gm)

# -------------------------------------------------------------- materials ----
for _o in [o for o in bpy.data.objects if o.type == 'MESH' and o.name.startswith("SM_")]:
    _slots = [s.material for s in _o.material_slots]
    _dst = next((i for i, m in enumerate(_slots) if m and m.name.startswith("MI_")), None)
    if _dst is None:
        continue
    for _p in _o.data.polygons:
        _m = _slots[_p.material_index] if _p.material_index < len(_slots) else None
        if _m and _m.name.startswith("Fbx Default"):
            _p.material_index = _dst
print("  default-mat remap done")

def load_img(rel, noncolor=False):
    p = os.path.join(REPO, rel)
    for im in bpy.data.images:
        if os.path.abspath(im.filepath) == os.path.abspath(p):
            return im
    im = bpy.data.images.load(p)
    if noncolor:
        im.colorspace_settings.name = "Non-Color"
    return im

for mat in list(bpy.data.materials):
    base = re.sub(r'\.\d{3}$', '', mat.name)
    spec = matmap.get(base)
    if not spec:
        continue
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    if "albedo" not in spec:
        bsdf.inputs["Base Color"].default_value = (0.72, 0.82, 0.88, 1)
        bsdf.inputs["Roughness"].default_value = 0.06
        bsdf.inputs["Alpha"].default_value = 0.35
        if "Transmission Weight" in bsdf.inputs:
            bsdf.inputs["Transmission Weight"].default_value = 0.5
        continue
    alb = nt.nodes.new("ShaderNodeTexImage")
    alb.image = load_img(spec["albedo"])
    tint = spec.get("basecolor", [1, 1, 1])[:3]
    if any(abs(c - 1.0) > 1e-6 for c in tint):
        rgb = nt.nodes.new("ShaderNodeRGB")
        rgb.outputs[0].default_value = (*tint, 1.0)
        mix = nt.nodes.new("ShaderNodeMixRGB")
        mix.blend_type = 'MULTIPLY'
        mix.inputs[0].default_value = 1.0
        nt.links.new(alb.outputs["Color"], mix.inputs[1])
        nt.links.new(rgb.outputs[0], mix.inputs[2])
        nt.links.new(mix.outputs[0], bsdf.inputs["Base Color"])
    else:
        nt.links.new(alb.outputs["Color"], bsdf.inputs["Base Color"])
    if spec.get("normal"):
        ntex = nt.nodes.new("ShaderNodeTexImage")
        ntex.image = load_img(spec["normal"], noncolor=True)
        nmap = nt.nodes.new("ShaderNodeNormalMap")
        nt.links.new(ntex.outputs["Color"], nmap.inputs["Color"])
        nt.links.new(nmap.outputs["Normal"], bsdf.inputs["Normal"])
    if spec.get("orm"):
        otex = nt.nodes.new("ShaderNodeTexImage")
        otex.image = load_img(spec["orm"], noncolor=True)
        sep = nt.nodes.new("ShaderNodeSeparateColor")
        nt.links.new(otex.outputs["Color"], sep.inputs["Color"])
        if spec.get("orm_style") == "gltf":
            nt.links.new(sep.outputs[2], bsdf.inputs["Metallic"])
            nt.links.new(sep.outputs[1], bsdf.inputs["Roughness"])
        else:
            mmult = nt.nodes.new("ShaderNodeMath")
            mmult.operation = 'MULTIPLY'
            mmult.inputs[1].default_value = spec.get("metallic_mult", 1.0)
            nt.links.new(sep.outputs[0], mmult.inputs[0])
            nt.links.new(mmult.outputs[0], bsdf.inputs["Metallic"])
            inv = nt.nodes.new("ShaderNodeInvert")
            inv.inputs[0].default_value = 1.0
            nt.links.new(otex.outputs["Alpha"], inv.inputs[1])
            nt.links.new(inv.outputs[0], bsdf.inputs["Roughness"])
print("MATERIALS DONE")

# ------------------------------------------------- view setup (lights) ----
# 3/4 aerial from the south-east: full perimeter in frame.
bpy.ops.object.light_add(type='SUN', location=(25.0, -35.0, 45.0))
sun = bpy.context.active_object
sun.name = "ViewSun"
sun.data.energy = 3.0
bpy.ops.object.light_add(type='SUN', location=(-20.0, 30.0, 30.0))
fill = bpy.context.active_object
fill.name = "ViewFill"
fill.data.energy = 0.7
w = bpy.data.worlds.new("ViewWorld")
w.use_nodes = True
w.node_tree.nodes["Background"].inputs["Color"].default_value = (0.55, 0.65, 0.80, 1)
w.node_tree.nodes["Background"].inputs["Strength"].default_value = 1.0
bpy.context.scene.world = w
bpy.ops.object.camera_add(location=(30.0, -50.0, 22.0))
cam = bpy.context.active_object
cam.name = "ViewCamera"
aim = Vector((0.0, 0.0, 4.0)) - Vector(cam.location)
cam.rotation_euler = aim.to_track_quat('-Z', 'Y').to_euler()
bpy.context.scene.camera = cam
print("  view setup: 2 suns + world + top-down camera built")

bpy.ops.wm.save_as_mainfile(filepath=OUT_BLEND)

# ---- store texture paths relative to the .blend (portable across checkouts)
for im in bpy.data.images:
    if im.filepath and os.path.isabs(im.filepath):
        im.filepath = bpy.path.relpath(im.filepath)
bpy.ops.wm.save_as_mainfile(filepath=OUT_BLEND)
print("BUILD_DONE", OUT_BLEND)
