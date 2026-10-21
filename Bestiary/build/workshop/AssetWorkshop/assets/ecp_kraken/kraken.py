"""Blender entry for the kraken: builds, bakes and exports the tentacle or the head, with quick preview stills.

    blender --background --factory-startup --python-exit-code 1 --python assets/ecp_kraken/kraken.py -- --part tentacle
    blender ... -- --part head [--no-preview]

Writes out/<part>/: <asset>.json (mesh, weights, rig, markers in Unity's axes), the baked PNGs, <asset>.blend and
view_*.png. assets/ecp_kraken/build.ps1 runs both, then Unity (unity/Assets/Editor/Kraken) builds the prefabs.
"""
import argparse
import math
import os
import sys
import time

HERE = os.path.dirname(os.path.abspath(__file__))
sys.dont_write_bytecode = True
sys.path[:0] = [HERE, os.path.join(HERE, "..", "..", "blender")]

import bpy  # noqa: E402
from mathutils import Matrix, Vector  # noqa: E402

import bake_export as bx  # noqa: E402
import geo  # noqa: E402
import head  # noqa: E402
import head_parts  # noqa: E402
import paint  # noqa: E402
import tentacle  # noqa: E402
import views  # noqa: E402
from workshop import scene  # noqa: E402

OUT = os.path.join(HERE, "out")
TEXTURES = os.path.join(OUT, "textures")
BEAK_BAKE_OPEN = 30.0           # the jaws are baked open, so their insides get their own paint
UV_SCALE = {"beak_upper": 2.2, "beak_lower": 2.2, "lips": 1.5, "siphon": 1.7, "lid_kh_eye_l": 1.8, "lid_kh_eye_r": 1.8,
            "barnacles": 1.4, "throat": 0.8}


def log(key, value):
    print(f"WORKSHOP {key}: {value}", flush=True)


def build_tentacle(out, render):
    low_parts, high_parts, bones, markers, notes = tentacle.build()
    low = geo.join([geo.to_object(p) for p in low_parts], "ecp_kraken_tentacle")
    high = geo.join([geo.to_object(p, p.name + "_hi") for p in high_parts], "tentacle_bake")
    high.data.materials.append(paint.material("tentacle_paint", head=False))
    bx.triangulate(low)
    bx.unwrap_strips(low)
    albedo, normal = bx.bake(low, high, "ecp_kraken_tentacle", 1024, 512, 0.05, 0.12)
    textures = _save(out, "ecp_kraken_tentacle", albedo, normal, 0.25)
    bx.final_material(low, "ecp_kraken_tentacle", albedo, normal)
    high.hide_render = high.hide_viewport = True
    names = [b[0] for b in bones]
    parts = [bx.mesh_json(low, "mesh_tentacle", "ecp_kraken_tentacle", names, textures)]
    notes.update(triangles=geo.triangles(low), outward=round(geo.check_outward(low), 2))
    bx.write(out, "ecp_kraken_tentacle", bones, markers, parts, notes)
    for key, value in notes.items():
        log(key, value)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(out, "ecp_kraken_tentacle.blend"))
    if render:
        views.tentacle(low, out)


def build_head(out, render):
    low_parts, high_parts, eye_parts, bones, markers = head.build()
    for part in low_parts + high_parts:
        part.uv_scale = UV_SCALE.get(part.name, 0.55 if part.name.startswith("root_") else 1.0)
    low = geo.join([geo.to_object(p) for p in low_parts], "ecp_kraken_head")
    high = geo.join([geo.to_object(p, p.name + "_hi") for p in high_parts], "head_bake")
    high.data.materials.append(paint.material("head_paint", head=True))
    eyes = geo.join([_eye_object(p) for p in eye_parts], "ecp_kraken_eyes")
    bx.triangulate(low)
    bx.triangulate(eyes)
    bx.unwrap_smart(low)
    closed = bx.pose(low, _jaws(BEAK_BAKE_OPEN))
    bx.pose(high, _jaws(BEAK_BAKE_OPEN))
    albedo, normal = bx.bake(low, high, "ecp_kraken_skin", 1024, 512, 0.025, 0.07)
    bx._set_positions(low, closed)
    skin = _save(out, "ecp_kraken_skin", albedo, normal, 0.2)
    bx.final_material(low, "ecp_kraken_skin", albedo, normal)
    eye_image = bpy.data.images.load(os.path.join(TEXTURES, "ecp_kraken_eye_albedo.png"))
    bx.final_material(eyes, "ecp_kraken_eye", eye_image)
    high.hide_render = high.hide_viewport = True
    names = [b[0] for b in bones]
    eye_textures = {"albedo": "ecp_kraken_eye_albedo.png", "normal": "", "textureSize": 256, "normalSize": 0,
                    "smoothness": 0.65}
    parts = [bx.mesh_json(low, "mesh_skin", "ecp_kraken_skin", names, skin),
             bx.mesh_json(eyes, "mesh_eyes", "ecp_kraken_eye", names, eye_textures)]
    notes = {"triangles": geo.triangles(low), "eye triangles": geo.triangles(eyes),
             "outward": round(geo.check_outward(low), 2)}
    bx.write(out, "ecp_kraken_head", bones, markers, parts, notes)
    for key, value in notes.items():
        log(key, value)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(out, "ecp_kraken_head.blend"))
    if render:
        views.head(low, out)
        bx.pose(low, _jaws(35.0))
        views.head_open(low, out)
        bx._set_positions(low, closed)


def _jaws(degrees):
    """Blender matrices opening the beak: the upper jaw up, the lower down, about the hinge on the head's X axis."""
    hinge = geo.U(*head_parts.HINGE)
    around = lambda a: Matrix.Translation(hinge) @ Matrix.Rotation(math.radians(a), 4, 'X') @ Matrix.Translation(-hinge)
    return {"kh_beak_upper": around(-degrees), "kh_beak_lower": around(degrees)}


def _eye_object(part):
    """An eyeball with planar UVs along its gaze: u to the viewer's right, v up, the pupil in the middle."""
    obj = geo.to_object(part)
    centre, s, n, r = part.uv_frame
    layer = obj.data.uv_layers.new(name="UVMap")
    for loop in obj.data.loops:
        p = obj.data.vertices[loop.vertex_index].co - centre
        layer.data[loop.index].uv = (0.5 + 0.5 * p.dot(s) / r, 0.5 + 0.5 * p.dot(n) / r)
    return obj


def _save(out, name, albedo, normal, smoothness):
    bx.save_png(albedo, os.path.join(out, name + "_albedo.png"))
    bx.save_png(normal, os.path.join(out, name + "_normal.png"))
    return {"albedo": name + "_albedo.png", "normal": name + "_normal.png", "textureSize": albedo.size[0],
            "normalSize": normal.size[0], "smoothness": smoothness}


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    parser = argparse.ArgumentParser(prog="kraken.py")
    parser.add_argument("--part", choices=("tentacle", "head"), required=True)
    parser.add_argument("--no-preview", action="store_true")
    args = parser.parse_args(argv)
    started = time.time()
    out = os.path.join(OUT, args.part)
    os.makedirs(out, exist_ok=True)
    scene.clear()
    (build_tentacle if args.part == "tentacle" else build_head)(out, not args.no_preview)
    log("seconds", round(time.time() - started, 1))


main()
