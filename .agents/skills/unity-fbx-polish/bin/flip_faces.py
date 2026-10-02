"""Flip faces whose normal opposes ALL edge-neighbors (max dot < -0.5).
Uses bmesh normal_flip() to reverse winding; makes faces consistent.
Usage: blender --background --python flip_faces.py -- in.fbx out.fbx
"""
import bpy
import bmesh
import sys
from mathutils import Vector

fbx_in, fbx_out = sys.argv[sys.argv.index("--") + 1:sys.argv.index("--") + 3]

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=fbx_in)

for o in bpy.data.objects:
    if o.type != 'MESH' or o.name.upper().startswith('UCX_'):
        continue
    polys = o.data.polygons
    edge_faces = {}
    for p in polys:
        vs = p.vertices
        n = len(vs)
        for k in range(n):
            e = tuple(sorted((vs[k], vs[(k + 1) % n])))
            edge_faces.setdefault(e, []).append(p.index)
    adj = {p.index: set() for p in polys}
    for faces in edge_faces.values():
        for a in faces:
            for b in faces:
                if a != b:
                    adj[a].add(b)
    flip_idx = set()
    for p in polys:
        if not adj[p.index]:
            continue
        n = Vector(p.normal)
        if max(n.dot(Vector(polys[q].normal)) for q in adj[p.index]) < -0.5:
            flip_idx.add(p.index)
    if not flip_idx:
        continue
    print(f"{o.name}: flipping {len(flip_idx)} faces")
    bm = bmesh.new()
    bm.from_mesh(o.data)
    bm.faces.ensure_lookup_table()
    for idx in sorted(flip_idx):
        if idx < len(bm.faces):
            bm.faces[idx].normal_flip()
    bm.to_mesh(o.data)
    bm.free()
    o.data.update()

bpy.ops.export_scene.fbx(filepath=fbx_out, use_selection=False)
print("EXPORTED:", fbx_out)
