"""Encodes a folder of numbered PNG frames into an H.264 MP4 with Blender's video editor, so no ffmpeg install is needed.

    blender --background --factory-startup --python blender/encode.py -- <frames folder> <out.mp4> [fps]
"""
import os
import sys

import bpy

args = sys.argv[sys.argv.index("--") + 1:]
folder, out = os.path.abspath(args[0]), os.path.abspath(args[1])
fps = int(args[2]) if len(args) > 2 else 30
files = sorted(f for f in os.listdir(folder) if f.lower().endswith(".png"))

scene = bpy.context.scene
editor = scene.sequence_editor_create()
strips = editor.strips if hasattr(editor, "strips") else editor.sequences
strip = strips.new_image("frames", os.path.join(folder, files[0]), 1, 1)
for name in files[1:]:
    strip.elements.append(name)

first = bpy.data.images.load(os.path.join(folder, files[0]))
scene.render.resolution_x, scene.render.resolution_y = first.size
scene.render.resolution_percentage = 100
scene.render.fps = fps
scene.frame_start, scene.frame_end = 1, len(files)
settings = scene.render.image_settings
settings.media_type, settings.file_format = 'VIDEO', 'FFMPEG'
scene.render.ffmpeg.format, scene.render.ffmpeg.codec = 'MPEG4', 'H264'
scene.render.ffmpeg.constant_rate_factor = 'HIGH'
scene.render.filepath = out
bpy.ops.render.render(animation=True)
print("WORKSHOP encoded", len(files), "frames to", out, flush=True)
