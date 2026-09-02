# =============================================================================
# UEI - bake blended kit materials to flattened textures (UE-side, Path B)
# -----------------------------------------------------------------------------
# For every unique material in import_manifest.csv this renders the REAL
# material (all layer blending, grading, tiling included) onto a flat plane
# and captures the G-buffer BaseColor channel (unlit albedo) to
# Baked/<Material>_Bake_B.png next to the manifest.
#
# The Unity importer prefers Baked/<Material>_Bake_B.png as the base map when
# present. Normals/ORM keep the detailed-layer approximation (see docs).
#
# Run via Run-Export.ps1-style launch (UnrealEditor-Cmd -run=pythonscript).
# Completion marker: "BAKE ALL DONE ok=N fail=N"
# =============================================================================

import unreal
import os
import csv

out_dir = os.environ.get('UEI_OUT_DIR', r'C:\Users\ljtinitanao\Documents\Unreal Projects\MyProject\Exports\Building_kit')
baked_dir = os.path.join(out_dir, 'Baked')
os.makedirs(baked_dir, exist_ok=True)

# ------------------------------------------------------------------ materials
material_names = []
manifest = os.path.join(out_dir, 'import_manifest.csv')
with open(manifest) as f:
    reader = csv.DictReader(f)
    for row in reader:
        m = (row.get('material') or '').strip()
        if m and m not in material_names:
            material_names.append(m)
unreal.log('BAKE: %d unique material(s)' % len(material_names))

# resolve each material's object path via the registry
paths = {}
reg = unreal.AssetRegistryHelpers.get_asset_registry()
for cls_name in ('MaterialInstanceConstant', 'MaterialInstance', 'Material'):
    try:
        for ad in reg.get_assets_by_class(unreal.TopLevelAssetPath('/Script/Engine', cls_name), True):
            n = str(ad.asset_name)
            if n in material_names and n not in paths:
                paths[n] = str(ad.package_name)
    except Exception as ex:
        unreal.log_warning('BAKE: registry scan failed (%s): %s' % (cls_name, ex))

# ------------------------------------------------------------------ world setup
world = unreal.EditorLoadingAndSavingUtils.load_map('/Engine/Maps/Entry')
plane = unreal.EditorAssetLibrary.load_asset('/Engine/BasicShapes/Plane')
eas = unreal.get_editor_subsystem(unreal.EditorActorSubsystem)
sma = eas.spawn_actor_from_class(unreal.StaticMeshActor, unreal.Vector(0, 0, 0), unreal.Rotator(0, 0, 0))
comp = sma.static_mesh_component
try:
    comp.set_static_mesh(plane)
except Exception:
    comp.set_static_mesh(plane, None)
sma.set_actor_scale3d(unreal.Vector(10, 10, 1))

cap_actor = unreal.EditorLevelLibrary.spawn_actor_from_class(unreal.SceneCapture2D, unreal.Vector(0, 0, 800))
cap_actor.set_actor_rotation(unreal.Rotator(-90, 0, 0), False)
cap = cap_actor.get_component_by_class(unreal.SceneCaptureComponent2D)
cap.capture_source = unreal.SceneCaptureSource.SCS_BASE_COLOR
cap.ortho_width = 1000.0

# ------------------------------------------------------------------ bake loop
ok = 0
fail = 0
for name in material_names:
    out = os.path.join(baked_dir, name + '_Bake_B.png')
    try:
        mi = unreal.EditorAssetLibrary.load_asset(paths[name]) if name in paths else None
        if mi is None:
            unreal.log_warning('BAKE: asset not found for %s' % name)
            fail += 1
            continue
        comp.set_material(0, mi)
        rt = unreal.RenderingLibrary.create_render_target2d(
            world, 2048, 2048,
            unreal.TextureRenderTargetFormat.RTF_RGBA8_SRGB,
            unreal.LinearColor(0, 0, 0, 1))
        cap.texture_target = rt
        cap.capture_scene()
        unreal.RenderingLibrary.export_render_target(world, rt, baked_dir, name + '_Bake_B.png')
        good = os.path.exists(out)
        unreal.log('BAKE: %s -> %s : %s' % (name, out, good))
        if good:
            ok += 1
        else:
            fail += 1
    except Exception as ex:
        unreal.log_warning('BAKE: exception on %s : %s' % (name, ex))
        fail += 1

# remove the spike artifact if present
spike = os.path.join(baked_dir, '_spike_basecolor.png')
if os.path.exists(spike):
    os.remove(spike)

unreal.log('BAKE ALL DONE ok=%d fail=%d' % (ok, fail))
