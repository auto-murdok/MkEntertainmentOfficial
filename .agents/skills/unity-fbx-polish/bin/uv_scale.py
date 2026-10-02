"""Uniform-scale UVs of one material about the FIXED point (0.5, 0.5).

Affine transform: preserves UV continuity across shared edges (unlike
per-face scaling). Use to match texture density between materials, e.g.
make base-wall bricks the same size as upper-wall bricks.

Usage: blender --background --python uv_scale.py -- in.fbx out.fbx MatNameSubstring factor
"""
import bpy
import sys
from mathutils import Vector

args = sys.argv[sys.argv.index("--") + 1:]
fbx_in, fbx_out, mat_sub, factor = args[0], args[1], args[2], float(args[3])

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=fbx_in)

C = Vector((0.5, 0.5))
fixed = 0

for o in bpy.data.objects:
    if o.type != 'MESH' or o.name.upper().startswith('UCX_'):
        continue
    midx = next((i for i, m in enumerate(o.data.materials)
                 if m and mat_sub in m.name), None)
    if midx is None:
        continue
    uv_layer = o.data.uv_layers.get('UVmap_0') or o.data.uv_layers[0]
    for p in o.data.polygons:
        if p.material_index != midx:
            continue
        for li in p.loop_indices:
            uv = Vector(uv_layer.data[li].uv[:])
            uv_layer.data[li].uv = (C + (uv - C) * factor)[:]
            fixed += 1

bpy.ops.export_scene.fbx(filepath=fbx_out, use_selection=False)
print(f"SCALED {fixed} loops matching '{mat_sub}' x{factor} -> {fbx_out}")
