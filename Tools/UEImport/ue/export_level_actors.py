# =============================================================================
# UEI - UE-side level actor dump (mesh + transform inventory for Blender rebuild)
# -----------------------------------------------------------------------------
# Runs inside the UnrealEditor-Cmd.exe pythonscript commandlet. Do not run in a
# normal editor session; use Tools/UEImport/Run-ExportActors.ps1 which prepares
# the environment and launches this script.
#
# Configuration (environment variables, set by the bootstrap runner):
#   UEI_MAP_PATH   /Game/... path to the level (.umap suffix optional). Required.
#   UEI_OUT_JSON   Output path for the actor dump JSON. Required.
#   UEI_HISM_CAP   Max instanced-mesh instances dumped per component (0 = all).
#
# Output JSON shape:
#   {"map": ..., "kind_counts": {...},
#    "actors": [{"label": ..., "class": ..., "kind": "staticmesh"|"instanced"|"other",
#                "mesh": ..., "mesh_path": ...,
#                "loc_cm": [x,y,z], "rot_pyr": [pitch,yaw,roll], "scale": [x,y,z],
#                "instance_count": n, "instances": [{"loc_cm":..., "quat_xyzw":..., "scale":...}]}]}
#   kind=staticmesh: one StaticMeshComponent actor (placed mesh).
#   kind=instanced: ISM/HISM actor (foliage etc); per-instance world transforms.
#   kind=other: anything else (lights, brushes, volumes...) with actor transform.
#
# NOTE on World Partition: partitioned actors that are not streamed in are
# invisible to the level-actor iterator. Compare the dumped total against the
# __ExternalActors__ file count; if short, rerun with the map open in the
# editor UI (same script via the editor Python console works).
#
# Completion markers (scanned by Run-ExportActors.ps1):
#   UEI ACTORS DONE total=<n> sm=<n> instanced=<n> other=<n>
#   UEI ACTORS FAILED: <reason>
# =============================================================================

import unreal
import os
import json


def fail(reason):
    unreal.log('UEI ACTORS FAILED: %s' % reason)


map_path = os.environ.get('UEI_MAP_PATH', '')
out_json = os.environ.get('UEI_OUT_JSON', '')
try:
    hism_cap = int(os.environ.get('UEI_HISM_CAP', '0') or '0')
except Exception:
    hism_cap = 0

if not map_path or not out_json:
    fail('UEI_MAP_PATH and UEI_OUT_JSON must be set')
else:
    try:
        map_path = map_path.replace('\\', '/')
        if map_path.lower().endswith('.umap'):
            map_path = map_path[:-5]
        if not map_path.startswith('/Game'):
            map_path = '/Game' + map_path if map_path.startswith('/') else '/Game/' + map_path
        parent = os.path.dirname(os.path.abspath(out_json))
        if parent:
            os.makedirs(parent, exist_ok=True)

        ok = unreal.EditorLevelLibrary.load_level(map_path)
        unreal.log('UEI: load_level %s -> %s' % (map_path, ok))
        if not ok:
            fail('load_level failed for %s' % map_path)
        else:
            actors = unreal.EditorLevelLibrary.get_all_level_actors()
            unreal.log('UEI: %d level actor(s) visible' % len(actors))
            items = []
            for a in actors:
                try:
                    cls = a.get_class().get_name()
                    label = a.get_actor_label()
                    loc = a.get_actor_location()
                    rot = a.get_actor_rotation()
                    scl = a.get_actor_scale3d()
                except Exception as ex:
                    unreal.log_warning('UEI: actor read failed: %s' % ex)
                    continue
                base = {
                    'label': str(label),
                    'class': str(cls),
                    'loc_cm': [float(loc.x), float(loc.y), float(loc.z)],
                    'rot_pyr': [float(rot.pitch), float(rot.yaw), float(rot.roll)],
                    'scale': [float(scl.x), float(scl.y), float(scl.z)],
                }
                smc = None
                try:
                    smc = a.get_component_by_class(unreal.StaticMeshComponent)
                except Exception:
                    smc = None
                if smc is None and cls == 'StaticMeshActor':
                    # component query can miss (e.g. reparented blueprints);
                    # the root of a StaticMeshActor is its mesh component
                    try:
                        rc = a.root_component
                        if rc is not None and unreal.MathLibrary.class_is_child_of(
                                rc.get_class(), unreal.StaticMeshComponent):
                            smc = rc
                    except Exception:
                        smc = None
                sm = None
                if smc is not None:
                    try:
                        sm = smc.static_mesh
                    except Exception:
                        sm = None
                if sm is not None:
                    base['kind'] = 'staticmesh'
                    base['mesh'] = sm.get_name()
                    base['mesh_path'] = sm.get_path_name()
                    items.append(base)
                    continue
                ismc = None
                for comp_cls in (unreal.InstancedStaticMeshComponent,
                                 unreal.HierarchicalInstancedStaticMeshComponent):
                    try:
                        ismc = a.get_component_by_class(comp_cls)
                    except Exception:
                        ismc = None
                    if ismc is not None:
                        break
                if ismc is not None:
                    ism = None
                    try:
                        ism = ismc.static_mesh
                    except Exception:
                        ism = None
                    base['kind'] = 'instanced'
                    base['mesh'] = ism.get_name() if ism is not None else ''
                    base['mesh_path'] = ism.get_path_name() if ism is not None else ''
                    try:
                        n = ismc.get_instance_count()
                    except Exception:
                        n = -1
                    base['instance_count'] = int(n)
                    inst = []
                    if n > 0:
                        take = n if hism_cap <= 0 else min(n, hism_cap)
                        for i in range(take):
                            try:
                                t = ismc.get_instance_transform(i, True)
                            except Exception:
                                break
                            try:
                                tl = t.translation
                                tq = t.rotation
                                ts = t.scale3d
                                inst.append({
                                    'loc_cm': [float(tl.x), float(tl.y), float(tl.z)],
                                    'quat_xyzw': [float(tq.x), float(tq.y), float(tq.z), float(tq.w)],
                                    'scale': [float(ts.x), float(ts.y), float(ts.z)],
                                })
                            except Exception:
                                break
                    base['instances'] = inst
                    items.append(base)
                    continue
                base['kind'] = 'other'
                if cls == 'StaticMeshActor':
                    base['mesh_missing'] = True
                items.append(base)

            counts = {}
            for it in items:
                k = it.get('kind', 'other')
                counts[k] = counts.get(k, 0) + 1
            mesh_names = sorted(set(it['mesh'] for it in items if it.get('mesh')))
            with open(out_json, 'w', encoding='utf-8') as f:
                json.dump({'map': map_path, 'kind_counts': counts,
                           'mesh_names': mesh_names, 'actors': items}, f)
            unreal.log('UEI ACTORS DONE total=%d sm=%d instanced=%d other=%d meshes=%d -> %s'
                       % (len(items), counts.get('staticmesh', 0),
                          counts.get('instanced', 0), counts.get('other', 0),
                          len(mesh_names), out_json))
    except Exception as ex:
        unreal.log('UEI ACTORS FAILED: %s' % ex)
