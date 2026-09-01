# =============================================================================
# UEI - UE-side batch exporter (FBX + textures + material manifest)
# -----------------------------------------------------------------------------
# Runs inside the UnrealEditor-Cmd.exe pythonscript commandlet. Do not run in a
# normal editor session; use Tools/UEImport/Run-Export.ps1 which prepares the
# environment and launches this script.
#
# Configuration (environment variables, set by the bootstrap runner):
#   UEI_ASSET_PATH   /Game/... path to a StaticMesh asset OR a folder that
#                    contains StaticMesh assets (recursive=false). Required.
#   UEI_OUT_DIR      Output directory for *.fbx, texture *.png and
#                    import_manifest.csv. Required.
#   UEI_FILTER       Optional case-insensitive name filter applied when
#                    UEI_ASSET_PATH is a folder (substring match on asset name).
#
# Outputs (in UEI_OUT_DIR):
#   <MeshName>.fbx            one per static mesh
#   <TextureName>.png         every texture referenced by the exported meshes'
#                             materials (resolved via the asset registry, so
#                             textures may live anywhere under /Game)
#   import_manifest.csv       mesh,slot,material,param,texture,texture_path
#                             - instance overrides come before parent defaults;
#                               consumers take the FIRST occurrence per
#                               (material, param)
#
# Completion markers (scanned by Run-Export.ps1):
#   UEI EXPORT DONE meshes=<n> textures=<n> rows=<n>
#   UEI EXPORT FAILED: <reason>
# =============================================================================

import unreal
import os


def fail(reason):
    unreal.log('UEI EXPORT FAILED: %s' % reason)


asset_path = os.environ.get('UEI_ASSET_PATH', '')
out_dir = os.environ.get('UEI_OUT_DIR', '')
name_filter = (os.environ.get('UEI_FILTER') or '').lower()

if not asset_path or not out_dir:
    fail('UEI_ASSET_PATH and UEI_OUT_DIR must be set')
else:
    try:
        os.makedirs(out_dir, exist_ok=True)
        asset_path = asset_path.replace('\\', '/')
        if not asset_path.startswith('/Game'):
            asset_path = '/Game' + asset_path
        eal = unreal.EditorAssetLibrary
        me_lib = unreal.MaterialEditingLibrary
        have_names = hasattr(me_lib, 'get_texture_parameter_names')
        have_default = hasattr(me_lib, 'get_material_default_texture_parameter_value')

        # ------------------------------------------------------------------
        # 1. Collect static meshes
        # ------------------------------------------------------------------
        mesh_paths = []
        if eal.does_asset_exist(asset_path):
            mesh_paths.append(asset_path)
        else:
            for p in eal.list_assets(asset_path, recursive=False, include_folder=False):
                full = p if str(p).startswith('/Game') else asset_path + '/' + str(p)
                if name_filter and name_filter not in str(p).lower():
                    continue
                mesh_paths.append(full)

        meshes = []
        for p in mesh_paths:
            try:
                a = eal.load_asset(p)
            except Exception as ex:
                unreal.log_warning('UEI: load failed %s : %s' % (p, ex))
                continue
            if a is None:
                continue
            if unreal.MathLibrary.class_is_child_of(a.get_class(), unreal.StaticMesh):
                meshes.append(a)
        unreal.log('UEI: %d static mesh(es) matched' % len(meshes))
        if not meshes:
            fail('no static meshes matched %s (filter=%r)' % (asset_path, name_filter))
        else:
            # --------------------------------------------------------------
            # 2. Material/texture map (instance overrides first, then parent
            #    chain defaults)
            # --------------------------------------------------------------
            def params_of(mat):
                found = []
                if have_names and have_default:
                    try:
                        for n in me_lib.get_texture_parameter_names(mat):
                            try:
                                tex = me_lib.get_material_default_texture_parameter_value(mat, n)
                            except Exception:
                                tex = None
                            if tex is not None:
                                found.append((str(n), tex))
                    except Exception:
                        pass
                if isinstance(mat, unreal.MaterialInstance):
                    try:
                        for tv in mat.get_editor_property('texture_parameter_values'):
                            info = tv.get_editor_property('parameter_info')
                            pn = str(info.get_editor_property('name'))
                            pv = tv.get_editor_property('parameter_value')
                            if pv is not None:
                                found.append((pn, pv))
                    except Exception as ex:
                        unreal.log_warning('UEI: texture params failed on %s : %s' % (mat.get_name(), ex))
                return found

            rows = []            # (mesh, slot, material, param, texture_name, texture_path)
            texture_names = []   # unique, ordered
            mat_flags = {}       # material name -> two_sided ("1"/"0"/"")
            mesh_exporter = unreal.StaticMeshExporterFBX()
            tex_exporter = unreal.TextureExporterPNG()
            fbx_options = unreal.FbxExportOption()

            def two_sided_of(mi):
                # TwoSided lives on the base Material; walk the parent chain
                cur = mi
                guard = 0
                while cur is not None and guard < 8:
                    try:
                        ts = cur.get_editor_property('two_sided')
                        return '1' if ts else '0'
                    except Exception:
                        pass
                    guard += 1
                    try:
                        cur = cur.get_editor_property('parent') if isinstance(cur, unreal.MaterialInstance) else None
                    except Exception:
                        cur = None
                return ''

            fbx_ok = 0
            fbx_fail = 0
            for mesh in meshes:
                out_fbx = os.path.join(out_dir, mesh.get_name() + '.fbx')
                task = unreal.AssetExportTask()
                task.object = mesh
                task.filename = out_fbx
                task.options = fbx_options
                task.exporter = mesh_exporter
                task.automated = True
                task.prompt = False
                try:
                    ok = mesh_exporter.run_asset_export_task(task)
                except Exception as ex:
                    unreal.log_warning('UEI: fbx exception %s : %s' % (mesh.get_name(), ex))
                    ok = False
                unreal.log('UEI FBX: %s -> %s : %s' % (mesh.get_name(), out_fbx, ok))
                if ok:
                    fbx_ok += 1
                else:
                    fbx_fail += 1

                for sm in mesh.get_editor_property('static_materials'):
                    slot = str(sm.get_editor_property('material_slot_name'))
                    mi = sm.get_editor_property('material_interface')
                    if mi is None:
                        continue
                    if mi.get_name() not in mat_flags:
                        mat_flags[mi.get_name()] = two_sided_of(mi)
                    collected = []
                    cur = mi
                    guard = 0
                    while cur is not None and guard < 8:
                        for entry in params_of(cur):
                            if entry not in collected:
                                collected.append(entry)
                        guard += 1
                        try:
                            cur = cur.get_editor_property('parent') if isinstance(cur, unreal.MaterialInstance) else None
                        except Exception:
                            cur = None
                    for param, tex in collected:
                        tname = tex.get_name()
                        if tname not in texture_names:
                            texture_names.append(tname)
                        rows.append((mesh.get_name(), slot, mi.get_name(), param, tname, tex.get_path_name()))

            # --------------------------------------------------------------
            # 3. Export every referenced texture (registry-resolved by name,
            #    works for textures stored anywhere under /Game)
            # --------------------------------------------------------------
            registry = unreal.AssetRegistryHelpers.get_asset_registry()
            by_name = {}
            try:
                all_tex = registry.get_assets_by_class(
                    unreal.TopLevelAssetPath('/Script/Engine', 'Texture2D'), True)
                for ad in all_tex:
                    # property names shifted across UE versions: prefer
                    # package_name, fall back to object_path
                    val = None
                    for prop in ('package_name', 'object_path'):
                        try:
                            val = str(getattr(ad, prop))
                        except Exception:
                            val = None
                        if val:
                            break
                    if val:
                        by_name[str(ad.asset_name)] = val
            except Exception as ex:
                unreal.log_warning('UEI: asset registry scan failed: %s' % ex)

            tex_ok = 0
            tex_fail = 0
            for tname in texture_names:
                out_png = os.path.join(out_dir, tname + '.png')
                if os.path.exists(out_png):
                    tex_ok += 1
                    continue
                obj_path = by_name.get(tname)
                if not obj_path:
                    unreal.log_warning('UEI: texture not found in registry: %s' % tname)
                    tex_fail += 1
                    continue
                # package_name looks like /Game/..../T_X -> loads the texture
                tex = eal.load_asset(obj_path)
                if tex is None:
                    unreal.log_warning('UEI: texture load failed: %s (%s)' % (tname, obj_path))
                    tex_fail += 1
                    continue
                ttask = unreal.AssetExportTask()
                ttask.object = tex
                ttask.filename = out_png
                ttask.exporter = tex_exporter
                ttask.automated = True
                ttask.prompt = False
                try:
                    ok = tex_exporter.run_asset_export_task(ttask)
                except Exception as ex:
                    unreal.log_warning('UEI: texture exception %s : %s' % (tname, ex))
                    ok = False
                unreal.log('UEI TEX: %s -> %s : %s' % (tname, out_png, ok))
                if ok:
                    tex_ok += 1
                else:
                    tex_fail += 1

            # --------------------------------------------------------------
            # 4. Manifest
            # --------------------------------------------------------------
            # merge with any existing manifest so incremental exports of the
            # same kit accumulate instead of dropping previously exported
            # meshes (rows for re-exported meshes are replaced)
            manifest = os.path.join(out_dir, 'import_manifest.csv')
            run_meshes = set(m.get_name() for m in meshes)
            old_rows = []
            if os.path.exists(manifest):
                with open(manifest) as f:
                    lines = [l.rstrip('\n') for l in f if l.strip()]
                for l in lines[1:]:
                    parts = l.split(',')
                    if len(parts) >= 7 and parts[0] and parts[0] not in run_meshes:
                        old_rows.append(l)
            with open(manifest, 'w') as f:
                f.write('mesh,slot,material,param,texture,texture_path,two_sided\n')
                for l in old_rows:
                    f.write(l + '\n')
                for r in rows:
                    f.write('%s,%s,%s,%s,%s,%s,%s\n'
                            % (r[0], r[1], r[2], r[3], r[4], r[5], mat_flags.get(r[2], '')))
            unreal.log('UEI: manifest kept=%d new=%d (meshes this run=%d)'
                       % (len(old_rows), len(rows), len(run_meshes)))

            unreal.log('UEI EXPORT DONE meshes=%d textures=%d rows=%d fbx_fail=%d tex_fail=%d'
                       % (fbx_ok, tex_ok, len(rows), fbx_fail, tex_fail))
    except Exception as ex:
        unreal.log('UEI EXPORT FAILED: %s' % ex)
