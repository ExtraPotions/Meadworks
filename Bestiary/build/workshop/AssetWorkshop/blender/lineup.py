"""Lineup of a built asset beside the game's own prefabs: its codex category's references (codex/data), or those named.
Run after a build (build.ps1 -Lineup does):

    blender --background --factory-startup --python blender/lineup.py --
        --asset <name or folder> [--category <key>] [--refs <prefab>,...]

--refs replaces the category's references; with a leading + (--refs +Skeleton) it adds to them. A reference is a
prefab path under the reference export's Assets folder or a bare prefab name (ShieldBanded). The category defaults to
the model's CATEGORY. Writes <asset>/out/lineup/front.png, turn.png, close.png and lineup.blend. To look round it:

    blender <asset>/out/lineup/lineup.blend --python blender/lineup.py -- --open

which shows every 3D view in material preview with the scene's light, through the three-quarter camera
('lineup_turn'; numpad 0 goes back to it; 'lineup_front' and 'lineup_close' are in the outliner).
"""
import argparse
import glob
import json
import os
import sys

sys.dont_write_bytecode = True
HERE = os.path.dirname(os.path.abspath(__file__))
WORKSHOP = os.path.dirname(HERE)
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(WORKSHOP, "codex", "tools"))

import bpy  # noqa: E402
import codexdata  # noqa: E402  (codex/tools, plain Python)
from workshop import lineup, unity  # noqa: E402

MAX_REFERENCES = 8


def parse():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    parser = argparse.ArgumentParser(prog="lineup.py")
    parser.add_argument("--asset", help="asset name under assets/, or an asset folder")
    parser.add_argument("--category", help="codex category key (default: the model's CATEGORY)")
    parser.add_argument("--refs", default="", help="prefabs, comma separated; a leading + adds to the category's")
    parser.add_argument("--open", action="store_true", help="set up the window of an opened lineup.blend")
    return parser.parse_args(argv)


def asset_folder(asset):
    return os.path.abspath(asset) if os.path.isdir(asset) else os.path.join(WORKSHOP, "assets", asset)


def references(folder, key, refs):
    """The prefab paths to line up with: the category's references and/or --refs, resolved, at most MAX_REFERENCES."""
    extra = [os.path.join(folder, "fixture")] if os.path.isdir(os.path.join(folder, "fixture")) else []
    found = (codexdata.category(key, extra) or {}).get("references", []) if key else []
    named = [r.strip() for r in refs.lstrip("+").split(",") if r.strip()]
    chosen = (found + named) if refs.startswith("+") or not named else named
    if key and not found:
        print(f"WORKSHOP lineup: the codex has no references for {key} yet", flush=True)
    paths = [resolve(r) for r in chosen]
    return [p for p in paths if p][:MAX_REFERENCES]


def resolve(ref):
    """A prefab path under the export for a path or a bare name (the shortest path wins), or None."""
    if ref.endswith(".prefab") and os.path.exists(os.path.join(unity.ROOT, ref)):
        return ref
    stem = os.path.splitext(os.path.basename(ref))[0] + ".prefab"
    try:
        with open(unity.GUID_INDEX, encoding="utf-8") as handle:
            paths = [p for p in json.load(handle).values() if p.endswith("/" + stem) or p == stem]
    except (OSError, ValueError):
        paths = [os.path.relpath(p, unity.ROOT).replace("\\", "/")
                 for p in glob.glob(os.path.join(unity.ROOT, "**", stem), recursive=True)]
    if not paths:
        print(f"WORKSHOP lineup: no prefab {ref} in the reference export", flush=True)
    return min(paths, key=len) if paths else None


def build(args):
    folder = asset_folder(args.asset)
    name = os.path.basename(os.path.normpath(folder))
    out = os.path.join(folder, "out")
    with open(os.path.join(out, name + ".json"), encoding="utf-8") as handle:
        key = args.category or json.load(handle).get("category")
    paths = references(folder, key, args.refs)
    entries = [lineup.asset_entry(os.path.join(out, name + ".blend"), name, lineup.is_item_category(key))]
    entries += [lineup.reference_entry(p) for p in paths]
    lineup.run(entries, os.path.join(out, "lineup"))
    print(f"WORKSHOP lineup: {name} beside {', '.join(e['label'] for e in entries[1:]) or 'nothing'}", flush=True)
    print(f"WORKSHOP lineup: {os.path.join(out, 'lineup')}", flush=True)


def look():
    """Material preview with the scene's light and world, through the three-quarter camera."""
    manager = bpy.context.window_manager
    window = manager.windows[0] if manager.windows else None
    if window is None:
        return 0.5
    bpy.context.scene.camera = bpy.data.objects.get('lineup_turn') or bpy.context.scene.camera
    for area in window.screen.areas:
        if area.type == 'VIEW_3D':
            space = area.spaces.active
            space.shading.type = 'MATERIAL'
            space.shading.use_scene_lights = True
            space.shading.use_scene_world = True
            space.overlay.show_extras = False
            space.region_3d.view_perspective = 'CAMERA'
    return None


arguments = parse()
if arguments.open:
    bpy.app.timers.register(look, first_interval=1.0)
elif not arguments.asset:
    raise SystemExit("lineup.py: --asset <name or folder> (or --open inside an opened lineup.blend)")
else:
    build(arguments)
