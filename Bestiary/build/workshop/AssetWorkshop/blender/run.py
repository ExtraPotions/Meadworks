"""Blender entry point for one asset. build.ps1 calls it; by hand:

    blender --background --factory-startup --python-exit-code 1 --python run.py -- --asset ../assets/test_crate

Builds the asset's model.py, bakes its textures, exports FBX + PNGs + manifest into the asset's out/ folder and,
unless --no-preview, renders out/preview.png.
"""
import argparse
import os
import sys

sys.dont_write_bytecode = True
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from workshop import pipeline  # noqa: E402  (needs the path above)


def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    parser = argparse.ArgumentParser(prog="run.py")
    parser.add_argument("--asset", required=True, help="asset folder containing model.py")
    parser.add_argument("--no-preview", action="store_true", help="skip the preview render")
    return parser.parse_args(argv)


args = parse_args()
pipeline.run(os.path.abspath(args.asset), render_preview=not args.no_preview)
