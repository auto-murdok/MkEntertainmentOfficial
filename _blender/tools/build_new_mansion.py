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

# Starter footprint (m): west-block size, room to grow east later.
FP_W, FP_D = 14.22, 12.25
SLAB_TOP = 0.0

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

# ------------------------------------------------------------ step 1: slab --
print("== step 1: ground-floor slab")
slab_objs = []
SLAB_FBX, TILE_X, TILE_Y = "SM_Wood_Internal_Floor01.fbx", 6.71, 6.36
x = -FP_W / 2
while x < FP_W / 2 - 0.01:
    y = -FP_D / 2
    while y < FP_D / 2 - 0.01:
        objs = import_piece(SLAB_FBX)
        place(objs, 0, (x, y, -2.0)); bake(objs)
        slab_objs += objs
        y += TILE_Y
    x += TILE_X
for co, no in [((FP_W / 2, 0, 0), (1, 0, 0)), ((-FP_W / 2, 0, 0), (-1, 0, 0)),
               ((0, FP_D / 2, 0), (0, 1, 0)), ((0, -FP_D / 2, 0), (0, -1, 0))]:
    bisect_cut(slab_objs, co, no)
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
misses = []
gx = -FP_W / 2 + 0.25
while gx < FP_W / 2:
    gy = -FP_D / 2 + 0.25
    while gy < FP_D / 2:
        hit, loc, no, idx, ob, mat = bpy.context.scene.ray_cast(
            deps, Vector((gx, gy, 5.0)), Vector((0, 0, -1)))
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
# Top-down default camera: this scene starts as a floor plan.
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
bpy.ops.object.camera_add(location=(0.01, -0.01, 45.0))
cam = bpy.context.active_object
cam.name = "ViewCamera"
aim = Vector((0.0, 0.0, 0.0)) - Vector(cam.location)
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
