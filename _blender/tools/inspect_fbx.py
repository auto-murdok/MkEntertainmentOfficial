import bpy, sys, json

results = []
for path in sys.argv[sys.argv.index("--") + 1:]:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    try:
        bpy.ops.import_scene.fbx(filepath=path)
    except Exception as e:
        results.append({"file": path, "error": str(e)})
        continue
    info = {"file": path, "meshes": [], "armatures": [], "materials": [],
            "uv_missing": [], "ngons": 0, "nonmanifold": 0}
    for obj in bpy.data.objects:
        if obj.type == 'MESH':
            me = obj.data
            tris = sum(1 for p in me.polygons if len(p.vertices) == 3)
            quads = sum(1 for p in me.polygons if len(p.vertices) == 4)
            ngons = sum(1 for p in me.polygons if len(p.vertices) > 4)
            info["ngons"] += ngons
            has_uv = len(me.uv_layers) > 0
            if not has_uv:
                info["uv_missing"].append(obj.name)
            info["meshes"].append({
                "name": obj.name, "verts": len(me.vertices),
                "tris": tris, "quads": quads, "ngons": ngons,
                "mats": [m.name if m else None for m in me.materials],
                "uv_layers": [uv.name for uv in me.uv_layers],
                "armature": obj.parent.name if obj.parent and obj.parent.type == 'ARMATURE' else None,
                "vertex_groups": len(obj.vertex_groups),
            })
        elif obj.type == 'ARMATURE':
            info["armatures"].append({"name": obj.name,
                                      "bones": len(obj.data.bones)})
    for m in bpy.data.materials:
        info["materials"].append(m.name)
    results.append(info)

print("JSON-BEGIN")
print(json.dumps(results, indent=1))
print("JSON-END")
