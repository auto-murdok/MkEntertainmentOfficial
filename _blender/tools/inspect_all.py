import bpy, sys, json, os

root = sys.argv[sys.argv.index("--") + 1:]
files = []
for r in root:
    for dp, dn, fn in os.walk(r):
        for f in fn:
            if f.lower().endswith(".fbx"):
                files.append(os.path.join(dp, f))

files.sort()
agg = []
for path in files:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    try:
        bpy.ops.import_scene.fbx(filepath=path)
    except Exception as e:
        agg.append({"file": os.path.basename(path), "error": str(e)[:120]})
        continue
    total_tris = 0
    total_verts = 0
    uv_missing = []
    n_meshes = 0
    n_bones = 0
    n_actions = 0
    n_mats = 0
    max_bad_w = 0.0
    for obj in bpy.data.objects:
        if obj.type == 'MESH':
            me = obj.data
            n_meshes += 1
            total_verts += len(me.vertices)
            for p in me.polygons:
                total_tris += len(p.vertices) - 2
            if len(me.uv_layers) == 0:
                uv_missing.append(obj.name)
            # skin weight sanity: check total weight per vertex deviates from 1.0
            if obj.vertex_groups and len(obj.vertex_groups) > 1:
                worst = 0.0
                for v in me.vertices:
                    s = sum(g.weight for g in v.groups)
                    d = abs(s - 1.0)
                    if d > worst:
                        worst = d
                if worst > max_bad_w:
                    max_bad_w = worst
        elif obj.type == 'ARMATURE':
            n_bones = len(obj.data.bones)
    n_actions = len(bpy.data.actions)
    n_mats = len(bpy.data.materials)
    agg.append({"file": os.path.relpath(path, root[0]), "meshes": n_meshes,
                "verts": total_verts, "tris": total_tris, "bones": n_bones,
                "actions": n_actions, "mats": n_mats,
                "uv_missing": uv_missing,
                "weight_dev": round(max_bad_w, 4)})

print("JSON-BEGIN")
print(json.dumps(agg))
print("JSON-END")
