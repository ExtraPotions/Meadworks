"""The Bone Crossbow's inventory icon (Elite Creatures Pack's player crossbow), from the built crossbow:

    blender --background assets/ecp_xbow_crossbow/out/ecp_xbow_crossbow.blend --python assets/ecp_xbow_crossbow/icon.py

Writes out/ecp_xbow_crossbow_icon.png: 256 x 256, transparent, the crossbow spanned (its string drawn back to the nut)
seen from straight above, turned diagonally across the square as the game shows its weapons, lit softly from the top
left. The workshop's Unity side packs it as a sprite of the same name into the bundle.
"""
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path[:0] = [HERE, os.path.join(HERE, '..', '..', 'blender')]

import bpy  # noqa: E402
from mathutils import Vector  # noqa: E402

import model  # noqa: E402
from workshop import materials  # noqa: E402

SIZE = 256


def main():
    crossbow = next(o for o in bpy.context.scene.objects if o.type == 'MESH' and not o.name.startswith('col_'))
    for obj in [o for o in bpy.context.scene.objects if o is not crossbow]:
        obj.hide_render = True
    _string(crossbow)
    turn = bpy.data.objects.new('turn', None)
    bpy.context.collection.objects.link(turn)
    for obj in [crossbow] + [o for o in bpy.context.scene.objects if o.name.startswith('icon_string')]:
        obj.parent = turn
    turn.rotation_euler = (0.0, 0.0, math.radians(-45))
    bpy.context.view_layer.update()
    _light()
    _camera([crossbow] + [o for o in bpy.context.scene.objects if o.name.startswith('icon_string')])
    s = bpy.context.scene
    s.render.engine = 'BLENDER_EEVEE'
    s.render.film_transparent = True
    s.render.resolution_x = s.render.resolution_y = SIZE
    s.render.image_settings.color_mode = 'RGBA'
    s.view_settings.view_transform = 'Standard'
    s.render.filepath = os.path.join(HERE, 'out', 'ecp_xbow_crossbow_icon.png')
    bpy.ops.render.render(write_still=True)


def _string(crossbow):
    """The drawn string: prod tips to the nut."""
    mat = materials.flat('icon_string', (0.42, 0.34, 0.22))
    for i, tip in enumerate(model.TIPS):
        a, b = Vector(tip), Vector(model.NUT)
        bpy.ops.mesh.primitive_cylinder_add(vertices=6, radius=0.004, depth=(b - a).length, location=(a + b) / 2)
        cord = bpy.context.active_object
        cord.name = f'icon_string_{i}'
        cord.rotation_mode = 'QUATERNION'
        cord.rotation_quaternion = Vector((0, 0, 1)).rotation_difference((b - a).normalized())
        cord.data.materials.append(mat)


def _light():
    world = bpy.data.worlds.new('icon')
    world.use_nodes = True
    world.node_tree.nodes['Background'].inputs[0].default_value = (0.5, 0.5, 0.5, 1.0)
    world.node_tree.nodes['Background'].inputs[1].default_value = 0.7
    bpy.context.scene.world = world
    sun = bpy.data.objects.new('icon_sun', bpy.data.lights.new('icon_sun', 'SUN'))
    sun.data.energy = 3.0
    sun.rotation_euler = (math.radians(35), math.radians(-25), math.radians(20))
    bpy.context.collection.objects.link(sun)


def _camera(parts):
    """Orthographic, straight down, framing the turned crossbow's bounds with a small margin."""
    points = [o.matrix_world @ Vector(c) for o in parts for c in o.bound_box]
    low = Vector([min(p[i] for p in points) for i in range(3)])
    high = Vector([max(p[i] for p in points) for i in range(3)])
    cam = bpy.data.objects.new('icon_camera', bpy.data.cameras.new('icon_camera'))
    cam.data.type = 'ORTHO'
    cam.data.ortho_scale = max(high.x - low.x, high.y - low.y) * 1.08
    cam.location = ((low.x + high.x) / 2, (low.y + high.y) / 2, high.z + 1.0)
    bpy.context.collection.objects.link(cam)
    bpy.context.scene.camera = cam


main()
