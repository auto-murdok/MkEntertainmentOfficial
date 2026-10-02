"""Probe a headless Blender install against unity-fbx-polish assumptions.

Run:
  blender --background --factory-startup --python probe_blender.py -- <sample.fbx> <out_dir>

Verifies, on any new machine or after a Blender upgrade, before trusting the
repair/QA helpers: legacy `import_scene.fbx`/`export_scene.fbx` registration
(+ `io_scene_fbx` enabled state), the new 5.x `wm.fbx_import` presence, FBX
import sanity (objects, cm object-space scale, material slots, UV layers) and
an export round-trip. Prints machine-readable PROBE lines.
Validated green on Blender 5.2.2 LTS / macOS arm64 (2026-10-02,
docs/blender_cli_macos.md); also runs on 4.x.
"""
import bpy
import os
import sys

args = sys.argv[sys.argv.index("--") + 1:]
if len(args) < 2:
    print("PROBE FAIL usage: probe_blender.py -- <sample.fbx> <out_dir>")
    raise SystemExit(1)
sample, out_dir = args[0], args[1]

print(f"PROBE blender_version={bpy.app.version_string}")
print(f"PROBE python={sys.version.split()[0]}")

# --- 1. operator registration under factory startup -------------------------
def op_exists(category, name):
    cat = getattr(bpy.ops, category, None)
    return hasattr(cat, name)

for cat, name in (
    ("import_scene", "fbx"),      # legacy Python importer (used by helpers)
    ("wm", "fbx_import"),         # new C++ importer (5.0+ default)
    ("export_scene", "fbx"),      # Python exporter (mechanism unchanged in 5.x)
    ("wm", "read_factory_settings"),
    ("render", "render"),
):
    print(f"PROBE op {cat}.{name}={op_exists(cat, name)}")

# which FBX add-ons exist and are enabled (4.x addon_utils vs 5.x extensions)
try:
    import addon_utils
    mods = {m.__name__ for m in addon_utils.modules()}
    fbx_mods = sorted(m for m in mods if "fbx" in m.lower())
    enabled = sorted(m for m in fbx_mods
                     if addon_utils.check(m)[1] or addon_utils.check(m)[0])
    print(f"PROBE addon_utils_fbx_modules={fbx_mods}")
    print(f"PROBE addon_utils_fbx_enabled={enabled}")
except Exception as e:
    print(f"PROBE addon_utils_error={e!r}")

# --- 2. legacy import sanity -------------------------------------------------
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=sample)

meshes = [o for o in bpy.data.objects if o.type == "MESH"]
print(f"PROBE imported_mesh_objects={len(meshes)}")
for o in meshes:
    d = o.matrix_world.to_scale()
    dims = tuple(round(v, 4) for v in o.dimensions)
    mats = [s.material.name if s.material else None for s in o.material_slots]
    uvs = [l.name for l in o.data.uv_layers]
    print(f"PROBE obj={o.name} scale={tuple(round(s,4) for s in d)} "
          f"dims_m={dims} polys={len(o.data.polygons)} mats={mats} uvs={uvs}")

# --- 3. export round-trip ----------------------------------------------------
out_fbx = os.path.join(out_dir, "probe_roundtrip.fbx")
bpy.ops.export_scene.fbx(filepath=out_fbx, use_selection=False)
print(f"PROBE export_ok={os.path.isfile(out_fbx)} size={os.path.getsize(out_fbx)}")

# re-import the exported file and confirm same object count
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=out_fbx)
print(f"PROBE reimport_mesh_objects={len([o for o in bpy.data.objects if o.type=='MESH'])}")
print("PROBE DONE")
