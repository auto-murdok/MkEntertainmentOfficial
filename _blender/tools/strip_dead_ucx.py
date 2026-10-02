"""POC3: strip faceless UCX point-clouds (0 polys, 0 edges) from FBXs.
Faced UCX hulls are kept (postprocessor converts them to colliders in Unity).
Run: blender --background --factory-startup --python poc3_strip_dead_ucx.py -- outdir src1 [src2 ...]
"""
import bpy, sys, os, json, hashlib
from collections import Counter

def sig(obj):
    me = obj.data
    vc = Counter((round(v.co.x, 4), round(v.co.y, 4), round(v.co.z, 4)) for v in me.vertices)
    fc = Counter(tuple(sorted((round(me.vertices[i].co.x, 4), round(me.vertices[i].co.y, 4), round(me.vertices[i].co.z, 4)) for i in p.vertices)) for p in me.polygons)
    h = hashlib.sha256()
    h.update(repr(sorted(vc.items())).encode())
    h.update(repr(sorted((repr(k), v) for k, v in fc.items())).encode())
    return h.hexdigest()[:16]

args = sys.argv[sys.argv.index("--") + 1:]
OUTDIR, SRCS = args[0], args[1:]
os.makedirs(OUTDIR, exist_ok=True)
results = []
for src in SRCS:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=src)
    before = {o.name: sig(o) for o in bpy.data.objects if o.type == 'MESH'}
    removed = []
    for o in list(bpy.data.objects):
        if o.type == 'MESH' and o.name.upper().startswith("UCX") \
                and len(o.data.polygons) == 0 and len(o.data.edges) == 0:
            removed.append(o.name)
            bpy.data.objects.remove(o, do_unlink=True)
    out = os.path.join(OUTDIR, os.path.basename(src))
    bpy.ops.export_scene.fbx(filepath=out, use_selection=False,
                             embed_textures=False, add_leaf_bones=False)
    # verify: re-import the export, compare non-UCX signatures
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=out)
    after = {o.name: sig(o) for o in bpy.data.objects if o.type == 'MESH'}
    kept_ok = all(after.get(n) == s for n, s in before.items()
                  if n not in removed)
    results.append({"file": os.path.basename(src), "removed": removed,
                    "kept_identical": kept_ok,
                    "bytes_before": os.path.getsize(src),
                    "bytes_after": os.path.getsize(out)})
print("POC3-JSON-BEGIN")
print(json.dumps(results, indent=1))
print("POC3-JSON-END")
