"""Build a two-story house from the MkEntertainmentOfficial building kit.

Layout (kit grid, meters, house centered at origin, front = -Y/south):
  Footprint 22.22 x 12.25 (outer faces x -7.11..+15.11, y +-6.125) after the
  +8 m east-wing expansion (the roof modules fix the 12.25 depth; the house
  grows along its length only). Original west block: 14.22 x 12.25.
  Ground floor surface z=2.0 (top of brick platform), floor-to-floor 5.5,
  upper floor z=7.5, wall top / eaves z=13.0, gable roof ridge z=16.28.
  Ground: living SW, foyer S-center, kitchen S-center-E, dining NE of the west
  block; east wing: family room S, laundry + guest wing N (sitting, bedroom,
  ensuite). Stair (Internal_Stairs01) against north wall: entry from foyer,
  3 flights, exit at west end up top onto a plank landing.
  Upper: hall strip y=-2.55..-0.99 running the full length, bed2/bed3/study
  south strip, main bath SE of the west block (ceramic, over kitchen), suite
  (bedroom S + ensuite N) mid-east, primary suite in the east wing (bedroom
  + walk-in closet S, dressing + ensuite + sitting N).
  Two-story canted bay (WindowFrame03, both floors) on the south facade
  serving the kitchen/family room below and the main bath/primary bedroom
  above; the bay carries its own trapezoid floors and a flat roof lid.
  A SecondFloor DoorFrame01 in the east wall opens from the primary bedroom
  onto a small timber balcony (kit floor deck on posts, prism rails).
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
WXE = 15.11          # east outer face after the +8 m wing (west stays -WX)
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
    corner_pt = Vector((WXE if want[0] else -WX, WY if want[1] else -WY))
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
                    ("SM_Wood_External_Floor01.fbx", 0.0, 0.0),
                    ("SM_Wood_External_Floor01.fbx", 7.4, -6.42),
                    ("SM_Wood_External_Floor03.fbx", 7.4, 0.0)]:
    objs = import_piece(fbx)
    bb = place(objs, 0, (tx, ty, 0.0))
    bake(objs)
    print(f"  {fbx} top z={bb[1].z:.2f}")
# east edge column, cut to the terrace edge (east wall face 15.11 + 0.30)
for fbx, tx, ty in [("SM_Wood_External_Floor03.fbx", 14.8, -6.42),
                    ("SM_Wood_External_Floor01.fbx", 14.8, 0.0)]:
    objs = import_piece(fbx)
    place(objs, 0, (tx, ty, 0.0))
    bake(objs)
    bisect_cut(objs, (15.41, 0, 0), (1, 0, 0))
    print(f"  {fbx} (cut) top z={combined_bbox(objs)[1].z:.2f}")

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
                    "SM_ExternalWall_Baseflor_wall01.fbx"],
                   0, 1, -WY, False, -6.07, Z0, "S-ground")
print(f"  S-ground ends {end:.3f} - bay takes over to the SE corner")
bay_x = end
objs = import_piece("SM_ExternalWall_Baseflor_WindowFrame03.fbx")
place(objs, 0, (bay_x, -7.97, Z0))
bake(objs)
print(f"  south bay (ground) x {bay_x:.2f}..{bay_x + 10:.2f}, front y -7.97")
end = ext_wall_run(["SM_ExternalWall_Baseflor_wall01.fbx",
                    "SM_ExternalWall_Baseflor_WindowFrame01.fbx",
                    "SM_ExternalWall_Baseflor_WindowFrame02.fbx",
                    "SM_ExternalWall_Baseflor_WindowFrame01.fbx",
                    "SM_ExternalWall_Baseflor_WindowFrame01.fbx"],
                   180, 1, WY, True, -6.07, Z0, "N-ground")
print(f"  N-ground ends {end:.3f}")
end = ext_wall_run(["SM_ExternalWall_Baseflor_WindowFrame02.fbx",
                    "SM_ExternalWall_Baseflor_WindowFrame01.fbx"],
                   -90, 0, -WX, False, -5.10, Z0, "W-ground")
print(f"  W-ground ends {end:.3f} (NW corner starts ~4.91)")
end = ext_wall_run(["SM_ExternalWall_Baseflor_WindowFrame01.fbx",
                    "SM_ExternalWall_Baseflor_DoorFrame01.fbx",
                    "SM_ExternalWall_Baseflor_wall01.fbx"],
                   90, 0, WXE, True, -5.10, Z0, "E-ground")
print(f"  E-ground ends {end:.3f}")

print("== exterior walls upper")
ext_corner("SM_ExternalWall_SecondFloor_Corner01.fbx", 'SW', Z1)
ext_corner("SM_ExternalWall_SecondFloor_Corner02.fbx", 'SE', Z1)
ext_corner("SM_ExternalWall_SecondFloor_Corner01.fbx", 'NE', Z1)
ext_corner("SM_ExternalWall_SecondFloor_Corner02.fbx", 'NW', Z1)
end = ext_wall_run(["SM_ExternalWall_SecondFloor_WindowFrame02.fbx",
                    "SM_ExternalWall_SecondFloor_WindowFrame01.fbx"],
                   0, 1, -WY, False, -6.07, Z1, "S-upper")
bay_xu = end
objs = import_piece("SM_ExternalWall_SecondFloor_WindowFrame03.fbx")
place(objs, 0, (bay_xu, -7.97, 7.25))   # piece has a 0.25 below-floor tail
bake(objs)
print(f"  south bay (upper) x {bay_xu:.2f}..{bay_xu + 10:.2f}")
ext_wall_run(["SM_ExternalWall_SecondFloor_WindowFrame01.fbx",
              "SM_ExternalWall_SecondFloor_WindowFrame02.fbx",
              "SM_ExternalWall_SecondFloor_wall01.fbx",
              "SM_ExternalWall_SecondFloor_WindowFrame01.fbx",
              "SM_ExternalWall_SecondFloor_WindowFrame01.fbx"],
             180, 1, WY, True, -6.07, Z1, "N-upper")
ext_wall_run(["SM_ExternalWall_SecondFloor_WindowFrame01.fbx",
              "SM_ExternalWall_SecondFloor_WindowFrame02.fbx"],
             -90, 0, -WX, False, -5.10, Z1, "W-upper")
ext_wall_run(["SM_ExternalWall_SecondFloor_DoorFrame01.fbx",
              "SM_ExternalWall_SecondFloor_wall01.fbx",
              "SM_ExternalWall_SecondFloor_WindowFrame01.fbx"],
             90, 0, WXE, True, -5.10, Z1, "E-upper")

# string-course trim between floors
print("== trim")
for theta, ty, krange in ((0, -WY - 0.20, range(2)), (180, WY - 0.003, range(6))):
    for k in krange:
        objs = import_piece("SM_Trim01.fbx")
        place(objs, theta, (-6.45 + 3.227 * k, ty, 7.24))
        bake(objs)
for theta, tx, ty in ((0, -7.30, -WY - 0.20), (0, 14.82, -WY - 0.20),
                      (180, -7.30, WY - 0.003), (180, 14.82, WY - 0.003)):
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

print("== interior walls ground, east wing (z=2.0)")
# old east wall line x=6.65 becomes interior: arch S (kitchen->family),
# wall + arch N (dining->guest sitting)
int_wall("SM_Internal_Wall_DoorFrame01.fbx", 90, 1, 0, 6.65, -5.95, 2.0)
int_wall("SM_Internal_Wall_wall02.fbx", 90, 1, 0, 6.65, -0.99, 2.0)
int_wall("SM_Internal_Wall_DoorFrame01.fbx", 90, 1, 0, 6.65, 2.02, 2.0)
# family room north wall (laundry door gap x 8.15..9.05)
int_wall("SM_Internal_Wall_wall03.fbx", 0, 0, 1, -0.99, 6.65, 2.0)
int_wall("SM_Internal_Wall_wall02.fbx", 0, 0, 1, -0.99, 9.05, 2.0)
int_wall("SM_Internal_Wall_wall03.fbx", 0, 0, 1, -0.99, 12.06, 2.0)
int_wall("SM_Internal_Wall_wall03.fbx", 0, 0, 1, -0.99, 13.56, 2.0)
# guest wing spine x=10.65 (sitting->bedroom door gap y 3.52..4.42)
int_wall("SM_Internal_Wall_wall02.fbx", 90, 1, 0, 10.65, -0.99, 2.0)
int_wall("SM_Internal_Wall_wall03.fbx", 90, 1, 0, 10.65, 2.02, 2.0)
int_wall("SM_Internal_Wall_wall02.fbx", 90, 1, 0, 10.65, 4.42, 2.0)
# guest wing divider y=2.06 (bedroom->ensuite door gap x 12.67..13.57)
int_wall("SM_Internal_Wall_wall02.fbx", 0, 0, 1, 2.06, 6.65, 2.0)
int_wall("SM_Internal_Wall_wall02.fbx", 0, 0, 1, 2.06, 9.66, 2.0)
int_wall("SM_Internal_Wall_wall03.fbx", 0, 0, 1, 2.06, 13.57, 2.0)

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

print("== interior walls upper, east wing (z=7.5)")
# cross wall x=6.65 in the south strip (main bath | primary bedroom, solid)
int_wall("SM_Internal_Wall_wall02.fbx", 90, 1, 0, 6.65, -5.66, 7.5)
# cross wall x=6.65 in the north block (suite ensuite | dressing, solid)
int_wall("SM_Internal_Wall_wall02.fbx", 90, 1, 0, 6.65, -0.99, 7.5)
int_wall("SM_Internal_Wall_wall03.fbx", 90, 1, 0, 6.65, 1.77, 7.5)
int_wall("SM_Internal_Wall_wall02.fbx", 90, 1, 0, 6.65, 3.02, 7.5)
# hall wall extension (primary bedroom door gap x 8.15..9.15)
int_wall("SM_Internal_Wall_wall03.fbx", 0, 0, 1, -2.55, 6.65, 7.5)
int_wall("SM_Internal_Wall_wall02.fbx", 0, 0, 1, -2.55, 9.15, 7.5)
int_wall("SM_Internal_Wall_wall03.fbx", 0, 0, 1, -2.55, 12.16, 7.5)
int_wall("SM_Internal_Wall_wall03.fbx", 0, 0, 1, -2.55, 13.66, 7.5)
# suite wall extension (dressing door gap x 8.15..9.15)
int_wall("SM_Internal_Wall_wall03.fbx", 0, 0, 1, -0.99, 6.65, 7.5)
int_wall("SM_Internal_Wall_wall02.fbx", 0, 0, 1, -0.99, 9.15, 7.5)
int_wall("SM_Internal_Wall_wall03.fbx", 0, 0, 1, -0.99, 12.16, 7.5)
int_wall("SM_Internal_Wall_wall03.fbx", 0, 0, 1, -0.99, 13.66, 7.5)
# wing spine x=10.65 (dressing | ensuite/sitting; door gaps y 0.51..1.41, 4.42..5.32)
int_wall("SM_Internal_Wall_wall03.fbx", 90, 1, 0, 10.65, -0.99, 7.5)
int_wall("SM_Internal_Wall_wall02.fbx", 90, 1, 0, 10.65, 1.41, 7.5)
int_wall("SM_Internal_Wall_wall03.fbx", 90, 1, 0, 10.65, 5.32, 7.5)
# ensuite | sitting divider y=2.06 (solid; both rooms open off the dressing)
int_wall("SM_Internal_Wall_wall02.fbx", 0, 0, 1, 2.06, 10.65, 7.5)
int_wall("SM_Internal_Wall_wall03.fbx", 0, 0, 1, 2.06, 13.41, 7.5)
# walk-in closet spine x=12.40 (bedroom | WIC; door gap y -4.16..-3.26; the WIC
# absorbs the hall strip's east end, so the spine runs to the suite wall line)
int_wall("SM_Internal_Wall_wall03.fbx", 90, 1, 0, 12.40, -5.66, 7.5)
int_wall("SM_Internal_Wall_wall02.fbx", 90, 1, 0, 12.40, -3.26, 7.5)

# Measurement-fix pass: no interior wall may leave the building. Run-fill
# overshoot pushed two wall02 end pieces through the east facade (audit
# 2026-10-02: protrusions to x 8.66 / 8.86). Clip any internal-wall object
# that crosses the facade box back to the interior clear face.
for o in list(bpy.data.objects):
    if o.type != 'MESH' or 'Internal_Wall' not in o.name:
        continue
    mn, mx = combined_bbox([o])
    if mx.x > WXE:
        bisect_cut([o], (14.65, 0, 0), (1, 0, 0))
    if mn.x < -WX:
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
# east wing slabs: south strip (wood), dressing + sitting (wood), ensuite (ceramic)
slab("SM_Wood_Internal_Floor02.fbx", 6.65, -5.66,
     [((0, -0.99, 0), (0, 1, 0)), ((14.65, 0, 0), (1, 0, 0))])
slab("SM_Wood_Internal_Floor02.fbx", 13.79, -5.66,
     [((0, -0.99, 0), (0, 1, 0)), ((13.79, 0, 0), (-1, 0, 0)), ((14.65, 0, 0), (1, 0, 0))])
slab("SM_Wood_Internal_Floor03.fbx", 6.65, -0.99,
     [((10.65, 0, 0), (1, 0, 0))])
slab("SM_Wood_Internal_Floor03.fbx", 6.65, 3.66,
     [((10.65, 0, 0), (1, 0, 0)), ((0, 5.66, 0), (0, 1, 0))])
slab("SM_Ceramic_Internal_Floor02.fbx", 10.65, -0.99,
     [((14.65, 0, 0), (1, 0, 0)), ((0, 2.06, 0), (0, 1, 0))])
slab("SM_Wood_Internal_Floor03.fbx", 10.65, 2.06,
     [((14.65, 0, 0), (1, 0, 0)), ((0, 5.66, 0), (0, 1, 0))])

# bay floors (ground, Floor02) + bay slabs (upper, Wood02): trapezoid pieces
# cut to the bay outline A(bay_x+0.5,-5.66) B(+3.0,-7.92) C(+7.0,-7.92) D(+9.5,-5.66)
print("== bay floors and slabs")
import math as _math
_fn = _math.hypot(2.5, 2.26)
W_FACET = (((bay_x + 0.5), -5.66, 0), (-2.26 / _fn, -2.5 / _fn, 0))
E_FACET = (((bay_x + 7.0), -7.92, 0), (2.26 / _fn, -2.5 / _fn, 0))
CHORD = ((0, -5.66, 0), (0, 1, 0))
FRONT = ((0, -7.92, 0), (0, -1, 0))

def fix_floor02(objs):
    """Floor02 ships ~1% of faces on 'Fbx Default Material' - reassign to wood."""
    for ob in objs:
        if ob.type != 'MESH':
            continue
        names = [m.name if m else "" for m in ob.data.materials]
        if "MI_Wood_ExteriorFloor" not in names or "Fbx Default Material" not in names:
            continue
        wood = names.index("MI_Wood_ExteriorFloor")
        bad = names.index("Fbx Default Material")
        for p in ob.data.polygons:
            if p.material_index == bad:
                p.material_index = wood

objs = import_piece("SM_Wood_External_Floor02.fbx")
fix_floor02(objs)
place(objs, 0, (bay_x - 0.6, -7.97, Z0)); bake(objs)
for co, no in [CHORD, FRONT, W_FACET, ((bay_x + 5, 0, 0), (1, 0, 0))]:
    bisect_cut(objs, co, no)
objs = import_piece("SM_Wood_External_Floor02.fbx")
fix_floor02(objs)
place(objs, 0, (bay_x + 4.4, -7.97, Z0)); bake(objs)
for co, no in [CHORD, FRONT, E_FACET, ((bay_x + 5, 0, 0), (-1, 0, 0))]:
    bisect_cut(objs, co, no)
slab("SM_Wood_Internal_Floor02.fbx", bay_x - 0.6, -7.97,
     [CHORD, FRONT, W_FACET, ((bay_x + 5, 0, 0), (1, 0, 0))])
slab("SM_Wood_Internal_Floor02.fbx", bay_x + 4.4, -7.97,
     [CHORD, FRONT, E_FACET, ((bay_x + 5, 0, 0), (-1, 0, 0))])

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
for k, fbx in enumerate(["SM_Roof01.fbx", "SM_Roof01.fbx", "SM_Roof03.fbx",
                         "SM_Roof01.fbx", "SM_Roof01.fbx", "SM_Roof01.fbx"]):
    objs = import_piece(fbx)
    place(objs, 0, (-8.0 + 4.0 * k, -7.085, 12.06))
    bake(objs)
    bisect_cut(objs, (0, 0, 0), (0, 1, 0))
    roof_south += objs
    if k == 0:
        roof_plain = objs
    print(f"  S module {k} {fbx}")
for k in range(6):
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
    ob.location.x = (14.87 if xsign > 0 else -7.11)
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

# ------------------------------------------------- corner seam plugs ----
# The corner pieces' arms are shorter than the run pieces assume (Corner01
# reaches 1.04/1.02 from its vertex, Corner02 1.21/1.18), leaving vertical
# slots where three runs meet their NE/NW corners (measured by outside-in
# raycasts, 2026-10-02: N-face slot x 13.95..14.07, E-face slot y 4.91..5.10,
# W-face slot y 4.91..4.94, both floors; the same slots exist in the
# pre-expansion house). Plug them with brick piers flush with the wall faces.
print("== corner seam plugs")
def seam_plug(x0, x1, y0, y1):
    bm = bmesh.new()
    vs = [bm.verts.new((x, y, z)) for x in (x0, x1) for y in (y0, y1) for z in (0.0, 13.0)]
    for a, b, c, d in ((0, 1, 3, 2), (4, 6, 7, 5), (0, 4, 5, 1), (2, 3, 7, 6), (0, 2, 6, 4), (1, 5, 7, 3)):
        bm.faces.new((vs[a], vs[b], vs[c], vs[d]))
    me = bpy.data.meshes.new("SeamPlug")
    bm.to_mesh(me); bm.free()
    ob = bpy.data.objects.new("SeamPlug", me)
    bpy.context.scene.collection.objects.link(ob)
    if brick:
        me.materials.append(brick)
    uv = me.uv_layers.new(name="UVMap")
    for loop in me.loops:
        v = me.vertices[loop.vertex_index]
        uv.data[loop.index].uv = ((v.co.x + v.co.y) * 0.22, v.co.z * 0.22)
    bpy.context.view_layer.update()
    bake([ob])

seam_plug(13.93, 14.09, 5.66, WY)        # NE corner, north face
seam_plug(14.65, WXE, 4.89, 5.12)        # NE corner, east face
seam_plug(-WX, -6.65, 4.89, 4.96)        # NW corner, west face

# ---------------------------------------------------- bay cap + balcony ----
# The bay projects past the main roof's eave line, so it gets a flat
# trapezoid lid just above its wall top (13.04), north edge tucked to the
# eave. The upper-east DoorFrame01 opens onto a small timber balcony.
print("== bay cap + balcony")
roof_mat = None
wood_mat = None
for m in bpy.data.materials:
    if m.name.split('.')[0] == "MI_Roof":
        roof_mat = m
    if m.name.split('.')[0] == "MI_Wood_ExteriorFloor":
        wood_mat = m

def prism(name, outline, z0, z1, mat):
    bm = bmesh.new()
    lo = [bm.verts.new((x, y, z0)) for x, y in outline]
    hi = [bm.verts.new((x, y, z1)) for x, y in outline]
    n = len(outline)
    bm.faces.new(tuple(reversed(lo)))
    bm.faces.new(tuple(hi))
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((lo[i], lo[j], hi[j], hi[i]))
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me); bm.free()
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    if mat:
        me.materials.append(mat)
    uv = me.uv_layers.new(name="UVMap")
    for loop in me.loops:
        v = me.vertices[loop.vertex_index]
        uv.data[loop.index].uv = (v.co.x * 0.22, v.co.y * 0.22)
    bpy.context.view_layer.update()
    bake([ob])
    return ob

prism("BayCap", [(bay_x + 0.3, -5.90), (bay_x + 2.9, -8.02),
                 (bay_x + 7.1, -8.02), (bay_x + 9.7, -5.90)],
      13.02, 13.14, roof_mat)

deck = import_piece("SM_Wood_External_Floor01.fbx")
place(deck, 0, (WXE, -5.30, 5.45)); bake(deck)
bisect_cut(deck, (16.51, 0, 0), (1, 0, 0))
bisect_cut(deck, (0, -0.69, 0), (0, 1, 0))
bisect_cut(deck, (0, 0, 7.13), (0, 0, -1))   # drop the platform skirt, keep the deck slab
# posts + beam under the deck's outer edge, railing uprights + rails
prism("BalconyPost", [(16.27, -5.30), (16.51, -5.30), (16.51, -5.06), (16.27, -5.06)], 2.04, 5.45, wood_mat)
prism("BalconyPost", [(16.27, -0.93), (16.51, -0.93), (16.51, -0.69), (16.27, -0.69)], 2.04, 5.45, wood_mat)
prism("BalconyBeam", [(16.11, -5.30), (16.51, -5.30), (16.51, -0.69), (16.11, -0.69)], 5.13, 5.45, wood_mat)
for ux, uy in ((16.41, -5.25), (16.41, -3.0), (16.41, -0.79)):
    prism("RailPost", [(ux - 0.05, uy - 0.05), (ux + 0.05, uy - 0.05),
                       (ux + 0.05, uy + 0.05), (ux - 0.05, uy + 0.05)], 7.50, 8.58, wood_mat)
prism("RailTop", [(16.39, -5.32), (16.53, -5.32), (16.53, -0.67), (16.39, -0.67)], 8.46, 8.58, wood_mat)
prism("RailMid", [(16.44, -5.32), (16.52, -5.32), (16.52, -0.67), (16.44, -0.67)], 8.02, 8.12, wood_mat)
prism("RailLow", [(16.44, -5.32), (16.52, -5.32), (16.52, -0.67), (16.44, -0.67)], 7.62, 7.72, wood_mat)
for y0, y1 in ((-5.31, -5.19), (-0.85, -0.73)):
    prism("RailTop", [(15.11, y0), (16.45, y0), (16.45, y1), (15.11, y1)], 8.46, 8.58, wood_mat)
for y0, y1 in ((-5.29, -5.21), (-0.83, -0.75)):
    prism("RailMid", [(15.11, y0), (16.45, y0), (16.45, y1), (15.11, y1)], 8.02, 8.12, wood_mat)
    prism("RailLow", [(15.11, y0), (16.45, y0), (16.45, y1), (15.11, y1)], 7.62, 7.72, wood_mat)
print("  bay cap + balcony built")

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
