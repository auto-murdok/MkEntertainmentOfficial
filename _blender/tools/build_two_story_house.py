"""Build a two-story house from the MkEntertainmentOfficial building kit.

Layout (kit grid, meters, house centered at origin, front = -Y/south):
  Footprint 14.22 x 12.25 (outer faces x +-7.11, y +-6.125).
  Ground floor surface z=2.0 (top of brick platform), floor-to-floor 5.5,
  upper floor z=7.5, wall top / eaves z=13.0, gable roof ridge z=16.28.
  Ground: living SW, foyer S-center, kitchen SE, dining NE, guest WC in dining NE.
  Stair (Internal_Stairs01) against north wall: entry from foyer, 3 flights,
  exit at west end up top onto a plank landing.
  Upper: hall strip y=-2.55..-0.99, bed2/bed3/study south strip, main bath SE
  (ceramic, over kitchen), primary suite east block (bedroom S + ensuite N).
Materials: real kit materials from matlib_building_kit.json (polish recipe).
Texture paths are stored relative to the .blend so it opens in any checkout.

Usage:
  blender --background --python _blender/tools/build_two_story_house.py
Output: _blender/tests/two_story_house.blend
"""
import bpy, bmesh, os, re, json, math, statistics
from mathutils import Vector, Matrix

TOOLS_DIR = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(TOOLS_DIR, "..", ".."))
MESHDIR = os.path.join(REPO, "Assets/ImportedContent/Building_kit/Meshes")
MATLIB = os.path.join(REPO, "_blender/polish/matlib_building_kit.json")
OUT_BLEND = os.path.join(REPO, "_blender/tests/two_story_house.blend")
matmap = json.load(open(MATLIB))
BIG = 30

bpy.ops.wm.read_factory_settings(use_empty=True)
USED = []

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
    USED.append(fbx)
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
    """Cut objects by a plane (world coords, baked geometry); remove the side
    the normal points to, cap the boundary loop(s) where possible."""
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

def centered_target(objs, theta_deg, run_axis, thick_axis, line, start, z):
    mn, mx = combined_bbox(objs)
    anchor = mn
    R = Matrix.Rotation(math.radians(theta_deg), 4, 'Z')
    corners = [Vector((x, y, zz)) for x in (mn.x, mx.x) for y in (mn.y, mx.y) for zz in (mn.z, mx.z)]
    rot = [R @ (c - anchor) for c in corners]
    rmin = Vector((min(c[i] for c in rot) for i in range(3)))
    rmax = Vector((max(c[i] for c in rot) for i in range(3)))
    tgt = [0.0, 0.0, z]
    tgt[run_axis] = start
    tgt[thick_axis] = line - (rmax[thick_axis] - rmin[thick_axis]) / 2
    return tgt

# ---------------------------------------------------------------- layout ----
WX, WY = 7.11, 6.125
Z0, Z1 = 0.0, 7.5

def ext_corner(fbx, quadrant, z):
    objs = import_piece(fbx)
    mn, mx = combined_bbox(objs)

    def mat_centroid(substr):
        cen = Vector((0, 0, 0)); n = 0
        for o in objs:
            mw = o.matrix_world
            for p in o.data.polygons:
                if p.material_index < len(o.data.materials) and o.data.materials[p.material_index] \
                        and substr in o.data.materials[p.material_index].name:
                    cen += mw @ p.center; n += 1
        return (cen / n) if n else None

    cen = mat_centroid("Concrete_Wall_Details")
    if cen is not None:
        # Corner01: concrete quoins sit at the outer vertex itself.
        vx = mn.x if abs(cen.x - mn.x) < abs(cen.x - mx.x) else mx.x
        vy = mn.y if abs(cen.y - mn.y) < abs(cen.y - mx.y) else mx.y
    else:
        # Corner02 has no concrete: its wainscot panel cluster sits at the
        # INNER (concave) vertex, so the outer vertex is the opposite corner.
        cen = mat_centroid("Wainscoting")
        ix = mn.x if abs(cen.x - mn.x) < abs(cen.x - mx.x) else mx.x
        iy = mn.y if abs(cen.y - mn.y) < abs(cen.y - mx.y) else mx.y
        vx = mx.x if ix == mn.x else mn.x
        vy = mx.y if iy == mn.y else mn.y
    vc = Vector((vx, vy, mn.z))
    want = {'SW': (0, 0), 'SE': (1, 0), 'NE': (1, 1), 'NW': (0, 1)}[quadrant]
    corner_pt = Vector((WX if want[0] else -WX, WY if want[1] else -WY))
    for theta in (0, 90, 180, 270):
        R = Matrix.Rotation(math.radians(theta), 4, 'Z')
        corners = [Vector((x, y, zz)) for x in (mn.x, mx.x) for y in (mn.y, mx.y) for zz in (mn.z, mx.z)]
        rot = [R @ (c - mn) for c in corners]
        rmin = Vector((min(c[i] for c in rot) for i in range(3)))
        rmax = Vector((max(c[i] for c in rot) for i in range(3)))
        voff = R @ (vc - mn) - rmin
        vx = 1 if abs(voff.x - (rmax.x - rmin.x)) < 0.05 else (0 if abs(voff.x) < 0.05 else -1)
        vy = 1 if abs(voff.y - (rmax.y - rmin.y)) < 0.05 else (0 if abs(voff.y) < 0.05 else -1)
        if (vx, vy) == want:
            ext = rmax - rmin
            tgt = Vector((corner_pt.x - (ext.x if want[0] else 0.0),
                          corner_pt.y - (ext.y if want[1] else 0.0), z))
            bb = place(objs, theta, tgt)
            bake(objs)
            print(f"corner {fbx} {quadrant} theta={theta} aabb {tuple(round(v,2) for v in bb[0])}..{tuple(round(v,2) for v in bb[1])}")
            return bb
    raise RuntimeError("no rotation fits corner " + quadrant)

def ext_wall_run(fbx_list, theta, fixed_axis, fixed_val, fixed_is_max, cursor, z, label):
    for fbx in fbx_list:
        objs = import_piece(fbx)
        mn, mx = combined_bbox(objs)
        R = Matrix.Rotation(math.radians(theta), 4, 'Z')
        corners = [Vector((x, y, zz)) for x in (mn.x, mx.x) for y in (mn.y, mx.y) for zz in (mn.z, mx.z)]
        rot = [R @ (c - mn) for c in corners]
        rmin = Vector((min(c[i] for c in rot) for i in range(3)))
        rmax = Vector((max(c[i] for c in rot) for i in range(3)))
        run_axis = 1 - fixed_axis
        tgt = [0.0, 0.0, z]
        tgt[run_axis] = cursor
        tgt[fixed_axis] = (fixed_val - (rmax[fixed_axis] - rmin[fixed_axis])) if fixed_is_max else fixed_val
        bb = place(objs, theta, tgt)
        bake(objs)
        cursor = bb[1][run_axis]
        print(f"  {label} {fbx} -> cursor {cursor:.3f}")
    return cursor

# platforms
print("== platforms")
for fbx, tx, ty in [("SM_Wood_External_Floor01.fbx", -7.4, -6.42),
                    ("SM_Wood_External_Floor03.fbx", 0.0, -6.42),
                    ("SM_Wood_External_Floor03.fbx", -7.4, 0.0),
                    ("SM_Wood_External_Floor01.fbx", 0.0, 0.0)]:
    objs = import_piece(fbx)
    bb = place(objs, 0, (tx, ty, 0.0))
    bake(objs)
    print(f"  {fbx} top z={bb[1].z:.2f}")

# exterior entry stairs (auto-detect ascent direction)
print("== external stairs")
objs = import_piece("SM_External_Stairs01.fbx")
pts = []
for o in objs:
    mw = o.matrix_world
    for p in o.data.polygons:
        ws = [mw @ o.data.vertices[vi].co for vi in p.vertices]
        zs = [w.z for w in ws]
        if max(zs) - min(zs) < 0.03 and sum(zs) / len(zs) > 0.15:
            pts.append((sum(w.x for w in ws) / len(ws), sum(w.y for w in ws) / len(ws), sum(zs) / len(zs)))
theta = 0
if len(pts) > 4:
    xs = [p[0] for p in pts]; ys = [p[1] for p in pts]; zs = [p[2] for p in pts]
    def slope(a, b):
        ma, mb = statistics.mean(a), statistics.mean(b)
        return sum((x - ma) * (y - mb) for x, y in zip(a, b)) / max(1e-6, sum((x - ma) ** 2 for x in a))
    sy = slope(ys, zs)
    print(f"  stair tread slope dz/dy={sy:.2f}")
    theta = 0 if sy > 0 else 180
bb = place(objs, theta, (-3.92, -11.85, 0.0))
bake(objs)
print(f"  ext stairs theta={theta} aabb {tuple(round(v,2) for v in bb[0])}..{tuple(round(v,2) for v in bb[1])}")

# exterior walls
print("== exterior walls ground")
ext_corner("SM_ExternalWall_Baseflor_Corner01.fbx", 'SW', Z0)
ext_corner("SM_ExternalWall_Baseflor_Corner02.fbx", 'SE', Z0)
ext_corner("SM_ExternalWall_Baseflor_Corner01.fbx", 'NE', Z0)
ext_corner("SM_ExternalWall_Baseflor_Corner02.fbx", 'NW', Z0)
end = ext_wall_run(["SM_ExternalWall_Baseflor_WindowFrame01.fbx",
                    "SM_ExternalWall_Baseflor_DoorFrame01.fbx",
                    "SM_ExternalWall_Baseflor_WindowFrame01.fbx"],
                   0, 1, -WY, False, -6.07, Z0, "S-ground")
print(f"  S-ground ends {end:.3f} (SE corner starts ~5.93)")
end = ext_wall_run(["SM_ExternalWall_Baseflor_wall01.fbx",
                    "SM_ExternalWall_Baseflor_WindowFrame01.fbx",
                    "SM_ExternalWall_Baseflor_WindowFrame02.fbx"],
                   180, 1, WY, True, -6.07, Z0, "N-ground")
print(f"  N-ground ends {end:.3f}")
end = ext_wall_run(["SM_ExternalWall_Baseflor_WindowFrame02.fbx",
                    "SM_ExternalWall_Baseflor_WindowFrame01.fbx"],
                   -90, 0, -WX, False, -5.10, Z0, "W-ground")
print(f"  W-ground ends {end:.3f} (NW corner starts ~4.91)")
end = ext_wall_run(["SM_ExternalWall_Baseflor_WindowFrame01.fbx",
                    "SM_ExternalWall_Baseflor_DoorFrame01.fbx",
                    "SM_ExternalWall_Baseflor_wall01.fbx"],
                   90, 0, WX, True, -5.10, Z0, "E-ground")
print(f"  E-ground ends {end:.3f}")

print("== exterior walls upper")
ext_corner("SM_ExternalWall_SecondFloor_Corner01.fbx", 'SW', Z1)
ext_corner("SM_ExternalWall_SecondFloor_Corner02.fbx", 'SE', Z1)
ext_corner("SM_ExternalWall_SecondFloor_Corner01.fbx", 'NE', Z1)
ext_corner("SM_ExternalWall_SecondFloor_Corner02.fbx", 'NW', Z1)
ext_wall_run(["SM_ExternalWall_SecondFloor_WindowFrame02.fbx",
              "SM_ExternalWall_SecondFloor_WindowFrame01.fbx",
              "SM_ExternalWall_SecondFloor_wall01.fbx"],
             0, 1, -WY, False, -6.07, Z1, "S-upper")
ext_wall_run(["SM_ExternalWall_SecondFloor_WindowFrame01.fbx",
              "SM_ExternalWall_SecondFloor_WindowFrame02.fbx",
              "SM_ExternalWall_SecondFloor_wall01.fbx"],
             180, 1, WY, True, -6.07, Z1, "N-upper")
ext_wall_run(["SM_ExternalWall_SecondFloor_WindowFrame01.fbx",
              "SM_ExternalWall_SecondFloor_WindowFrame02.fbx"],
             -90, 0, -WX, False, -5.10, Z1, "W-upper")
ext_wall_run(["SM_ExternalWall_SecondFloor_WindowFrame02.fbx",
              "SM_ExternalWall_SecondFloor_WindowFrame01.fbx"],
             90, 0, WX, True, -5.10, Z1, "E-upper")

# string-course trim between floors
print("== trim")
for theta, ty in ((0, -WY - 0.20), (180, WY - 0.003)):
    for k in range(4):
        objs = import_piece("SM_Trim01.fbx")
        place(objs, theta, (-6.45 + 3.227 * k, ty, 7.24))
        bake(objs)
for theta, tx, ty in ((0, -7.30, -WY - 0.20), (0, 6.82, -WY - 0.20),
                      (180, -7.30, WY - 0.003), (180, 6.82, WY - 0.003)):
    objs = import_piece("SM_Trim01_Corner.fbx")
    place(objs, theta, (tx, ty, 7.24))
    bake(objs)

# ------------------------------------------------------------- interior ----
def int_wall(fbx, theta, run_axis, thick_axis, line, start, z):
    objs = import_piece(fbx)
    tgt = centered_target(objs, theta, run_axis, thick_axis, line, start, z)
    bb = place(objs, theta, tgt)
    bake(objs)
    return bb

print("== interior walls ground (z=2.0)")
int_wall("SM_Internal_Wall_DoorFrame01.fbx", 90, 1, 0, -1.9, -5.95, 2.0)
int_wall("SM_Internal_Wall_DoorFrame01.fbx", 90, 1, 0, 1.9, -5.95, 2.0)
# guest WC in dining NE corner: box x 4.4..6.65, y 3.4..5.66 (door gap y 3.4..4.16 on west side)
int_wall("SM_Internal_Wall_wall03.fbx", 90, 1, 0, 4.4, 4.16, 2.0)
int_wall("SM_Internal_Wall_wall03.fbx", 0, 0, 1, 3.4, 4.4, 2.0)
int_wall("SM_Internal_Wall_wall02.fbx", 0, 0, 1, 3.4, 5.65, 2.0)
objs = import_piece("SM_Internal_Wall_corner01.fbx")
place(objs, 0, (3.85, 2.85, 2.0)); bake(objs)

print("== interior walls upper (z=7.5)")
int_wall("SM_Internal_Wall_DoorFrame01.fbx", 0, 0, 1, -2.55, -6.65, 7.5)
int_wall("SM_Internal_Wall_DoorFrame01.fbx", 0, 0, 1, -2.55, -1.65, 7.5)
int_wall("SM_Internal_Wall_wall03.fbx", 0, 0, 1, -2.55, 3.35, 7.5)
int_wall("SM_Internal_Wall_wall02.fbx", 0, 0, 1, -2.55, 5.85, 7.5)   # bath door gap x 4.85..5.85
for xline in (-3.3, 1.2, 4.0):
    int_wall("SM_Internal_Wall_wall02.fbx", 90, 1, 0, xline, -5.66, 7.5)
    int_wall("SM_Internal_Wall_wall03.fbx", 90, 1, 0, xline, -3.85, 7.5)
# No corner post at the bath/hall junction: the chunky corner01 (1.19 deep)
# protruded into the hall and pinched it to 0.36 m (measurement audit
# 2026-10-02). The spine + hall wall form a flush T-junction instead.
int_wall("SM_Internal_Wall_wall03.fbx", 0, 0, 1, -0.99, 2.51, 7.5)  # suite S wall + door gap 4.01..5.15
int_wall("SM_Internal_Wall_wall03.fbx", 0, 0, 1, -0.99, 5.15, 7.5)
int_wall("SM_Internal_Wall_wall02.fbx", 0, 0, 1, 3.26, 3.65, 7.5)   # ensuite divider (passage 2.51..3.65)

# Measurement-fix pass: no interior wall may leave the building. Run-fill
# overshoot pushed two wall02 end pieces through the east facade (audit
# 2026-10-02: protrusions to x 8.66 / 8.86). Clip any internal-wall object
# that crosses the facade box back to the interior clear face.
for o in list(bpy.data.objects):
    if o.type != 'MESH' or 'Internal_Wall' not in o.name:
        continue
    mn, mx = combined_bbox([o])
    if mx.x > 7.11:
        bisect_cut([o], (6.65, 0, 0), (1, 0, 0))
    if mn.x < -7.11:
        bisect_cut([o], (-6.65, 0, 0), (-1, 0, 0))
    if mx.y > 6.13:
        bisect_cut([o], (0, 5.66, 0), (0, 1, 0))
    if mn.y < -6.13:
        bisect_cut([o], (0, -5.66, 0), (0, -1, 0))

# ---------------------------------------------------------------- stair ----
print("== internal staircase")
objs = import_piece("SM_Internal_Stairs01.fbx")
bb = place(objs, 0, (-6.55, -0.99, 2.0))
bake(objs)
print(f"  stairs aabb {tuple(round(v,2) for v in bb[0])}..{tuple(round(v,2) for v in bb[1])}")

# ------------------------------------------------------- upper floor slabs ----
print("== upper slabs (top z=7.5)")
def slab(fbx, tx, ty, cuts):
    objs = import_piece(fbx)
    mn, mx = combined_bbox(objs)
    z = 7.5 - (mx.z - mn.z)
    place(objs, 0, (tx, ty, z))
    bake(objs)
    for co, no in cuts:
        bisect_cut(objs, co, no)
    bb = combined_bbox(objs)
    print(f"  {fbx} -> x {bb[0].x:.2f}..{bb[1].x:.2f} y {bb[0].y:.2f}..{bb[1].y:.2f} z {bb[0].z:.2f}..{bb[1].z:.2f}")
    return objs

slab("SM_Wood_Internal_Floor01.fbx", -6.65, -5.66,
     [((0, -0.99, 0), (0, 1, 0))])
slab("SM_Wood_Internal_Floor02.fbx", 0.06, -5.66,
     [((0, -0.99, 0), (0, 1, 0)), ((4.0, 0, 0), (1, 0, 0))])
slab("SM_Ceramic_Internal_Floor01.fbx", -0.025, -5.66,
     [((0, -0.99, 0), (0, 1, 0)), ((4.0, 0, 0), (-1, 0, 0))])
slab("SM_Wood_Internal_Floor03.fbx", 2.51, -0.99,
     [((6.65, 0, 0), (1, 0, 0)), ((0, 3.26, 0), (0, 1, 0))])
slab("SM_Ceramic_Internal_Floor02.fbx", 2.51, 3.26,
     [((6.65, 0, 0), (1, 0, 0)), ((0, 5.66, 0), (0, 1, 0))])

print("== planks")
for k in range(3):
    objs = import_piece("SM_FloorPlank01.fbx")
    place(objs, 0, (-6.50, -0.96 + 0.244 * k, 7.421)); bake(objs)
for k in range(2):
    objs = import_piece("SM_FloorPlank02.fbx")
    place(objs, 0, (-2.28, -3.45 + 0.244 * k, 2.04)); bake(objs)

# ------------------------------------------------------------------ roof ----
print("== roof")
roof_south = []
roof_plain = []
for k, fbx in enumerate(["SM_Roof01.fbx", "SM_Roof01.fbx", "SM_Roof03.fbx", "SM_Roof01.fbx"]):
    objs = import_piece(fbx)
    place(objs, 0, (-8.0 + 4.0 * k, -7.085, 12.06))
    bake(objs)
    bisect_cut(objs, (0, 0, 0), (0, 1, 0))
    roof_south += objs
    if k == 0:
        roof_plain = objs
    print(f"  S module {k} {fbx}")
for k in range(4):
    objs = import_piece("SM_Roof01.fbx")
    mn, mx = combined_bbox(objs)
    ty = 6.575 - mx.y          # rotated: tile eave (local y=0) lands at house y +6.575
    place(objs, 180, (-8.0 + 4.0 * k, ty, 12.06))
    bake(objs)
    bisect_cut(objs, (0, 0, 0), (0, -1, 0))
    print(f"  N module {k}")

# ------------------------------------------------------------- gable ends ----
print("== gables")
prof = {}
for o in roof_plain:
    for p in o.data.polygons:
        if p.material_index < len(o.data.materials) and o.data.materials[p.material_index] \
                and o.data.materials[p.material_index].name.split('.')[0] == "MI_Roof":
            for vi in p.vertices:
                w = o.data.vertices[vi].co
                b = round(w.y * 4) / 4
                if w.y <= 0.05:
                    prof[b] = max(prof.get(b, -99), w.z)
pts = sorted((y, z) for y, z in prof.items() if z > 0)
print("  profile pts:", [(round(y, 2), round(z, 2)) for y, z in pts])
ridge_z = pts[-1][1] if pts else 16.9
poly = [(-7.35, 12.97)] + [(y, z - 0.05) for y, z in pts if y > -7.3] + \
       [(-y, z - 0.05) for y, z in reversed(pts) if y < -0.01] + [(7.35, 12.97)]
brick = None
for m in bpy.data.materials:
    if m.name.split('.')[0] == "MI_Bricks_External_Wall":
        brick = m; break
for xsign in (1, -1):
    bm = bmesh.new()
    vs = []
    for (yy, zz) in poly:
        vs.append((bm.verts.new((0.0, yy, zz)), bm.verts.new((0.24, yy, zz))))
    n = len(vs)
    for i in range(n - 1):
        a0, a1 = vs[i]; b0, b1 = vs[i + 1]
        bm.faces.new((a0, b0, b1, a1))
    bm.faces.new([v[0] for v in reversed(vs)])
    bm.faces.new([v[1] for v in vs])
    me = bpy.data.meshes.new("Gable")
    bm.to_mesh(me); bm.free()
    ob = bpy.data.objects.new("Gable", me)
    bpy.context.scene.collection.objects.link(ob)
    ob.location.x = (6.87 if xsign > 0 else -7.11)
    if brick:
        me.materials.append(brick)
    # simple planar UVs from (y,z)
    uv = me.uv_layers.new(name="UVMap")
    for loop in me.loops:
        v = me.vertices[loop.vertex_index]
        uv.data[loop.index].uv = ((v.co.y + 7.35) * 0.22, (v.co.z - 12.9) * 0.22)
    bpy.context.view_layer.update()
    bake([ob])
    print(f"  gable {'E' if xsign > 0 else 'W'} built, ridge z={ridge_z:.2f}")

# ---------------------------------------------------------------- ground ----
bpy.ops.mesh.primitive_plane_add(size=90, location=(0, 0, -0.03))
gp = bpy.context.active_object
gm = bpy.data.materials.new("GroundMat")
gm.use_nodes = True
gm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.16, 0.18, 0.13, 1)
gm.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = 1.0
gp.data.materials.append(gm)

# -------------------------------------------------------------- materials ----
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
        # texture-less spec (roof glass): synthesize a simple glass
        bsdf.inputs["Base Color"].default_value = (0.72, 0.82, 0.88, 1)
        bsdf.inputs["Roughness"].default_value = 0.06
        bsdf.inputs["Alpha"].default_value = 0.35
        if "Transmission Weight" in bsdf.inputs:
            bsdf.inputs["Transmission Weight"].default_value = 0.5
        print("MATERIAL OK (glass synth):", mat.name)
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
    print("MATERIAL OK:", mat.name)

bpy.ops.wm.save_as_mainfile(filepath=OUT_BLEND)

# ---- store texture paths relative to the .blend (portable across checkouts)
for im in bpy.data.images:
    if im.filepath and os.path.isabs(im.filepath):
        im.filepath = bpy.path.relpath(im.filepath)
bpy.ops.wm.save_as_mainfile(filepath=OUT_BLEND)
from collections import Counter
print("USED PIECES:", dict(Counter(USED)))
print("TOTAL IMPORTS:", len(USED))
print("BUILD_DONE", OUT_BLEND)
