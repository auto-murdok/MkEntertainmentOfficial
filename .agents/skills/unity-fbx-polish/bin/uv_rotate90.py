"""Rotate UVs of one material 90deg about the FIXED point (0.5, 0.5).

Rigid UV-space transform: preserves continuity across triangulation
diagonals. Rotating about each face's own center BREAKS shared edges and
produces visible diagonal seams -- never do that.

Usage: blender --background --python uv_rotate90.py -- in.fbx out.fbx MatNameSubstring
"""
import bpy
import sys
from mathutils import Vector
import math

fbx_in, fbx_out, mat_sub = sys.argv[sys.argv.index("--") + 1:sys.argv.index("--") + 4]

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=fbx_in)

C = Vector((0.5, 0.5))
COS, SIN = math.cos(math.radians(90)), math.sin(math.radians(90))
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
            rel = Vector(uv_layer.data[li].uv[:]) - C
            rot = Vector((rel.x * COS - rel.y * SIN,
                          rel.x * SIN + rel.y * COS)) + C
            uv_layer.data[li].uv = rot[:]
            fixed += 1

bpy.ops.export_scene.fbx(filepath=fbx_out, use_selection=False)
print(f"ROTATED {fixed} loops matching '{mat_sub}' -> {fbx_out}")
