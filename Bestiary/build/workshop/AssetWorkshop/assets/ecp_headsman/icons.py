"""The inventory icons of the Crypt Executioner's greataxe and its axehead (Elite Creatures Pack), from the built axe:

    blender --background assets/ecp_bone_greataxe/out/ecp_bone_greataxe.blend --python assets/ecp_headsman/icons.py

Writes out/icons/ecp_greataxe_icon.png (the whole axe) and ecp_greataxe_axehead_icon.png (the head alone: the islands
whose middles are above the haft's top, as the workshop's Unity side cuts the axehead item, HeadsmanPieces): 256 x 256,
transparent, the blade's face seen square on, turned diagonally across the square as the game shows its weapons (the
head up to the right), lit softly from the top left. build.ps1 -Bundle stages them for the bundle as sprites.
"""
import math
import os

import bmesh
import bpy
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, 'out', 'icons')
SIZE = 256
HEAD_START = 0.83    # HeadsmanPieces.HeadStart


def main():
    os.makedirs(OUT, exist_ok=True)
    axe = next(o for o in bpy.context.scene.objects if o.type == 'MESH')
    _stage()
    _render([axe], 45.0, 'ecp_greataxe_icon.png')
    head = _head(axe)
    axe.hide_render = True
    _render([head], 30.0, 'ecp_greataxe_axehead_icon.png')


def _head(axe):
    """A copy of the axe keeping only the islands above the haft."""
    head = axe.copy()
    head.data = axe.data.copy()
    bpy.context.collection.objects.link(head)
    bm = bmesh.new()
    bm.from_mesh(head.data)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
    low = [f for island in _islands(bm) if sum(v.co.z for f in island for v in f.verts) / sum(len(f.verts) for f in island) <= HEAD_START
           for f in island]
    bmesh.ops.delete(bm, geom=low, context='FACES')
    bm.to_mesh(head.data)
    bm.free()
    return head


def _islands(bm):
    bm.faces.ensure_lookup_table()
    seen, out = set(), []
    for face in bm.faces:
        if face.index in seen:
            continue
        stack, group = [face], []
        seen.add(face.index)
        while stack:
            f = stack.pop()
            group.append(f)
            for edge in f.edges:
                for g in edge.link_faces:
                    if g.index not in seen:
                        seen.add(g.index)
                        stack.append(g)
        out.append(group)
    return out


def _render(parts, turn, name):
    """The parts turned about the view line by `turn` degrees, framed square on by an orthographic camera."""
    pivot = bpy.data.objects.new('icon_turn_' + name, None)
    bpy.context.collection.objects.link(pivot)
    for obj in parts:
        obj.parent = pivot
    pivot.rotation_euler = (0.0, math.radians(turn), 0.0)
    bpy.context.view_layer.update()
    _camera(parts)
    s = bpy.context.scene
    s.render.filepath = os.path.join(OUT, name)
    bpy.ops.render.render(write_still=True)
    print('WORKSHOP icon', s.render.filepath)


def _stage():
    s = bpy.context.scene
    s.render.engine = 'BLENDER_EEVEE'
    s.render.film_transparent = True
    s.render.resolution_x = s.render.resolution_y = SIZE
    s.render.image_settings.color_mode = 'RGBA'
    s.view_settings.view_transform = 'Standard'
    world = bpy.data.worlds.new('icon')
    world.use_nodes = True
    background = next(n for n in world.node_tree.nodes if n.type == 'BACKGROUND')
    background.inputs[0].default_value = (0.5, 0.5, 0.5, 1.0)
    background.inputs[1].default_value = 0.7
    s.world = world
    sun = bpy.data.objects.new('icon_sun', bpy.data.lights.new('icon_sun', 'SUN'))
    sun.data.energy = 3.0
    sun.rotation_euler = (math.radians(70), math.radians(-30), math.radians(-20))
    bpy.context.collection.objects.link(sun)


def _camera(parts):
    """Looking along +Y at the blade's face, framing the parts' bounds with a small margin."""
    points = [o.matrix_world @ Vector(c) for o in parts for c in o.bound_box]
    low = Vector([min(p[i] for p in points) for i in range(3)])
    high = Vector([max(p[i] for p in points) for i in range(3)])
    cam = bpy.data.objects.get('icon_camera') or bpy.data.objects.new('icon_camera', bpy.data.cameras.new('icon_camera'))
    if cam.name not in bpy.context.collection.objects:
        bpy.context.collection.objects.link(cam)
    cam.data.type = 'ORTHO'
    cam.data.ortho_scale = max(high.x - low.x, high.z - low.z) * 1.08
    middle = (low + high) / 2
    cam.location = (middle.x, low.y - 3.0, middle.z)
    cam.rotation_euler = (math.radians(90), 0.0, 0.0)
    bpy.context.scene.camera = cam


main()
