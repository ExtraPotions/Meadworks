"""A presentation sheet of the whole skeleton arsenal, for looking the models over:

    blender --background --factory-startup --python assets/ecp_skel_arsenal/present.py

Appends each built piece (assets/<name>/out/<name>.blend, from build.ps1) and renders it on its own tile, laid
diagonally across the square as the game shows its weapons (business end to the top right, broad side to the camera),
with close-ups of the long weapons' heads, and the spine the skeletons drop. Tiles them into
out/arsenal_sheet.png, four across.
"""
import math
import os
import sys

import bpy
import numpy as np
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, '..', '..', 'blender'))
from workshop import preview, scene  # noqa: E402

TILE = 640
# (piece, part of its length shown from the business end: 1 is all of it)
SHOTS = [('ecp_skel_dagger', 1.0), ('ecp_skel_sword', 1.0), ('ecp_skel_axe', 1.0), ('ecp_skel_mace', 1.0),
         ('ecp_skel_spear', 1.0), ('ecp_skel_spear', 0.2), ('ecp_skel_atgeir', 1.0), ('ecp_skel_atgeir', 0.3),
         ('ecp_skel_bow', 1.0), ('ecp_skel_arrow', 1.0), ('ecp_skel_arrow', 0.16), ('ecp_spine', 1.0)]


def main():
    scene.clear()
    s = bpy.context.scene
    s.render.engine = 'BLENDER_EEVEE'
    s.eevee.taa_render_samples = 64
    s.render.resolution_x = s.render.resolution_y = TILE
    s.view_settings.view_transform = 'Standard'
    preview._world(s)
    preview._sun()
    camera = preview._camera()
    camera.data.type = 'ORTHO'
    tiles = []
    for i, (name, part) in enumerate(SHOTS):
        obj = _append(name)
        _lay(obj)
        path = os.path.join(HERE, 'out', 'present', f'{i:02d}_{name}_{part}.png')
        _shoot(camera, obj, part, path)
        tiles.append(path)
        obj.hide_render = True
    preview.grid(tiles, 4, os.path.join(HERE, 'out', 'arsenal_sheet.png'))
    print('WORKSHOP sheet', os.path.join(HERE, 'out', 'arsenal_sheet.png'))


def _append(name):
    with bpy.data.libraries.load(os.path.join(HERE, '..', name, 'out', name + '.blend')) as (source, target):
        target.objects = [n for n in source.objects if n == name]
    obj = target.objects[0].copy()
    bpy.context.scene.collection.objects.link(obj)
    return obj


def _lay(obj):
    """Longest axis diagonally up to the right in the XZ plane, the heavier (more detailed) end up, broad side to -Y."""
    points = np.array([list(v.co) for v in obj.data.vertices])
    centre = points.mean(0)
    _, _, axes = np.linalg.svd(points - centre, full_matrices=False)
    along = (points - centre) @ axes[0]
    # These models have explicit weapon axes; vertex density picks the pommel
    # instead of the blade on a wrapped spear or feathered arrow.
    head = Vector((0, 1 if obj.name.startswith('ecp_skel_spear') else -1, 0))
    long = Vector(axes[0])
    if obj.name.startswith(('ecp_skel_',)):
        long *= 1 if long.dot(head) >= 0 else -1
    else:
        long *= 1 if (along > 0.3 * along.max()).sum() >= (along < 0.3 * along.min()).sum() else -1
    broad = Vector(axes[1])
    frame = Matrix((broad, long.cross(broad).normalized(), long)).transposed().inverted()
    tilt = Matrix.Rotation(math.radians(45), 3, 'Y')
    obj.matrix_world = (tilt @ frame).to_4x4() @ Matrix.Translation(-Vector(centre))
    bpy.context.view_layer.update()


def _shoot(camera, obj, part, path):
    """Frames the whole piece, or the top `part` of it along the diagonal, looking along +Y."""
    points = [obj.matrix_world @ v.co for v in obj.data.vertices]
    diagonal = Vector((1, 0, 1)).normalized()
    reach = [p.dot(diagonal) for p in points]
    cut = max(reach) - part * (max(reach) - min(reach))
    shown = [p for p, r in zip(points, reach) if r >= cut - 1e-6]
    low = Vector([min(p[i] for p in shown) for i in range(3)])
    high = Vector([max(p[i] for p in shown) for i in range(3)])
    camera.data.ortho_scale = max(high.x - low.x, high.z - low.z) * 1.12
    camera.location = ((low.x + high.x) / 2, -6.0, (low.z + high.z) / 2)
    camera.rotation_euler = (math.radians(90), 0.0, 0.0)
    camera.data.clip_end = 20.0
    bpy.context.scene.render.filepath = path
    bpy.ops.render.render(write_still=True)


main()
