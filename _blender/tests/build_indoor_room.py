"""Build an indoor room from the building kit in Blender (headless).

Assembles: SM_Wood_Internal_Floor01 + 4 internal walls (north, south w/ door,
west, east) into a single 6.7 x 6.35 m room, 5.5 m walls.

Usage:
    blender --background --python build_indoor_room.py

Outputs: indoor_room.blend + renders in renders/

Known quirks (Blender 4.0.2 headless on Linux):
- FBX meshes are in centimeters -> vertices scaled x0.01.
- The FBX importer loads LOD0..LOD5 + UCX collision meshes per piece.
  Deleting/hiding the extra LODs corrupts rendering in background mode
  (shared datablocks), so they are left in the scene. Open the .blend in
  the Blender UI and delete the *_LOD1..5 and UCX_* objects for a clean file.
- Object transforms (location/rotation) do not reliably update matrix_world
  in background mode, so placement is baked directly into the vertices.
- Materials here are flat placeholder colors; the real repo materials
  (wallpaper, wainscoting, wood floor) are applied in Unity via
  WorldAlignedLit and are not reproduced in this test scene.
"""
import bpy, os, math
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))  # _blender/tests -> repo root
M = os.path.join(REPO, 'Assets/ImportedContent/Building_kit/Meshes')
RENDERS = os.path.join(HERE, 'renders')
os.makedirs(RENDERS, exist_ok=True)

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scoll = scene.collection


def add_piece(name, tx, ty, tz, rot_deg):
    bpy.ops.import_scene.fbx(filepath=os.path.join(M, name + '.fbx'))
    cands = [x for x in bpy.data.objects if x.type == 'MESH'
             and '_LOD0' in x.name and 'UCX' not in x.name.upper()
             and name in x.name]
    o = cands[-1]
    # vertex scale cm -> m
    for v in o.data.vertices:
        v.co *= 0.01
    # manual Z-rotation bake + translate so bbox min lands at (tx, ty, tz)
    vs = [(v.co.x, v.co.y, v.co.z) for v in o.data.vertices]
    lmn = (min(v[0] for v in vs), min(v[1] for v in vs), min(v[2] for v in vs))
    lmx = (max(v[0] for v in vs), max(v[1] for v in vs), max(v[2] for v in vs))
    rad = math.radians(rot_deg); cr = math.cos(rad); sr = math.sin(rad)
    xs, ys = [], []
    for x in (lmn[0], lmx[0]):
        for y in (lmn[1], lmx[1]):
            xs.append(x * cr - y * sr); ys.append(x * sr + y * cr)
    rmn = (min(xs), min(ys), lmn[2])
    ox, oy, oz = tx - rmn[0], ty - rmn[1], tz - rmn[2]
    for v in o.data.vertices:
        x, y, z = v.co.x, v.co.y, v.co.z
        v.co = Vector((x * cr - y * sr + ox, x * sr + y * cr + oy, z + oz))
    o.data.update()
    return o


# Room layout (epsilon offset avoids LOD z-fighting at the origin)
EPS = 0.02
pieces = []
pieces.append(add_piece('SM_Wood_Internal_Floor01', EPS, EPS, 0, 0))
pieces.append(add_piece('SM_Internal_Wall_wall01', EPS, EPS + 6.35, 0.3, 0))        # north
pieces.append(add_piece('SM_Internal_Wall_wall03', EPS + 6.01, EPS + 6.35, 0.3, 0))  # north filler
pieces.append(add_piece('SM_Internal_Wall_DoorFrame01', EPS + 0.85, EPS - 0.63, 0.3, 0))  # south door
pieces.append(add_piece('SM_Internal_Wall_wall03', EPS + 5.86, EPS - 0.63, 0.3, 0))  # south filler
pieces.append(add_piece('SM_Internal_Wall_wall01', EPS + 0.63, EPS, 0.3, 90))       # west
pieces.append(add_piece('SM_Internal_Wall_wall01', EPS + 6.7, EPS, 0.3, 90))        # east
print(f'placed {len(pieces)} pieces')

# Placeholder materials
wall_mat = bpy.data.materials.new('wall')
wall_mat.use_nodes = True
wall_mat.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = (0.84, 0.80, 0.74, 1)
floor_mat = bpy.data.materials.new('floor')
floor_mat.use_nodes = True
floor_mat.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = (0.58, 0.42, 0.30, 1)
for i, o in enumerate(pieces):
    o.data.materials.clear()
    o.data.materials.append(floor_mat if i == 0 else wall_mat)
    for p in o.data.polygons:
        p.material_index = 0

# Lighting
scene.render.engine = 'CYCLES'
scene.cycles.samples = 80
scene.render.resolution_x, scene.render.resolution_y = 1280, 800
world = bpy.data.worlds.new('W')
world.use_nodes = True
world.node_tree.nodes['Background'].inputs[0].default_value = (0.78, 0.82, 0.90, 1)
world.node_tree.nodes['Background'].inputs[1].default_value = 1.1
scene.world = world
sun = bpy.data.objects.new('s', bpy.data.lights.new('s', 'SUN'))
sun.data.energy = 5
sun.rotation_euler = (math.radians(55), 0, math.radians(210))
scoll.objects.link(sun)
pt = bpy.data.objects.new('p', bpy.data.lights.new('p', 'POINT'))
pt.data.energy = 2000
pt.data.shadow_soft_size = 0.8
pt.location = (3.3, 3.2, 4.2)
scoll.objects.link(pt)


def rv(out, pos, tgt):
    cd = bpy.data.cameras.new('c'); co = bpy.data.objects.new('c', cd)
    scoll.objects.link(co)
    scene.camera = co
    co.location = Vector(pos)
    co.rotation_euler = (Vector(tgt) - Vector(pos)).to_track_quat('-Z', 'Y').to_euler()
    scene.render.filepath = os.path.join(RENDERS, out)
    bpy.ops.render.render(write_still=True)
    print('RENDERED', out)


rv('indoor_ext.png', (11, -7, 7), (3.3, 3, 1.5))
rv('indoor_door.png', (3.3, -4, 2.2), (3.3, 5, 1.8))
rv('indoor_int.png', (5.8, 5.2, 2.0), (1.5, 1.5, 1.2))
print('DONE')
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, 'indoor_room.blend'))
print('SAVED')
