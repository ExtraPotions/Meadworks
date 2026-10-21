"""Quick Blender stills of the kraken's pieces (Eevee), for the modelling loop. The finished previews come from Unity,
where the prefabs themselves are posed (unity/Assets/Editor/Kraken/KrakenPreview.cs).
"""
import math
import os

import bpy
from mathutils import Vector

from geo import U
from workshop import preview

WIDTH, HEIGHT = 960, 720


def _stage():
    s = bpy.context.scene
    if s.camera is not None:
        return s.camera
    s.render.engine = 'BLENDER_EEVEE'
    s.eevee.taa_render_samples = 32
    s.render.resolution_x, s.render.resolution_y = WIDTH, HEIGHT
    s.view_settings.view_transform = 'Standard'
    s.world = bpy.data.worlds.new("kraken_views")
    background = next(n for n in s.world.node_tree.nodes if n.bl_idname == 'ShaderNodeBackground')
    background.inputs['Color'].default_value = (0.36, 0.42, 0.48, 1.0)
    background.inputs['Strength'].default_value = 0.7
    light = bpy.data.lights.new("views_sun", 'SUN')
    light.energy = 3.0
    sun = bpy.data.objects.new("views_sun", light)
    s.collection.objects.link(sun)
    sun.rotation_euler = (math.radians(50), 0.0, math.radians(-150))
    camera = bpy.data.objects.new("views_camera", bpy.data.cameras.new("views_camera"))
    s.collection.objects.link(camera)
    s.camera = camera
    camera.data.clip_end = 500
    return camera


def _shot(path, eye, target, lens=None, ortho=None):
    camera = _stage()
    camera.data.type = 'ORTHO' if ortho else 'PERSP'
    if ortho:
        camera.data.ortho_scale = ortho
    else:
        camera.data.lens = lens or 50
    camera.location = eye
    camera.rotation_euler = (Vector(target) - Vector(eye)).to_track_quat('-Z', 'Y').to_euler()
    bpy.context.scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    return path


def tentacle(obj, out):
    """Side and underside of the whole length, and the base half from below and to the side."""
    mid = U(0, 0, 6.4)
    shots = [_shot(os.path.join(out, "view_side.png"), mid + U(-25, 0, 0), mid, ortho=13.5),
             _shot(os.path.join(out, "view_below.png"), mid + U(0, -25, 0.01), mid, ortho=13.5),
             _shot(os.path.join(out, "view_base_below.png"), U(-2.2, -2.4, -1.5), U(0, 0, 2.5), lens=30),
             _shot(os.path.join(out, "view_tip.png"), U(-1.5, -1.0, 12.5), U(0, 0, 10.8), lens=35)]
    preview.grid(shots, 2, os.path.join(out, "preview.png"))


def head(obj, out):
    """Front, three-quarter, side and back; the whole head framed with its column."""
    centre = U(0, 0.8, -0.4)
    shots = [_shot(os.path.join(out, "view_front.png"), centre + U(0, 0.6, 23), centre, lens=45),
             _shot(os.path.join(out, "view_three_quarter.png"), centre + U(15, 3, 17), centre, lens=45),
             _shot(os.path.join(out, "view_side.png"), centre + U(23, 0.5, 0), centre, lens=45),
             _shot(os.path.join(out, "view_back.png"), centre + U(-13, 5, -17), centre, lens=45)]
    preview.grid(shots, 2, os.path.join(out, "preview.png"))


def head_open(obj, out):
    """The face close up with the jaws open."""
    face = U(0, 1.6, 0.9)
    shots = [_shot(os.path.join(out, "view_face_open.png"), face + U(1.8, 0.6, 4.2), face, lens=40),
             _shot(os.path.join(out, "view_face_open_front.png"), face + U(0, 0.2, 4.5), face, lens=40)]
    preview.grid(shots, 2, os.path.join(out, "preview_open.png"))
