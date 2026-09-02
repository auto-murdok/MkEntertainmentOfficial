# =============================================================================
# UEI - Blender headless UV fix: rotate UV0 on named material slots within
# exported FBX files. Fixes kit meshes whose UVs are authored rotated (bricks
# run vertical instead of horizontal).
# -----------------------------------------------------------------------------
# Run via Apply-UvFixes.ps1 which sets:
#   UEI_FIXES      path to the JSON config - entries:
#                  { "fbx": "<file name or abs path>",
#                    "material": "<name substring matched on FBX material slots>",
#                    "degrees": 90 }
#                  Multiple entries for the SAME fbx are grouped: the file is
#                  restored from its .orig backup once, all rotations applied,
#                  then exported once.
#   UEI_EXPORT_DIR folder containing the FBX files (for relative "fbx" names)
#
# Behavior per FBX group:
#   - backs up to <file>.orig once (first run anchors the original); every run
#     re-rotates from that pristine original (idempotent)
#   - imports the FBX, rotates UV0 (layer 0) around the UV center for the
#     faces assigned to each configured material slot - tiling-safe
#   - re-exports the FBX in place (FBX 7.4 binary, custom split normals kept)
# Completion marker: "UVFIX ALL DONE ok=N fail=N"
# =============================================================================

import bpy
import math
import os
import json
import shutil

config_path = os.environ['UEI_FIXES']
export_dir = os.environ.get('UEI_EXPORT_DIR', '')
cfg = json.load(open(config_path))

def face_world_aspect(mesh, poly):
    """(horizontal_span, vertical_span) of the polygon in world/local space.
    FBX meshes import Z-up: vertical = Z span, horizontal = max(X, Y) span."""
    xs, ys, zs = [], [], []
    for li in poly.loop_indices:
        co = mesh.vertices[mesh.loops[li].vertex_index].co
        xs.append(co.x); ys.append(co.y); zs.append(co.z)
    dx = max(xs) - min(xs)
    dy = max(ys) - min(ys)
    dz = max(zs) - min(zs)
    return max(dx, dy), dz

def face_uv_aspect(mesh, poly, uvl):
    """(u_span, v_span) of the polygon in UV0 space (tiling-aware bbox)."""
    us, vs = [], []
    for li in poly.loop_indices:
        u, v = uvl.data[li].uv
        us.append(u); vs.append(v)
    return max(us) - min(us), max(vs) - min(vs)

def rot_uv_around(u, v, cu, cv, deg):
    r = math.radians(deg)
    s, c = math.sin(r), math.cos(r)
    du, dv = u - cu, v - cv
    return (cu + du * c - dv * s, cv + du * s + dv * c)

def rot_uv(u, v, deg):
    return rot_uv_around(u, v, 0.5, 0.5, deg)

def get_uv_tangents(mesh, p, flat):
    if len(p.loop_indices) < 3:
        return None, None
    co = [mesh.vertices[mesh.loops[li].vertex_index].co for li in p.loop_indices]
    uv = [(flat[2 * li], flat[2 * li + 1]) for li in p.loop_indices]
    dp1 = co[1] - co[0]
    dp2 = co[2] - co[0]
    du1 = uv[1][0] - uv[0][0]
    dv1 = uv[1][1] - uv[0][1]
    du2 = uv[2][0] - uv[0][0]
    dv2 = uv[2][1] - uv[0][1]
    det = du1 * dv2 - du2 * dv1
    if abs(det) < 1e-7:
        return None, None
    inv = 1.0 / det
    tu = (dp1 * dv2 - dp2 * dv1) * inv
    tv = (dp2 * du1 - dp1 * du2) * inv
    return tu, tv

# ------------------------------------------------------- group entries per fbx
groups = {}
order = []
for entry in cfg:
    fbx = entry['fbx']
    if not os.path.isabs(fbx) and export_dir:
        fbx = os.path.join(export_dir, fbx)
    if fbx not in groups:
        groups[fbx] = []
        order.append(fbx)
    groups[fbx].append(entry)

ok = 0
fail = 0
for fbx in order:
    entries = groups[fbx]
    try:
        backup = fbx + '.orig'
        if not os.path.exists(backup):
            shutil.copy2(fbx, backup)
        else:
            # idempotency: always rotate from the pristine original so
            # re-running the tool never accumulates rotations
            shutil.copy2(backup, fbx)

        bpy.ops.wm.read_homefile(use_empty=True)
        print('UVFIX step: importing ' + fbx)
        bpy.ops.import_scene.fbx(filepath=fbx)
        print('UVFIX step: imported')
        # Blender 4.2's FBX importer writes ID properties onto imported data
        # that its exporter then rejects ("this type doesn't support
        # IDProperties") - strip them from every data block
        for coll in (bpy.data.objects, bpy.data.meshes, bpy.data.materials,
                     bpy.data.images, bpy.data.curves, bpy.data.cameras, bpy.data.lights):
            for block in coll:
                try:
                    for k in list(block.keys()):
                        if k != '_RNA_UI':
                            del block[k]
                except Exception:
                    pass
        print('UVFIX step: idprops stripped')

        rotated = []
        for entry in entries:
            mat_needle = entry['material'].lower().replace('_', '').replace(' ', '')
            mode = entry.get('mode', 'degrees')
            for obj in [o for o in bpy.data.objects if o.type == 'MESH']:
                slot_idx = None
                for i, slot in enumerate(obj.material_slots):
                    mname = (slot.material.name if slot.material else '').lower().replace('_', '').replace(' ', '')
                    if mat_needle in mname:
                        slot_idx = i
                        break
                if slot_idx is None:
                    continue
                mesh = obj.data
                if not mesh.uv_layers:
                    print('UVFIX: no UV layers on ' + obj.name)
                    continue
                # bulk API: bypasses Blender 4.2's per-element UV wrappers, which
                # raise "this type doesn't support IDProperties" on
                # C++-imported FBX
                uvl = mesh.uv_layers[0]
                n = len(mesh.loops)
                flat = [0.0] * (n * 2)
                uvl.data.foreach_get('uv', flat)
                changed = 0
                if mode == 'auto':
                    # per-face: rotate only vertical wall faces whose UV U axis
                    # tracks the vertical world axis (= texture pattern runs vertical).
                    deg = float(entry.get('degrees', 90))
                    for p in mesh.polygons:
                        if p.material_index != slot_idx:
                            continue
                        if abs(p.normal.z) > 0.7:
                            continue  # skip horizontal caps, sills, and floors
                        tu, tv = get_uv_tangents(mesh, p, flat)
                        if not tu or not tv:
                            continue
                        tu_len = tu.length
                        tv_len = tv.length
                        if tu_len < 1e-6 or tv_len < 1e-6:
                            continue
                        tu_z = abs(tu.z) / tu_len
                        tv_z = abs(tv.z) / tv_len
                        # If U has a larger vertical component than V, texture is rotated
                        if tu_z > tv_z:
                            for li in p.loop_indices:
                                u, v = flat[2 * li], flat[2 * li + 1]
                                ru, rv = rot_uv(u, v, deg)
                                flat[2 * li] = ru
                                flat[2 * li + 1] = rv
                            changed += 1
                else:
                    deg = float(entry.get('degrees', 90))
                    loop_mat = []
                    for p in mesh.polygons:
                        loop_mat.extend([p.material_index] * p.loop_total)
                    for li in range(n):
                        if loop_mat[li] != slot_idx:
                            continue
                        u, v = flat[2 * li], flat[2 * li + 1]
                        ru, rv = rot_uv(u, v, deg)
                        flat[2 * li] = ru
                        flat[2 * li + 1] = rv
                        changed += 1
                uvl.data.foreach_set('uv', flat)
                rotated.append('%s: %d faces (%s)' % (obj.name, changed, entry['material']))

        if not rotated:
            print('UVFIX FAILED: material(s) %s not found in %s (materials: %s)'
                  % (str([e['material'] for e in entries]), os.path.basename(fbx),
                     str([s.material.name for o in bpy.data.objects if o.type == 'MESH' for s in o.material_slots])))
            fail += 1
            continue

        print('UVFIX step: rotated, exporting')
        bpy.ops.export_scene.fbx(filepath=fbx)
        print('UVFIX %s: %s' % (os.path.basename(fbx), str(rotated)))
        ok += 1
    except Exception as ex:
        import traceback
        traceback.print_exc()
        print('UVFIX FAILED %s: %s' % (fbx, ex))
        fail += 1

print('UVFIX ALL DONE ok=%d fail=%d' % (ok, fail))
