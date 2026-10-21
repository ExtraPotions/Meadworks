# Bestiary: run a pinned workshop Blender script without the game reference export.
# Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
"""Blender entry: runs a pinned AssetWorkshop script with `workshop.reference._texture` replaced so that a material
on the game's own textures gets an empty 1 x 1 placeholder image instead of a file read from the local game export
(`%USERPROFILE%/ValheimReference`, made by `rip-reference.ps1`), which Meadworks never keeps (design 10.1 rule 7).

    blender --background --factory-startup --python-exit-code 1 --python tools/assets/no_reference.py -- <script> [args]

Only the texture lookup changes. The material still records the game texture's path ("reference_albedo",
"reference_normal"), which is all the export writes for it: at runtime the mod borrows the game's own texture by that
path's material, and no game pixel is read, baked or exported. Geometry, rig, UVs and clips are untouched; the
mimic's output is checked against EliteCreaturesPack's bundle (tools/assets/ecp_bundles.py compare).
"""
import os
import runpy
import sys

argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
if not argv:
    raise SystemExit('usage: ... --python no_reference.py -- <workshop script> [args]')
script = os.path.abspath(argv[0])
workshop_blender = os.path.join(os.path.dirname(script), '..', '..', 'blender')
sys.path.insert(0, os.path.abspath(workshop_blender))

import bpy  # noqa: E402
from workshop import reference  # noqa: E402


def _placeholder_texture(tree, path, data):
    node = tree.nodes.new('ShaderNodeTexImage')
    name = 'reference:' + path
    image = bpy.data.images.get(name) or bpy.data.images.new(name, 1, 1)
    node.image = image
    node.interpolation = 'Closest'
    if data:
        image.colorspace_settings.name = 'Non-Color'
    return node


reference._texture = _placeholder_texture
print('BESTIARY no_reference: game texture lookups replaced by placeholders for', script, flush=True)
sys.argv = [script, '--'] + argv[1:]
runpy.run_path(script, run_name='__main__')
