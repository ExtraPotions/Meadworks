"""Loads the game's longship with workshop.prefab and renders it, to check the prefab loader by eye:

    blender --background --factory-startup --python blender/prefab_test.py

Writes out/prefab_test/longship_three_quarter.png, longship_side.png and longship.blend, and prints the ship's bounds,
which way its bow points and the heights of keel, deck and rail against the prefab origin.
"""
import os
import sys
import time

sys.dont_write_bytecode = True
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bpy  # noqa: E402
from mathutils import Vector  # noqa: E402

from workshop import materials, prefab, preview, scene  # noqa: E402

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "out", "prefab_test")
SHIP = "GameElements/Ships/VikingShip.prefab"


def main():
    scene.clear()
    started = time.perf_counter()
    root = prefab.load(SHIP, root_name="longship")
    print(f"prefab_test: loaded {len(prefab.meshes(root))} meshes in {time.perf_counter() - started:.2f} s")
    report(root)
    os.makedirs(OUT, exist_ok=True)
    render(root)
    bpy.context.preferences.filepaths.save_version = 0   # no longship.blend1 beside it
    bpy.ops.wm.save_as_mainfile(filepath=os.path.abspath(os.path.join(OUT, "longship.blend")))


def report(root):
    low, high = prefab.bounds(root)
    print(f"prefab_test: whole ship min {_v(low)} max {_v(high)} size {_v(high - low)}")
    hull = _part(root, "hull")
    hull_low, hull_high = prefab.bounds([hull])
    print(f"prefab_test: hull min {_v(hull_low)} max {_v(hull_high)} size {_v(hull_high - hull_low)}")
    head_low, head_high = prefab.bounds([_part(root, "skull_head")])
    print(f"prefab_test: figurehead (bow) from y {head_low.y:+.2f} to {head_high.y:+.2f}; stern post at y "
          f"{hull_high.y if head_low.y < 0 else hull_low.y:+.2f}")
    print(f"prefab_test: keel {hull_low.z:+.2f}, deck floor {_deck(hull):+.2f}, rail midships {_rail(hull):+.2f}, "
          f"rudder blade bottom {low.z:+.2f}, masthead {high.z:+.2f} (z, metres above the prefab origin)")


def _part(root, name):
    return next(o for o in prefab.meshes(root) if o.name == name)


def _deck(hull):
    """The deck floor: the lowest of the hull's surfaces hit straight down at a few spots (thwarts sit higher)."""
    into_hull = hull.matrix_world.inverted()
    heights = []
    for x, y in ((1.0, 0.8), (-1.0, -0.8), (0.0, 1.6), (0.0, -1.6), (0.6, 2.5), (-0.6, -4.0), (0.5, 5.0)):
        hit, where, *_ = hull.ray_cast(into_hull @ Vector((x, y, 20.0)), into_hull.to_3x3() @ Vector((0, 0, -1)))
        if hit:
            heights.append((hull.matrix_world @ where).z)
    return min(heights) if heights else float("nan")


def _rail(hull):
    """Highest hull vertex within a metre of midships."""
    points = [hull.matrix_world @ v.co for v in hull.data.vertices]
    return max(p.z for p in points if abs(p.y) < 1.0)


def render(root):
    s = bpy.context.scene
    s.render.engine = 'BLENDER_EEVEE'
    s.eevee.taa_render_samples = 32
    s.render.resolution_x, s.render.resolution_y = 1280, 800
    s.view_settings.view_transform = 'Standard'
    preview._world(s)
    preview._sun()
    parts = prefab.meshes(root)
    low, _ = prefab.bounds(root)
    bpy.ops.mesh.primitive_plane_add(size=80, location=(0, 0, low.z))
    bpy.context.active_object.data.materials.append(materials.flat("ground", (0.16, 0.18, 0.13)))
    camera = preview._camera()
    preview._render(camera, parts, Vector((1.0, -1.3, 0.55)), False, os.path.join(OUT, "longship_three_quarter.png"))
    preview._render(camera, parts, Vector((1.0, 0.0, 0.0)), True, os.path.join(OUT, "longship_side.png"))


def _v(vector):
    return "(" + ", ".join(f"{c:+.2f}" for c in vector) + ")"


main()
