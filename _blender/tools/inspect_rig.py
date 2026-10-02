import bpy, sys, json

results = []
for path in sys.argv[sys.argv.index("--") + 1:]:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    try:
        bpy.ops.import_scene.fbx(filepath=path)
    except Exception as e:
        results.append({"file": path, "error": str(e)})
        continue
    info = {"file": path, "bones": [], "actions": [], "objects": [],
            "dimensions": {}}
    for obj in bpy.data.objects:
        info["objects"].append({"name": obj.name, "type": obj.type,
                                "dim": [round(x, 3) for x in obj.dimensions]})
        if obj.type == 'ARMATURE':
            info["bones"] = [b.name for b in obj.data.bones]
    for act in bpy.data.actions:
        info["actions"].append({"name": act.name,
                                "frames": [int(act.frame_range[0]),
                                           int(act.frame_range[1])]})
    results.append(info)

print("JSON-BEGIN")
print(json.dumps(results, indent=1))
print("JSON-END")
