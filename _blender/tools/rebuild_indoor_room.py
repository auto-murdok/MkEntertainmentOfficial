"""Rebuild _blender/tests/indoor_room.blend with the building kit's real
materials, producing _blender/tests/indoor_room_real.blend + proof renders.

Why this exists: the test room was assembled with every LOD0 collapsed to a
single placeholder material ("wall"/"floor") and every face's material index
flattened to 0, and all LOD/UCX copies were left render-visible (stripe
artifacts). This script restores the real look without touching geometry:

- Harvests canonical LOD0 material slots + per-face material indices from the
  source FBXs and copies them onto the room's LOD0 meshes, after verifying
  each room mesh against its canonical mesh via per-face area ratios
  (invariant under rigid transforms; >=99% of faces must match within 2%).
- Rebuilds the MI_* materials from _blender/polish/matlib_building_kit.json
  using the same node recipe as polish_render.py.
- Deletes the LOD1-5 + UCX meshes (41 objects) that polluted the renders.
- Fixes a units bug in the source scene: the FBX empties carry 0.01 scale
  while the cameras/lights are placed at meter scale, so the room rendered
  as a speck. Empties are rescaled to 1.0.
- Stores texture paths relative to the .blend so it opens in any checkout.

Usage:
  blender --background --python rebuild_indoor_room.py [-- --no-render]
Outputs: _blender/tests/indoor_room_real.blend and renders in _blender/polish/.
"""
import bpy, json, os, re, sys
from collections import Counter

TOOLS_DIR = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(TOOLS_DIR, "..", ".."))
BLEND_IN = os.path.join(REPO, "_blender/tests/indoor_room.blend")
BLEND_OUT = os.path.join(REPO, "_blender/tests/indoor_room_real.blend")
OUTDIR = os.path.join(REPO, "_blender/polish")
matmap = json.load(open(os.path.join(REPO, "_blender/polish/matlib_building_kit.json")))

MESHDIR = os.path.join(REPO, "Assets/ImportedContent/Building_kit/Meshes")
CANON_FBX = {
    "SM_Internal_Wall_DoorFrame01_LOD0": "SM_Internal_Wall_DoorFrame01.fbx",
    "SM_Internal_Wall_wall01_LOD0": "SM_Internal_Wall_wall01.fbx",
    "SM_Internal_Wall_wall03_LOD0": "SM_Internal_Wall_wall03.fbx",
    "SM_Wood_Internal_Floor01_LOD0": "SM_Wood_Internal_Floor01.fbx",
}

def base_name(n):
    return re.sub(r"\.\d{3}$", "", n)

bpy.ops.wm.open_mainfile(filepath=BLEND_IN)

# ---- harvest canonical LOD0 data -------------------------------------------
canon = {}
for lod0_name, fbx_file in CANON_FBX.items():
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=os.path.join(MESHDIR, fbx_file))
    new_objs = [o for o in bpy.data.objects if o not in before]
    target = None
    for o in new_objs:
        if o.type == 'MESH' and base_name(o.name) == lod0_name:
            target = o
    assert target is not None, f"LOD0 not found in {fbx_file}"
    canon[lod0_name] = {
        "slots": [base_name(s.material.name) if s.material else None
                  for s in target.material_slots],
        "indices": [p.material_index for p in target.data.polygons],
        "areas": [p.area for p in target.data.polygons],
    }
    for o in new_objs:
        bpy.data.objects.remove(o, do_unlink=True)
    print("HARVESTED", lod0_name, canon[lod0_name]["slots"],
          Counter(canon[lod0_name]["indices"]))

# ---- build real materials ---------------------------------------------------
def load_img(rel, noncolor=False):
    im = bpy.data.images.load(os.path.join(REPO, rel))
    if noncolor:
        im.colorspace_settings.name = "Non-Color"
    return im

def build_material(name):
    spec = matmap[name]
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    alb = nt.nodes.new("ShaderNodeTexImage")
    alb.image = load_img(spec["albedo"])
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
    print("MATERIAL OK:", name, "->", spec["albedo"])
    return mat

needed = set()
for c in canon.values():
    needed.update(s for s in c["slots"] if s)
real_mats = {n: build_material(n) for n in sorted(needed)}

# ---- restore slots + face indices on room LOD0 meshes -----------------------
failures = []
for o in list(bpy.data.objects):
    if o.type != 'MESH':
        continue
    bname = base_name(o.name)
    if o.name.startswith("UCX") or bname not in canon:
        continue
    c = canon[bname]
    polys = o.data.polygons
    if len(polys) != len(c["indices"]):
        failures.append((o.name, "poly count mismatch"))
        continue
    ratios = []
    ok = True
    for p, ca in zip(polys, c["areas"]):
        if ca <= 0:
            if p.area > 1e-9:
                ok = False
            continue
        ratios.append(p.area / ca)
    k = 0.0
    if ratios:
        k = sorted(ratios)[len(ratios) // 2]
        dev = [abs(r / k - 1.0) for r in ratios] if k > 0 else [1.0]
        n_bad = sum(1 for d in dev if d > 0.02)
        # Pass when >=99% of faces match: face-order correspondence is proven
        # by the overwhelming match; a rare tweaked face keeps canonical index.
        if k <= 0 or n_bad > max(0, int(0.01 * len(dev))):
            ok = False
        elif n_bad:
            print("NOTE", o.name, f"{n_bad} face(s) deviate >2% (max "
                  f"{max(dev):.3f}); keeping canonical indices")
    if not ok:
        failures.append((o.name, "area profile mismatch"))
        continue
    o.data.materials.clear()
    for s in c["slots"]:
        o.data.materials.append(real_mats[s] if s else None)
    for p, idx in zip(polys, c["indices"]):
        p.material_index = idx
    o.data.update()
    print("RESTORED", o.name, "scale^2~", round(k, 4) if ratios else "?")

if failures:
    print("FAILURES:", failures)
    sys.exit(1)

# ---- delete LOD1-5 + UCX meshes ---------------------------------------------
removed = 0
for o in list(bpy.data.objects):
    if o.type == 'MESH' and (o.name.startswith("UCX") or re.search(r"_LOD[1-9]\d*(\.\d{3})?$", o.name)):
        bpy.data.objects.remove(o, do_unlink=True)
        removed += 1
print("REMOVED extra meshes:", removed)
print("remaining meshes:", len([o for o in bpy.data.objects if o.type == 'MESH']))

# ---- fix the units bug: FBX empties carry 0.01 scale, cameras/lights were
# placed for meter scale. Scale the assembly x100 about the origin.
for o in bpy.data.objects:
    if o.type == 'EMPTY' and tuple(round(s, 4) for s in o.scale) == (0.01, 0.01, 0.01):
        o.scale = (1.0, 1.0, 1.0)
        print("RESCALED", o.name)
bpy.context.view_layer.update()

bpy.ops.wm.save_as_mainfile(filepath=BLEND_OUT)

# ---- store texture paths relative to the .blend (portable across checkouts)
for im in bpy.data.images:
    if im.filepath and os.path.isabs(im.filepath):
        im.filepath = bpy.path.relpath(im.filepath)
bpy.ops.wm.save_as_mainfile(filepath=BLEND_OUT)
print("SAVED", BLEND_OUT)

# ---- render the 3 cameras -----------------------------------------------------
if "--no-render" not in sys.argv:
    sc = bpy.context.scene
    for cam_name, out_name in [("c", "indoor_room_overview.png"),
                               ("c.001", "indoor_room_inside_01.png"),
                               ("c.002", "indoor_room_inside_02.png")]:
        cam = bpy.data.objects.get(cam_name)
        if cam is None:
            print("MISSING CAMERA", cam_name)
            continue
        sc.camera = cam
        sc.render.filepath = os.path.join(OUTDIR, out_name)
        bpy.ops.render.render(write_still=True)
        print("RENDER_DONE:", out_name)
print("ALL DONE")
