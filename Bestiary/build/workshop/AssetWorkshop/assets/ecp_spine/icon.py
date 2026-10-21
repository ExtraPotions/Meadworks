"""The spine's inventory icon, after a build (build.ps1 -Asset ecp_spine):

    blender --background --factory-startup --python assets/ecp_spine/icon.py

Opens out/ecp_spine.blend and writes out/ecp_spine_icon.png: 256 x 256, transparent, lit like the skeleton arsenal's
icons (assets/ecp_skel_arsenal/icons.py). A long item on the icon's diagonal (codex models/icons.md): the tail end at
the lower left, the neck end at the upper right, seen from the front and above so the crest of spikes and the wings
show.
"""
import math
import os

import bpy
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
NAME = 'ecp_spine'
SIZE = 256
TURN = 50.0          # degrees the spine is turned about Z: its neck end away from the camera, up the icon
ELEVATION = 45.0     # degrees the camera looks down


def main():
    folder = os.path.join(HERE, 'out')
    bpy.ops.wm.open_mainfile(filepath=os.path.join(folder, NAME + '.blend'))
    item = bpy.data.objects[NAME]
    for obj in bpy.context.scene.objects:
        obj.hide_render = obj is not item
    item.matrix_world = Matrix.Rotation(math.radians(TURN), 4, 'Z') @ item.matrix_world
    bpy.context.view_layer.update()
    _light()
    _camera(item)
    _render(os.path.join(folder, NAME + '_icon.png'))


def _light():
    world = bpy.data.worlds.new('icon')
    world.use_nodes = True
    world.node_tree.nodes['Background'].inputs[0].default_value = (0.5, 0.5, 0.5, 1.0)
    world.node_tree.nodes['Background'].inputs[1].default_value = 0.8
    bpy.context.scene.world = world
    for obj in [o for o in bpy.context.scene.objects if o.type == 'LIGHT']:
        obj.hide_render = True
    sun = bpy.data.objects.new('icon_sun', bpy.data.lights.new('icon_sun', 'SUN'))
    sun.data.energy = 3.0
    sun.rotation_euler = (math.radians(30), math.radians(-30), math.radians(25))
    bpy.context.scene.collection.objects.link(sun)


def _camera(item):
    """Orthographic from the front (-Y) and above, framing the item's vertices with a small margin."""
    cam = bpy.data.objects.new('icon_camera', bpy.data.cameras.new('icon_camera'))
    cam.data.type = 'ORTHO'
    cam.rotation_euler = (math.radians(90 - ELEVATION), 0.0, 0.0)
    bpy.context.scene.collection.objects.link(cam)
    points = [item.matrix_world @ v.co for v in item.data.vertices]
    centre = sum(points, Vector()) / len(points)
    view = cam.rotation_euler.to_matrix()
    flat = [view.transposed() @ (p - centre) for p in points]
    cam.data.ortho_scale = max(max(p.x for p in flat) - min(p.x for p in flat),
                               max(p.y for p in flat) - min(p.y for p in flat)) * 1.06
    offset = Vector(((max(p.x for p in flat) + min(p.x for p in flat)) / 2,
                     (max(p.y for p in flat) + min(p.y for p in flat)) / 2, 5.0))
    cam.location = centre + view @ offset
    bpy.context.scene.camera = cam


def _render(path):
    s = bpy.context.scene
    s.render.engine = 'BLENDER_EEVEE'
    s.eevee.taa_render_samples = 64
    s.render.film_transparent = True
    s.render.resolution_x = s.render.resolution_y = SIZE
    s.render.image_settings.color_mode = 'RGBA'
    s.view_settings.view_transform = 'Standard'
    s.render.filepath = path
    bpy.ops.render.render(write_still=True)
    print('WORKSHOP icon', path)


main()
