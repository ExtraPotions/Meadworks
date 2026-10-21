"""Builds the crypt mimic scene: the game's chest on our rig, teeth, tongue and eyes, and the staged fight.

    blender --background --factory-startup --python-exit-code 1 --python assets/crypt_mimic/build.py -- [--render]

Always saves out/crypt_mimic.blend (open it in Blender and press Space to play). With --render it also writes
out/showreel.mp4, out/showreel_sheet.png (16 frames) and out/poses.png (four close-ups).
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.dont_write_bytecode = True
sys.path[:0] = [HERE, os.path.join(HERE, "..", "..", "blender")]

import bpy  # noqa: E402

import parts  # noqa: E402
import rig as mimic_rig  # noqa: E402
import showreel  # noqa: E402
import stage  # noqa: E402
from workshop import preview, scene  # noqa: E402

OUT = os.path.join(HERE, "out")
CLOSE_FRAMES = (20, 90, 182, 214)   # dormant, awake, wind-up, recovery
SHEET_FRAMES = (20, 42, 50, 90, 125, 130, 176, 185, 194, 197, 206, 214, 224, 236, 256, 300)


def build():
    scene.clear()
    rig = mimic_rig.armature()
    mimic_rig.chest(rig)
    parts.build(rig)
    stage.build()
    player, shoulder = stage.player()
    wide = stage.camera("camera_wide", (7.6, -8.2, 4.4), (0.3, -2.1, 0.35), 40)
    close = stage.camera("camera_close", (2.7, -2.7, 1.55), (0.0, -0.1, 0.42), 45)
    mimic_rig.attach(close, rig, "root")
    showreel.build(rig, player, shoulder)
    bpy.context.scene.camera = wide
    stage.viewport()
    return wide, close


def _render_settings(width, height, samples=24):
    render = bpy.context.scene.render
    render.engine = 'BLENDER_EEVEE'
    bpy.context.scene.eevee.taa_render_samples = samples
    render.resolution_x, render.resolution_y, render.resolution_percentage = width, height, 100
    render.image_settings.media_type = 'IMAGE'
    render.image_settings.file_format = 'PNG'
    render.use_stamp = False


def _stills(camera, frames, size, prefix):
    s = bpy.context.scene
    s.camera = camera
    _render_settings(*size)
    paths = []
    for frame in frames:
        s.frame_set(frame)
        s.render.filepath = os.path.join(OUT, f"{prefix}_{frame:03d}.png")
        bpy.ops.render.render(write_still=True)
        paths.append(s.render.filepath)
    return paths


def _video(camera):
    s = bpy.context.scene
    s.camera = camera
    _render_settings(1280, 720)
    _captions(s.render)
    settings = s.render.image_settings
    settings.media_type, settings.file_format = 'VIDEO', 'FFMPEG'
    s.render.ffmpeg.format, s.render.ffmpeg.codec = 'MPEG4', 'H264'
    s.render.ffmpeg.constant_rate_factor = 'HIGH'
    s.render.filepath = os.path.join(OUT, "showreel.mp4")
    bpy.ops.render.render(animation=True)


def _captions(render):
    """Burns the current beat's name into the bottom of each frame."""
    for name in dir(render):
        if name.startswith("use_stamp_"):
            setattr(render, name, False)
    render.use_stamp, render.use_stamp_note = True, True
    render.stamp_font_size = 26
    render.stamp_background = (0.0, 0.0, 0.0, 0.55)
    bpy.app.handlers.frame_change_pre.append(
        lambda s, *_: setattr(s.render, "stamp_note_text", showreel.caption(s.frame_current)))


def main():
    os.makedirs(OUT, exist_ok=True)
    wide, close = build()
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "crypt_mimic.blend"))
    print("WORKSHOP saved", os.path.join(OUT, "crypt_mimic.blend"), flush=True)
    if "--render" in sys.argv:
        preview.grid(_stills(close, CLOSE_FRAMES, (800, 600), "pose"), 2, os.path.join(OUT, "poses.png"))
        preview.grid(_stills(wide, SHEET_FRAMES, (480, 270), "beat"), 4, os.path.join(OUT, "showreel_sheet.png"))
        print("WORKSHOP sheets written", flush=True)
        if "--no-video" not in sys.argv:
            _video(wide)
            print("WORKSHOP video", os.path.join(OUT, "showreel.mp4"), flush=True)


main()
