"""Renders out/preview.png, four views in one sheet so a single look shows the asset:

    top left: three-quarter from the front right     top right: front, orthographic
    bottom left: right side, orthographic            bottom right: beside a 1.8 m figure (a Valheim player)

Each view is also kept as out/view_<name>.png.
"""
import math
import os

import bpy
import numpy as np
from mathutils import Vector

from . import export, materials, scene, shapes

TILE = 640
VIEWS = (
    ("three_quarter", Vector((1.0, -1.3, 0.8)), False),
    ("front", Vector((0.0, -1.0, 0.0)), True),
    ("side", Vector((1.0, 0.0, 0.0)), True),
    ("scale", Vector((1.2, -1.8, 0.6)), False),
)


def render_sheet(target, out_dir):
    _stage(target)
    camera = _camera()
    figure = _figure(target)
    tiles = []
    for name, direction, ortho in VIEWS:
        framed = [target] + figure if name == "scale" else [target]
        for part in figure:
            part.hide_render = name != "scale"
        tiles.append(_render(camera, framed, direction, ortho, os.path.join(out_dir, f"view_{name}.png")))
    path = os.path.join(out_dir, "preview.png")
    _sheet(tiles, path)
    return path


def _stage(target):
    s = bpy.context.scene
    s.render.engine = 'BLENDER_EEVEE'
    s.eevee.taa_render_samples = 32
    s.render.resolution_x = s.render.resolution_y = TILE
    s.render.resolution_percentage = 100
    s.render.film_transparent = False
    s.view_settings.view_transform = 'Standard'
    _world(s)
    _sun()
    low, _ = scene.bounds([target])
    bpy.ops.mesh.primitive_plane_add(size=60, location=(0, 0, low.z))
    bpy.context.active_object.data.materials.append(materials.flat("preview_ground", (0.16, 0.18, 0.13)))


def _world(s):
    s.world = bpy.data.worlds.new("preview")
    if s.world.node_tree is None:
        s.world.use_nodes = True
    background = next(n for n in s.world.node_tree.nodes if n.bl_idname == 'ShaderNodeBackground')
    background.inputs['Color'].default_value = (0.45, 0.52, 0.62, 1.0)
    background.inputs['Strength'].default_value = 0.6


def _sun():
    light = bpy.data.lights.new("preview_sun", 'SUN')
    light.energy = 2.5
    light.angle = math.radians(3)
    sun = bpy.data.objects.new("preview_sun", light)
    bpy.context.scene.collection.objects.link(sun)
    sun.rotation_euler = (math.radians(50), 0.0, math.radians(35))


def _camera():
    camera = bpy.data.objects.new("preview_camera", bpy.data.cameras.new("preview_camera"))
    bpy.context.scene.collection.objects.link(camera)
    bpy.context.scene.camera = camera
    camera.data.lens = 50
    camera.data.clip_end = 2000
    return camera


def _figure(target):
    """A plain 1.8 m stand-in for the player, to the right of the asset."""
    low, high = scene.bounds([target])
    x, y = high.x + 0.5, (low.y + high.y) / 2
    skin = materials.flat("preview_figure", (0.25, 0.3, 0.4))
    body = shapes.cylinder("preview_body", 0.2, 1.5, (x, y, low.z + 0.75), material=skin, vertices=16)
    head = shapes.sphere("preview_head", 0.15, (x, y, low.z + 1.65), material=skin)
    return [body, head]


def _render(camera, framed, direction, ortho, path):
    low, high = scene.bounds(framed)
    center, radius = (low + high) / 2, max((high - low).length / 2, 0.05)
    direction = direction.normalized()
    camera.data.type = 'ORTHO' if ortho else 'PERSP'
    camera.data.ortho_scale = radius * 2.1
    distance = radius * 4 if ortho else radius / math.sin(camera.data.angle / 2) * 1.05
    camera.data.clip_start = distance * 0.01
    camera.location = center + direction * distance
    camera.rotation_euler = (-direction).to_track_quat('-Z', 'Y').to_euler()
    bpy.context.scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    return path


def _sheet(paths, out):
    grid(paths, 2, out)


def grid(paths, columns, out):
    """Tiles same-sized images left to right, top to bottom, into one PNG."""
    tiles = [_pixels(p) for p in paths]
    blank = np.zeros_like(tiles[0])
    tiles += [blank] * (-len(tiles) % columns)
    rows = [np.hstack(tiles[i:i + columns]) for i in range(0, len(tiles), columns)]
    sheet = np.vstack(list(reversed(rows)))  # Blender stores rows bottom-up
    image = bpy.data.images.new("preview_sheet", sheet.shape[1], sheet.shape[0])
    image.pixels.foreach_set(sheet.ravel())
    export.png(image, out)


def _pixels(path):
    image = bpy.data.images.load(path)
    buffer = np.empty(len(image.pixels), np.float32)
    image.pixels.foreach_get(buffer)
    return buffer.reshape(image.size[1], image.size[0], 4)
