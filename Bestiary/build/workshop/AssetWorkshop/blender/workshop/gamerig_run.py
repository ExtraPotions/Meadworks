"""Command line for a body on a game skeleton (route b; gamerig_build.py has the steps):

    blender --background --factory-startup --python-exit-code 1 --python blender/workshop/gamerig_run.py --
            --asset assets/<name> [--no-preview]

Builds the asset's model.py on its game skeleton, bakes the atlas and writes out/<name>.json (the contract Unity's
Workshop.GameRig reads), the PNGs, <name>.blend and, unless --no-preview, out/preview.png.
"""
import argparse
import os
import sys

sys.dont_write_bytecode = True
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from workshop import gamerig_build  # noqa: E402  (needs the path above)


def _arguments():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    parser = argparse.ArgumentParser(prog="gamerig_run.py")
    parser.add_argument("--asset", required=True, help="asset folder containing model.py")
    parser.add_argument("--no-preview", action="store_true", help="skip the preview render")
    return parser.parse_args(argv)


if __name__ == "__main__":
    arguments = _arguments()
    gamerig_build.run(os.path.abspath(arguments.asset), render_preview=not arguments.no_preview)
