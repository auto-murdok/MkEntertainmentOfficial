"""Kit census scene: all 50 building-kit pieces, LOD0, separated on a uniform
grid with name labels and real materials — one .blend for visual debugging.

Layout (option A): 10 cols x 5 rows, 24 m cells, pieces at real scale
centered per cell, alphabetical order (families cluster by SM_* prefix).

Usage:
  blender --background --python _blender/tools/build_kit_overview.py
Output: _blender/tests/kit_overview.blend
"""
import bpy, bmesh, os, re, json, math, glob
from mathutils import Vector, Matrix

TOOLS_DIR = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(TOOLS_DIR, "..", ".."))
MESHDIR = os.path.join(REPO, "Assets/ImportedContent/Building_kit/Meshes")
MATLIB = os.path.join(REPO, "_blender/polish/matlib_building_kit.json")
OUT_BLEND = os.path.join(REPO, "_blender/tests/kit_overview.blend")
matmap = json.load(open(MATLIB))

COLS, CELL = 10, 24.0

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

fbxs = sorted(os.path.basename(p) for p in glob.glob(os.path.join(MESHDIR, "SM_*.fbx")))
print(f"PIECES: {len(fbxs)}")
for idx, fbx in enumerate(fbxs):
    objs = import_piece(fbx)
    mn, mx = combined_bbox(objs)
    cx = (idx % COLS) * CELL
    cy = -(idx // COLS) * CELL
    center = (mn + mx) / 2
    dx, dy, dz = cx - center.x, cy - center.y, 0.0 - mn.z
    M = Matrix.Translation(Vector((dx, dy, dz)))
    for o in objs:
        o.matrix_basis = M @ o.matrix_basis
    bpy.context.view_layer.update()
    # label above the piece
    short = re.sub(r"^SM_", "", fbx).replace(".fbx", "")
    bpy.ops.object.text_add(location=(cx, cy, (mx.z - mn.z) + 2.0))
    t = bpy.context.active_object
    t.name = "Label_" + short
    t.data.body = short
    t.data.align_x = 'CENTER'
    t.scale = (1.6, 1.6, 1.6)
    print(f"  [{idx + 1}/{len(fbxs)}] {fbx} -> cell ({cx:.0f}, {cy:.0f})")

# ---------------------------------------------------------------- ground ----
w_all, d_all = COLS * CELL, ((len(fbxs) + COLS - 1) // COLS) * CELL
bpy.ops.mesh.primitive_plane_add(size=1, location=(w_all / 2 - CELL / 2, -d_all / 2 + CELL / 2, -0.05))
gp = bpy.context.active_object
gp.name = "Ground"
gp.scale = (w_all + CELL, d_all + CELL, 1)
bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
gm = bpy.data.materials.new("GroundMat")
gm.use_nodes = True
gm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.16, 0.18, 0.13, 1)
gm.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = 1.0
gp.data.materials.append(gm)

# -------------------------------------------------------------- materials ----
# Kit defect: some pieces ship faces with unassigned "Fbx Default Material"
# (renders white). Point those faces at the piece's own slate slot if one
# exists, else at its first textured slot.
for _o in [o for o in bpy.data.objects if o.type == 'MESH']:
    _slots = [s.material for s in _o.material_slots]
    _roof = next((i for i, m in enumerate(_slots)
                  if m and m.name.startswith("MI_Roof.")), None)
    if _roof is None:
        continue
    for _p in _o.data.polygons:
        _m = _slots[_p.material_index] if _p.material_index < len(_slots) else None
        if _m and _m.name.startswith("Fbx Default"):
            _p.material_index = _roof
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
bpy.ops.object.light_add(type='SUN', location=(120.0, -150.0, 180.0))
sun = bpy.context.active_object
sun.name = "ViewSun"
sun.data.energy = 3.0
bpy.ops.object.light_add(type='SUN', location=(-100.0, 120.0, 120.0))
fill = bpy.context.active_object
fill.name = "ViewFill"
fill.data.energy = 0.7
w = bpy.data.worlds.new("ViewWorld")
w.use_nodes = True
w.node_tree.nodes["Background"].inputs["Color"].default_value = (0.55, 0.65, 0.80, 1)
w.node_tree.nodes["Background"].inputs["Strength"].default_value = 1.0
bpy.context.scene.world = w
bpy.ops.object.camera_add(location=(w_all / 2 + 250.0, -d_all - 220.0, 215.0))
cam = bpy.context.active_object
cam.name = "ViewCamera"
aim = Vector((w_all / 2 - CELL / 2, -d_all / 2, 4.0)) - Vector(cam.location)
cam.rotation_euler = aim.to_track_quat('-Z', 'Y').to_euler()
bpy.context.scene.camera = cam
print("  view setup: 2 suns + world + camera built")

bpy.ops.wm.save_as_mainfile(filepath=OUT_BLEND)

# ---- store texture paths relative to the .blend (portable across checkouts)
for im in bpy.data.images:
    if im.filepath and os.path.isabs(im.filepath):
        im.filepath = bpy.path.relpath(im.filepath)
bpy.ops.wm.save_as_mainfile(filepath=OUT_BLEND)
print("BUILD_DONE", OUT_BLEND)
