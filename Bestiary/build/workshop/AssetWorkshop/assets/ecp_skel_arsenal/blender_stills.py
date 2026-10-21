"""Stills from the showcase .blend, to check it without opening Blender's window:

    blender --background <bake>/skeleton_arsenal.blend --python assets/ecp_skel_arsenal/blender_stills.py -- <out png> [offsets]

For each shot marker (the row, each weapon, the vertebra) renders the frames at the given offsets after the marker
(default 12 25 38 50: the hold before the attack, then three moments of it) through that marker's camera, and tiles
them one row per shot into <out png>.
"""
import os
import sys

import bpy

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, '..', '..', 'blender'))
from workshop import preview  # noqa: E402


def main():
    argv = sys.argv[sys.argv.index('--') + 1:]
    out = argv[0]
    offsets = [int(a) for a in argv[1:]] or [12, 25, 38, 50]
    s = bpy.context.scene
    s.render.engine = 'BLENDER_EEVEE'
    s.eevee.taa_render_samples = 16
    s.render.resolution_x, s.render.resolution_y = 640, 360
    s.view_settings.view_transform = 'Standard'
    tiles = []
    for marker in sorted(s.timeline_markers, key=lambda m: m.frame):
        for offset in offsets:
            s.frame_set(marker.frame + offset)
            s.camera = marker.camera
            s.render.filepath = os.path.join(os.path.dirname(out), f"still_{marker.name}_{offset}.png")
            bpy.ops.render.render(write_still=True)
            tiles.append(s.render.filepath)
    preview.grid(tiles, len(offsets), out)
    print("WORKSHOP stills", out)


main()
