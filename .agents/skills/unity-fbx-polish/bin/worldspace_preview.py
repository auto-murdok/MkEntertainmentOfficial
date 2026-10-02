"""Render FBX meshes with a Blender replication of a Unity-style
world-aligned triplanar shader (cf. UEI/WorldAlignedLit):

    uvX = (-Z, Y) / tile_m      uvY = (X, Z) / tile_m      uvZ = (X, Y) / tile_m
    w   = |worldNormal|^sharpness, normalized; color = sum(w_i * tex_i)

Ignores mesh UVs entirely -- shows the true in-game look for materials that
use world-space projection.

Usage:
  blender --background --python worldspace_preview.py -- \\
      out.png albedo.png 300 4.0 in1.fbx [xoff1] [in2.fbx [xoff2] ...]

  out.png      render target
  albedo.png   brick/albedo texture
  300          _TextureSize in cm  (300 -> 3 m tile)
  4.0          _BlendSharpness
  inN.fbx      mesh; optional x-offset places pieces side by side
"""
import bpy
import sys
import math
from mathutils import Vector

args = sys.argv[sys.argv.index("--") + 1:]
out_png, albedo, tex_cm, sharp = args[0], args[1], float(args[2]), float(args[3])
SCALE = 1.0 / (tex_cm / 100.0)

bpy.ops.wm.read_factory_settings(use_empty=True)
rest = args[4:]
i = 0
while i < len(rest):
    fbx = rest[i]
    try:
        xoff = float(rest[i + 1])
        i += 2
    except (IndexError, ValueError):
        xoff = 0.0
        i += 1
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=fbx)
    for o in bpy.data.objects:
        if o in before or o.type != 'MESH' or o.name.upper().startswith('UCX_'):
            continue
        o.location.x += xoff

for o in [x for x in list(bpy.data.objects) if x.name.upper().startswith('UCX_')]:
    bpy.data.objects.remove(o, do_unlink=True)


def build_triplanar(image_path):
    mat = bpy.data.materials.new('WorldTriplanar')
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()
    N, L = nt.nodes.new, nt.links.new

    out = N('ShaderNodeOutputMaterial')
    bsdf = N('ShaderNodeBsdfPrincipled')
    bsdf.inputs['Roughness'].default_value = 0.85
    L(bsdf.outputs['BSDF'], out.inputs['Surface'])

    tc = N('ShaderNodeTexCoord')
    vt_p = N('ShaderNodeVectorTransform')
    vt_p.convert_from, vt_p.convert_to, vt_p.vector_type = 'OBJECT', 'WORLD', 'POINT'
    L(tc.outputs['Object'], vt_p.inputs['Vector'])
    sep = N('ShaderNodeSeparateXYZ')
    L(vt_p.outputs['Vector'], sep.inputs['Vector'])

    def uv_pair(u_src, u_m, v_src, v_m):
        mu = N('ShaderNodeMath'); mu.operation = 'MULTIPLY'
        L(u_src, mu.inputs[0]); mu.inputs[1].default_value = u_m
        mv = N('ShaderNodeMath'); mv.operation = 'MULTIPLY'
        L(v_src, mv.inputs[0]); mv.inputs[1].default_value = v_m
        c = N('ShaderNodeCombineXYZ')
        L(mu.outputs['Value'], c.inputs['X']); L(mv.outputs['Value'], c.inputs['Y'])
        return c

    uvs = (uv_pair(sep.outputs['Z'], -SCALE, sep.outputs['Y'], SCALE),
           uv_pair(sep.outputs['X'], SCALE, sep.outputs['Z'], SCALE),
           uv_pair(sep.outputs['X'], SCALE, sep.outputs['Y'], SCALE))

    texs = []
    for uv in uvs:
        t = N('ShaderNodeTexImage')
        t.image = bpy.data.images.load(image_path)
        t.extension = 'REPEAT'
        L(uv.outputs['Vector'], t.inputs['Vector'])
        texs.append(t)

    geom = N('ShaderNodeNewGeometry')
    vt_n = N('ShaderNodeVectorTransform')
    vt_n.convert_from, vt_n.convert_to, vt_n.vector_type = 'OBJECT', 'WORLD', 'NORMAL'
    L(geom.outputs['Normal'], vt_n.inputs['Vector'])
    sepn = N('ShaderNodeSeparateXYZ')
    L(vt_n.outputs['Vector'], sepn.inputs['Vector'])

    weights = []
    for comp in 'XYZ':
        ab = N('ShaderNodeMath'); ab.operation = 'ABSOLUTE'
        L(sepn.outputs[comp], ab.inputs[0])
        pw = N('ShaderNodeMath'); pw.operation = 'POWER'
        L(ab.outputs['Value'], pw.inputs[0]); pw.inputs[1].default_value = sharp
        weights.append(pw)
    s = N('ShaderNodeMath'); s.operation = 'ADD'
    L(weights[0].outputs['Value'], s.inputs[0]); L(weights[1].outputs['Value'], s.inputs[1])
    s2 = N('ShaderNodeMath'); s2.operation = 'ADD'
    L(s.outputs['Value'], s2.inputs[0]); L(weights[2].outputs['Value'], s2.inputs[1])

    acc = None
    for tex, w in zip(texs, weights):
        wn = N('ShaderNodeMath'); wn.operation = 'DIVIDE'
        L(w.outputs['Value'], wn.inputs[0]); L(s2.outputs['Value'], wn.inputs[1])
        comb = N('ShaderNodeCombineXYZ')
        for ax in 'XYZ':
            L(wn.outputs['Value'], comb.inputs[ax])
        vm = N('ShaderNodeVectorMath'); vm.operation = 'MULTIPLY'
        L(tex.outputs['Color'], vm.inputs[0]); L(comb.outputs['Vector'], vm.inputs[1])
        if acc is None:
            acc = vm
        else:
            va = N('ShaderNodeVectorMath'); va.operation = 'ADD'
            L(acc.outputs['Vector'], va.inputs[0]); L(vm.outputs['Vector'], va.inputs[1])
            acc = va
    L(acc.outputs['Vector'], bsdf.inputs['Base Color'])
    return mat


tri = build_triplanar(albedo)
for o in bpy.data.objects:
    if o.type == 'MESH':
        o.data.materials.clear()
        o.data.materials.append(tri)

for o in list(bpy.data.objects):
    if o.type in ('LIGHT', 'CAMERA'):
        bpy.data.objects.remove(o, do_unlink=True)

kept = [o for o in bpy.data.objects if o.type == 'MESH']
corners = [o.matrix_world @ Vector(c) for o in kept for c in o.bound_box]
mn = Vector((min(c[j] for c in corners) for j in range(3)))
mx = Vector((max(c[j] for c in corners) for j in range(3)))
center, radius = (mn + mx) / 2, max((mx - mn).length / 2, 0.01)

cam_data = bpy.data.cameras.new('C')
cam = bpy.data.objects.new('C', cam_data)
bpy.context.scene.collection.objects.link(cam)
bpy.context.scene.camera = cam
direction = Vector((0.2, -1.0, 0.28)).normalized()
dist = radius / math.tan(math.atan(18.0 / 50.0)) * 1.2
cam.location = center + direction * dist
cam.rotation_euler = (center - cam.location).to_track_quat('-Z', 'Y').to_euler()
cam_data.lens = 50

sun = bpy.data.objects.new('S', bpy.data.lights.new('S', 'SUN'))
bpy.context.scene.collection.objects.link(sun)
sun.data.energy = 5.0
sun.rotation_euler = (math.radians(30), 0, math.radians(-40))
world = bpy.data.worlds.new('W')
world.use_nodes = True
world.node_tree.nodes['Background'].inputs['Strength'].default_value = 1.4
bpy.context.scene.world = world

sc = bpy.context.scene
sc.view_settings.view_transform = 'Standard'
sc.render.engine = 'CYCLES'
sc.cycles.samples = 48
sc.render.resolution_x, sc.render.resolution_y = 1100, 950
sc.render.filepath = out_png
bpy.ops.render.render(write_still=True)
print('RENDERED:', out_png)
