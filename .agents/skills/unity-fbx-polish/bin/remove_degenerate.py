"""Remove zero-area (degenerate) polygons from a mesh.
Usage: blender --background --python remove_degenerate.py -- in.fbx out.fbx
"""
import bpy
import bmesh
import sys

fbx_in, fbx_out = sys.argv[sys.argv.index("--") + 1:sys.argv.index("--") + 3]

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=fbx_in)

for o in bpy.data.objects:
    if o.type != 'MESH' or o.name.upper().startswith('UCX_'):
        continue
    # identify via mesh polygon area (bmesh calc_area misses exact-zero faces)
    dead_idx = {p.index for p in o.data.polygons if p.area < 1e-9}
    if not dead_idx:
        continue
    bm = bmesh.new()
    bm.from_mesh(o.data)
    bm.faces.ensure_lookup_table()
    dead = [bm.faces[i] for i in sorted(dead_idx) if i < len(bm.faces)]
    print(f"{o.name}: removing {len(dead)} degenerate faces")
    bmesh.ops.delete(bm, geom=dead, context='FACES')
    bm.to_mesh(o.data)
    bm.free()

bpy.ops.export_scene.fbx(filepath=fbx_out, use_selection=False)
print("EXPORTED:", fbx_out)
