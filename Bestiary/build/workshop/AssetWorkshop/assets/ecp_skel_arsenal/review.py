"""Close-up review sheet for built arsenal assets, with the baked textures, tighter than preview.png:

    blender --background --factory-startup --python assets/ecp_skel_arsenal/review.py -- <out png> <asset>[:<game prefab>] ...

For each asset (its assets/<name>/out/<name>.blend, written by build.ps1) renders a three-quarter view, the flat from
above (Blender +Z), the side and a view from below, one row per asset. With a game prefab after a colon (a path under
the reference export's Assets, e.g. Characters/Skeleton/weapons/skeleton_sword.prefab), the game's weapon is drawn
beside ours with its 'attach' frame on our origin (the fist), GAP metres across, so size and grip compare directly.
"""
import os
import sys

import bpy
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "..", "blender"))
from workshop import prefab, preview  # noqa: E402

TILE = 480
GAP = 0.3
VIEWS = (("three_quarter", Vector((0.9, -0.7, 0.9)), False), ("above", Vector((0.0, 0.0, 1.0)), True),
         ("side", Vector((1.0, 0.0, 0.08)), True), ("below", Vector((0.25, 0.3, -1.0)), False))


def main():
    argv = sys.argv[sys.argv.index("--") + 1:]
    out, assets = argv[0], argv[1:]
    tiles = []
    for asset in assets:
        name, _, game = asset.partition(":")
        tiles += _asset(name, game, os.path.dirname(out))
    preview.grid(tiles, len(VIEWS), out)
    print("WORKSHOP review", out)


def _asset(asset, game, folder):
    bpy.ops.wm.open_mainfile(filepath=os.path.join(HERE, "..", asset, "out", asset + ".blend"))
    target = next(o for o in bpy.context.scene.objects if o.type == 'MESH' and not o.name.startswith("col_")
                  and not o.name.startswith("preview_"))
    for obj in bpy.context.scene.objects:
        if obj is not target and obj.type == 'MESH':
            obj.hide_render = True
    framed = [target] + (_game(game) if game else [])
    s = bpy.context.scene
    _light(s)
    s.render.resolution_x = s.render.resolution_y = TILE
    s.render.film_transparent = False
    camera = s.camera or preview._camera()
    paths = []
    for name, direction, ortho in VIEWS:
        path = os.path.join(folder, f"review_{asset}_{name}.png")
        preview._render(camera, framed, direction, ortho, path)
        paths.append(path)
    return paths


def _light(s):
    if not any(o.type == 'LIGHT' for o in s.objects):
        s.render.engine = 'BLENDER_EEVEE'
        s.eevee.taa_render_samples = 32
        s.view_settings.view_transform = 'Standard'
        preview._world(s)
        preview._sun()


def _game(path):
    """The game's weapon with its attach frame on our origin, moved GAP across."""
    root = prefab.load(path)
    attach = next(o for o in root.children_recursive if o.name.split(".")[0] == "attach")
    bpy.context.view_layer.update()
    root.matrix_world = Matrix.Translation((GAP, 0.0, 0.0)) @ attach.matrix_world.inverted() @ root.matrix_world
    bpy.context.view_layer.update()
    return [o for o in attach.children_recursive if o.type == 'MESH']


main()
