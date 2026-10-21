"""Coarse, faceted geometry for the bone arsenal; no modelled cords or tiny pores."""
from grave_shapes import *
import grave_shapes as old


def loft(name, stations, material, sides=6, roundness=.85, smooth=False):
    return old.loft(name, stations, material, min(sides, 6), roundness, False)


def band(name, points, material, sides=4, up=Vector((0, 0, 1)), smooth=False):
    return old.band(name, points, material, min(sides, 6), up, False)


def knob(name, location, radius, material, squash=(1, 1, 1), segments=6, rings=3):
    obj = old.knob(name, location, radius, material, squash, min(segments, 6), min(rings, 3))
    for p in obj.data.polygons:
        p.use_smooth = False
    return obj


def cone(name, base, tip, radius, material, tip_ratio=.25, vertices=4, squash=None):
    sx, sy = squash or (1, 1)
    return band(name, [(Vector(base), radius * sy, radius * sx),
                       (Vector(tip), radius * sy * tip_ratio, radius * sx * tip_ratio)],
                material, sides=4)


def wrap(name, s0, s1, radius, pitch, thickness, material, centre=(0, 0), squash=1, phase=0):
    # Bone collar, replacing costly cord spirals. Four rings give a broad raised rim.
    return loft(name, [(s0, radius, radius * squash, *centre),
                       (s0 + (s1-s0)*.2, radius+thickness, (radius+thickness)*squash, *centre),
                       (s1 - (s1-s0)*.2, radius+thickness, (radius+thickness)*squash, *centre),
                       (s1, radius, radius*squash, *centre)], material)


def rings(name, s_list, radius, thickness, material, centre=(0, 0), squash=1):
    return [wrap(name, min(s_list)-thickness, max(s_list)+thickness, radius,
                 1, thickness, material, centre, squash)]
