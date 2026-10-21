"""Optional model reference renders for the skeleton arsenal's player items. Production inventory icons are authored
PNGs beside each model.py, copied by build.ps1; it no longer invokes this reference renderer. The spine the skeletons
drop still has its own renderer, assets/ecp_spine/icon.py:

    blender --background --factory-startup --python assets/ecp_skel_arsenal/icons.py

Opens each built piece (assets/<name>/out/<name>.blend) and writes out/<name>_icon.png beside it: 256 x 256, transparent,
lit softly from the top left like the game's icons. Weapons lie diagonally across the square with the business end to
the top right and the broad side to the viewer, as the game shows its weapons. The workshop's Unity side packs each
into the bundle as a sprite of the same name.
"""
import math
import os

import bpy
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
SIZE = 256
HOLD = Vector((-0.355, -0.924, -0.14))           # the atgeir's haft in its hold (ecp_skel_atgeir)
BLADE_SIDE = Vector((-0.925, 0.325, 0.198))
BOW_STRING, BOW_LIMBS = Vector((0.995, 0.041, -0.092)), Vector((-0.026, 0.987, 0.158))   # ecp_skel_bow_player's hold

# name: (the business end's direction, the direction the broad side faces), in the built model's own axes
WEAPONS = {
    'ecp_skel_dagger': (Vector((0, -1, 0)), Vector((0, 0, 1))),
    'ecp_skel_sword': (Vector((0, -1, 0)), Vector((0, 0, 1))),
    'ecp_skel_axe': (Vector((0, -1, 0)), Vector((0, 0, 1))),
    'ecp_skel_spear': (Vector((0, 1, 0)), Vector((0, 0, 1))),
    'ecp_skel_atgeir': (HOLD, BLADE_SIDE.cross(HOLD)),
    'ecp_skel_bow_player': (BOW_LIMBS, BOW_STRING.cross(BOW_LIMBS)),
}


def main():
    for name, (head, face) in WEAPONS.items():
        _icon(name, lambda obj, h=head, f=face: _diagonal(obj, h, f))


def _icon(name, pose):
    folder = os.path.join(HERE, '..', name, 'out')
    bpy.ops.wm.open_mainfile(filepath=os.path.join(folder, name + '.blend'))
    item = bpy.data.objects[name]
    for obj in bpy.context.scene.objects:
        obj.hide_render = obj is not item
    pose(item)
    bpy.context.view_layer.update()
    _light()
    _camera(item)
    _render(os.path.join(folder, name + '_icon.png'))


def _diagonal(obj, head, face):
    """Business end to the top right (+X +Z), broad side to the camera (-Y)."""
    head = head.normalized()
    face = (face - head * face.dot(head)).normalized()
    source = Matrix((head, face, head.cross(face))).transposed()
    diagonal = Vector((1.0, 0.0, 1.0)).normalized()
    toward = Vector((0.0, -1.0, 0.0))
    target = Matrix((diagonal, toward, diagonal.cross(toward))).transposed()
    obj.matrix_world = (target @ source.transposed()).to_4x4() @ obj.matrix_world


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
    """Orthographic from the front (-Y), framing the item with a small margin."""
    points = [item.matrix_world @ Vector(c) for c in item.bound_box]
    low = Vector([min(p[i] for p in points) for i in range(3)])
    high = Vector([max(p[i] for p in points) for i in range(3)])
    centre = (low + high) / 2
    cam = bpy.data.objects.new('icon_camera', bpy.data.cameras.new('icon_camera'))
    cam.data.type = 'ORTHO'
    cam.data.ortho_scale = max(high.x - low.x, high.z - low.z) * 1.06
    cam.location = (centre.x, low.y - 2.0, centre.z)
    cam.rotation_euler = (math.radians(90), 0.0, 0.0)
    bpy.context.scene.collection.objects.link(cam)
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


if __name__ == '__main__':
    main()
