"""Headless polish pass + studio render for one FBX model.

Usage:
  blender --background --python polish_render.py -- <fbx> <out.png> <mode> <texdir>

mode: 'character' (keeps rig, sets up PBR materials from textures, poses idle)
      'static'    (strips UCX collision + UE LOD extras, joins, merges, recalc normals)
"""
import bpy, sys, os, re
from mathutils import Vector

args = sys.argv[sys.argv.index("--") + 1:]
fbx_path, out_png, mode, texdir = args[0], args[1], args[2], args[3]
matmap_path = args[4] if len(args) > 4 else None

bpy.ops.wm.read_factory_settings(use_empty=True)

# ---------- import ----------
bpy.ops.import_scene.fbx(filepath=fbx_path)
imported = list(bpy.context.selected_objects)

def tris_of(objs):
    return sum(sum(len(p.vertices) - 2 for p in o.data.polygons)
               for o in objs if o.type == 'MESH')

before_tris = tris_of(imported)
report = {"file": os.path.basename(fbx_path), "mode": mode,
          "tris_before": before_tris, "notes": []}

# ---------- polish ----------
if mode == "static":
    # strip Unreal collision hulls and LOD extras
    doomed = [o for o in imported
              if o.name.upper().startswith("UCX_")
              or re.search(r"_LOD[1-9]\d*$", o.name, re.I)]
    doomed_names = [o.name for o in doomed]
    for o in doomed:
        bpy.data.objects.remove(o, do_unlink=True)
    report["notes"].append(f"removed {len(doomed)} UCX/LOD-extra objects: "
                           + ", ".join(doomed_names))
    meshes = [o for o in bpy.data.objects if o.type == 'MESH']
    bpy.ops.object.select_all(action='DESELECT')
    for o in meshes:
        o.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    bpy.ops.object.join()
    joined = bpy.context.view_layer.objects.active
    joined.name = "Polished_" + os.path.basename(fbx_path).replace(".fbx", "")
    import bmesh
    me = joined.data
    bm = bmesh.new()
    bm.from_mesh(me)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=0.0001)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(me)
    bm.free()
    me.update()
    for p in me.polygons:
        p.use_smooth = True
    me.use_auto_smooth = True
    me.auto_smooth_angle = 0.5236  # 30 deg
    report["notes"].append("joined meshes, merged doubles, recalculated normals")

    # rebuild materials from the repo's real Unity materials (passed as JSON)
    if matmap_path and os.path.exists(matmap_path):
        import json
        matmap = json.load(open(matmap_path))
        root = os.path.abspath(".")
        def load_img(path, noncolor=False):
            im = bpy.data.images.load(os.path.join(root, path))
            if noncolor:
                im.colorspace_settings.name = "Non-Color"
            return im
        for mat in list(bpy.data.materials):
            spec = matmap.get(mat.name)
            if not spec:
                continue
            mat.use_nodes = True
            nt = mat.node_tree
            nt.nodes.clear()
            out = nt.nodes.new("ShaderNodeOutputMaterial")
            bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
            nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
            # albedo x basecolor tint
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
            # normal map (Unity green-up == Blender OpenGL convention)
            if spec.get("normal"):
                ntex = nt.nodes.new("ShaderNodeTexImage")
                ntex.image = load_img(spec["normal"], noncolor=True)
                nmap = nt.nodes.new("ShaderNodeNormalMap")
                nt.links.new(ntex.outputs["Color"], nmap.inputs["Color"])
                nt.links.new(nmap.outputs["Normal"], bsdf.inputs["Normal"])
            # ORM pack: unity style R=metal/A=smooth ; gltf style B=metal/G=rough
            if spec.get("orm"):
                otex = nt.nodes.new("ShaderNodeTexImage")
                otex.image = load_img(spec["orm"], noncolor=True)
                sep = nt.nodes.new("ShaderNodeSeparateRGB")
                nt.links.new(otex.outputs["Color"], sep.inputs["Image"])
                if spec.get("orm_style") == "gltf":
                    nt.links.new(sep.outputs["B"], bsdf.inputs["Metallic"])
                    nt.links.new(sep.outputs["G"], bsdf.inputs["Roughness"])
                else:
                    mmult = nt.nodes.new("ShaderNodeMath")
                    mmult.operation = 'MULTIPLY'
                    mmult.inputs[1].default_value = spec.get("metallic_mult", 1.0)
                    nt.links.new(sep.outputs["R"], mmult.inputs[0])
                    nt.links.new(mmult.outputs[0], bsdf.inputs["Metallic"])
                    inv = nt.nodes.new("ShaderNodeInvert")
                    inv.inputs[0].default_value = 1.0
                    nt.links.new(otex.outputs["Alpha"], inv.inputs[1])
                    nt.links.new(inv.outputs[0], bsdf.inputs["Roughness"])
            report["notes"].append(f"material {mat.name}: repo textures "
                                   f"({spec.get('orm_style', 'none')} ORM)")

elif mode == "character":
    base = os.path.basename(fbx_path).replace(".fbx", "")
    prefix = "FuseChica" if "FemaleModel" in base or "Chica" in fbx_path else "FuseZombie"
    # find packed sets present in texture dir
    sets = sorted({m.group(1) for m in
                   (re.match(rf"{prefix}_packed(\d+)_diffuse\.png", f)
                    for f in os.listdir(os.path.dirname(fbx_path)))
                   if m})
    for mat in bpy.data.materials:
        mat.use_nodes = True
        nt = mat.node_tree
        nt.nodes.clear()
        out = nt.nodes.new("ShaderNodeOutputMaterial")
        bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
        nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
        # use first packed set that matches this material index
        idx = 0
        m = re.search(r"(\d+)mat$", mat.name)
        if m:
            idx = int(m.group(1))
        s = sets[idx] if idx < len(sets) else sets[0]
        fbxdir = os.path.dirname(os.path.abspath(fbx_path))
        def img(name, noncolor=False, folder=None):
            im = bpy.data.images.load(os.path.join(folder or fbxdir, name))
            if noncolor:
                im.colorspace_settings.name = "Non-Color"
            t = nt.nodes.new("ShaderNodeTexImage")
            t.image = im
            return t
        d = img(f"{prefix}_packed{s}_diffuse.png")
        nt.links.new(d.outputs["Color"], bsdf.inputs["Base Color"])
        r = img(f"{prefix}_packed{s}_roughness.png", noncolor=True, folder=texdir)
        nt.links.new(r.outputs["Color"], bsdf.inputs["Roughness"])
        n = img(f"{prefix}_packed{s}_normal.png", noncolor=True)
        nmap = nt.nodes.new("ShaderNodeNormalMap")
        nt.links.new(n.outputs["Color"], nmap.inputs["Color"])
        nt.links.new(nmap.outputs["Normal"], bsdf.inputs["Normal"])
        report["notes"].append(f"material {mat.name}: PBR setup "
                               f"(diffuse/roughness/normal, packed{s})")
    # strike an idle pose
    sc = bpy.context.scene
    for o in bpy.data.objects:
        if o.type == 'ARMATURE' and o.animation_data and o.animation_data.action:
            sc.frame_set(40)
            report["notes"].append(
                f"posed on '{o.animation_data.action.name}' frame 40")
            break

kept = [o for o in bpy.data.objects if o.type == 'MESH']
report["tris_after"] = tris_of(kept)

# ---------- studio scene ----------
for o in list(bpy.data.objects):
    if o.type in ('LIGHT', 'CAMERA'):
        bpy.data.objects.remove(o, do_unlink=True)

# bounds
corners = []
for o in kept:
    corners += [o.matrix_world @ Vector(c) for c in o.bound_box]
mn = Vector((min(c[i] for c in corners) for i in range(3)))
mx = Vector((max(c[i] for c in corners) for i in range(3)))
center = (mn + mx) / 2
radius = max((mx - mn).length / 2, 0.01)

# ground shadow catcher
bpy.ops.mesh.primitive_plane_add(size=radius * 12, location=(center.x, center.y, mn.z - 0.001))
ground = bpy.context.view_layer.objects.active
ground.name = "Ground"
ground.is_shadow_catcher = True

# camera
cam_data = bpy.data.cameras.new("RenderCam")
cam = bpy.data.objects.new("RenderCam", cam_data)
bpy.context.scene.collection.objects.link(cam)
bpy.context.scene.camera = cam
direction = Vector((1.0, -1.0, 0.65)).normalized()
import math
fov = 2 * math.atan(18.0 / cam_data.lens)  # 36mm sensor, default 50mm lens
dist = radius / math.tan(fov / 2) * 1.25
cam.location = center + direction * dist
d = center - cam.location
cam.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()
cam_data.clip_start = dist / 100
cam_data.clip_end = dist * 100

# lights
sun = bpy.data.objects.new("Sun", bpy.data.lights.new("Sun", 'SUN'))
bpy.context.scene.collection.objects.link(sun)
sun.data.energy = 4.0
sun.rotation_euler = (math.radians(50), 0, math.radians(-35))
fill = bpy.data.objects.new("Fill", bpy.data.lights.new("Fill", 'AREA'))
bpy.context.scene.collection.objects.link(fill)
fill.data.energy = 120 * radius * radius
fill.data.size = radius * 2
fill.location = center + Vector((-1.2, 1.0, 0.9)).normalized() * dist * 0.8
fd = center - fill.location
fill.rotation_euler = fd.to_track_quat('-Z', 'Y').to_euler()

# world: soft studio gray
world = bpy.data.worlds.new("Studio")
world.use_nodes = True
bg = world.node_tree.nodes["Background"]
bg.inputs["Color"].default_value = (0.72, 0.73, 0.76, 1.0)
bg.inputs["Strength"].default_value = 1.0
bpy.context.scene.world = world

# ---------- render ----------
sc = bpy.context.scene
sc.render.engine = 'CYCLES'
sc.cycles.device = 'CPU'
sc.cycles.samples = 96
sc.cycles.use_denoising = False
sc.render.resolution_x = 1024
sc.render.resolution_y = 1024
sc.render.resolution_percentage = 100
sc.render.film_transparent = False
sc.render.image_settings.file_format = 'PNG'
sc.render.filepath = out_png
bpy.ops.render.render(write_still=True)

print("POLISH-REPORT:", report)
print("RENDER_DONE:", out_png)
