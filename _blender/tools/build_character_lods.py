"""Build a character LOD chain from a skinned FBX (Blender headless).

POC-proven on ZombieModel (2026-10-02, Blender 5.2.2):
  LOD0 = original geometry with skin weights capped at 4 influences/vertex
  LOD1 = Decimate collapse 0.5, weights re-capped
  LOD2 = Decimate collapse 0.5 again (0.25 total), weights re-capped

Lessons baked in:
  - Decimate reintroduces extra bone influences on merged vertices, so the
    Limit Total pass must run AFTER every decimate step, not just once.
  - Export with add_leaf_bones=False or the rig gains +13 dummy tip bones.
  - Armature modifiers are viewport-disabled during decimate so the bake
    does not include the bind pose deformation.

Usage:
  blender --background --factory-startup --python build_character_lods.py \
      -- <src.fbx> <outdir> [limit] [ratio]
Outputs <Base>_LOD0.fbx / _LOD1.fbx / _LOD2.fbx in <outdir> + JSON metrics.
"""
import bpy, sys, os, json

argv = sys.argv[sys.argv.index("--") + 1:]
SRC, OUTDIR = argv[0], argv[1]
LIMIT = int(argv[2]) if len(argv) > 2 else 4
RATIO = float(argv[3]) if len(argv) > 3 else 0.5
BASE = os.path.splitext(os.path.basename(SRC))[0]
os.makedirs(OUTDIR, exist_ok=True)


def tri_total():
    t = 0
    for o in bpy.data.objects:
        if o.type == 'MESH':
            o.data.calc_loop_triangles()
            t += len(o.data.loop_triangles)
    return t


def max_influences():
    mx = 0
    for o in bpy.data.objects:
        if o.type == 'MESH' and o.vertex_groups:
            for v in o.data.vertices:
                n = sum(1 for g in v.groups if g.weight > 0.001)
                mx = max(mx, n)
    return mx


def limit_total(obj, limit=4):
    """Keep top-`limit` weights per vertex, renormalize, drop the rest."""
    groups = obj.vertex_groups
    for v in obj.data.vertices:
        entries = [(g.group, g.weight) for g in v.groups if g.weight > 0.0]
        if len(entries) <= limit:
            continue
        entries.sort(key=lambda e: -e[1])
        keep, drop = entries[:limit], entries[limit:]
        s = sum(w for _, w in keep) or 1.0
        for gi, _ in drop:
            groups[gi].remove([v.index])
        for gi, w in keep:
            groups[gi].add([v.index], w / s, 'REPLACE')


def decimate_all(ratio):
    for o in [o for o in bpy.data.objects if o.type == 'MESH']:
        arm = next((m for m in o.modifiers if m.type == 'ARMATURE'), None)
        if arm:
            arm.show_viewport = False
        mod = o.modifiers.new("LODDecimate", 'DECIMATE')
        mod.decimate_type = 'COLLAPSE'
        mod.ratio = ratio
        bpy.ops.object.select_all(action='DESELECT')
        o.select_set(True)
        bpy.context.view_layer.objects.active = o
        bpy.ops.object.modifier_apply(modifier=mod.name)
        if arm:
            arm.show_viewport = True


def cap_all():
    for o in [o for o in bpy.data.objects if o.type == 'MESH']:
        limit_total(o, LIMIT)


def export(tag):
    path = os.path.join(OUTDIR, f"{BASE}_{tag}.fbx")
    bpy.ops.export_scene.fbx(filepath=path, use_selection=False,
                             embed_textures=False, add_leaf_bones=False)
    return {"tris": tri_total(), "max_influences": max_influences(),
            "file": path, "bytes": os.path.getsize(path)}


bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=SRC)

metrics = {"source": BASE, "source_tris": tri_total(),
           "source_max_influences": max_influences()}
cap_all()
metrics["lod0"] = export("LOD0")
decimate_all(RATIO)
cap_all()
metrics["lod1"] = export("LOD1")
decimate_all(RATIO)
cap_all()
metrics["lod2"] = export("LOD2")
metrics["bones"] = sum(len(o.data.bones) for o in bpy.data.objects
                       if o.type == 'ARMATURE')
print("LODGEN-JSON-BEGIN")
print(json.dumps(metrics, indent=1))
print("LODGEN-JSON-END")
