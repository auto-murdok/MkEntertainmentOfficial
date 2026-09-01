# =============================================================================
# UEI - Blender batch converter: glTF (.glb) -> FBX
# -----------------------------------------------------------------------------
# Run headless with the Blender bundled by setup-prereqs.ps1:
#   blender.exe --background --factory-startup --python glb2fbx.py
#
# Configuration (environment variables):
#   GLB_IN    folder scanned recursively for .glb files (CUE4Parse output)
#   GLB_OUT   folder that receives one .fbx per .glb (flat)
# =============================================================================

import bpy
import os

in_root = os.environ['GLB_IN']
out_dir = os.environ['GLB_OUT']
os.makedirs(out_dir, exist_ok=True)
count = 0
fail = 0
for root, dirs, files in os.walk(in_root):
    for f in files:
        if not f.lower().endswith('.glb'):
            continue
        src = os.path.join(root, f)
        out = os.path.join(out_dir, f[:-4] + '.fbx')
        try:
            bpy.ops.wm.read_homefile(use_empty=True)
            bpy.ops.import_scene.gltf(filepath=src)
            bpy.ops.export_scene.fbx(
                filepath=out,
                path_mode='COPY',
                embed_textures=False,
                bake_space_transform=True)
            count += 1
        except Exception as ex:
            fail += 1
            print('UEI FBXFAIL ' + f + ' : ' + str(ex))
print('UEI CONVERTED ' + str(count) + ' fail=' + str(fail))
