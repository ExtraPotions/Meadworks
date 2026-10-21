"""Bones the skeleton arsenal is made of, besides the vertebrae (grave_vertebra) and the ground bone blades (grave_blade):
long bones with their knuckled ends, hafts of long bones joined end to end, ribs, fangs and the pores in a bone's face.
Low-poly like the game's bones (LargeBone, the Skeleton, the bone tower shield): a few big facets, a waisted shaft,
round knuckles. Positions are along the weapon (grave_shapes.at: s up the weapon, x across, z out of the flat).
"""
import math

from mathutils import Vector

import low_shapes as gs

KNUCKLE, BALL = 'knuckle', 'ball'


def long_bone(name, s0, s1, r, material, bottom=KNUCKLE, top=BALL, x=0.0, z=0.0):
    """A long bone (a femur) along the weapon from s0 to s1, `r` its shaft's half width: flared towards both ends and
    waisted between, ended by `bottom` and `top` - a KNUCKLE (two condyles side by side), a BALL (a round head with a
    knob of trochanter below it) or None (the end hidden in something else)."""
    length = s1 - s0
    stations = [(s0 + length * f, r * w, r * h, x, z) for f, w, h in
                ((0.0, 1.25, 1.1), (0.08, 1.12, 1.0), (0.3, 0.95, 0.9), (0.6, 0.92, 0.88), (0.9, 1.08, 1.0), (1.0, 1.2, 1.08))]
    parts = [gs.loft(name, stations, material, sides=8, roundness=0.8)]
    parts += _end(name + '_bottom', bottom, s0, -1, r, material, x, z)
    parts += _end(name + '_top', top, s1, 1, r, material, x, z)
    return parts


def _end(name, kind, s, way, r, material, x, z):
    if kind == KNUCKLE:
        return [gs.knob(f'{name}_{side}', gs.at(s + way * 0.2 * r, x + side * 0.62 * r, z), 1.02 * r, material,
                        squash=(1.0, 1.05, 1.1)) for side in (-1, 1)]
    if kind == BALL:
        return [gs.knob(name, gs.at(s + way * 0.35 * r, x, z), 1.18 * r, material),
                gs.knob(name + '_trochanter', gs.at(s - way * 0.9 * r, x + 0.75 * r, z), 0.6 * r, material)]
    return []


def jointed_haft(name, s0, s1, joints, r, bone, sinew):
    """A haft of long bones laid end to end, knuckle to ball, each joint bound in sinew: joints are the s where one bone
    ends and the next begins."""
    ends = [s0] + list(joints) + [s1]
    parts = []
    for i, (a, b) in enumerate(zip(ends, ends[1:])):
        parts += long_bone(f'{name}_{i}', a + (0.012 if i else 0.0), b - (0.012 if i < len(joints) else 0.0), r, bone)
    for i, s in enumerate(joints):
        parts.append(gs.wrap(f'{name}_joint_{i}', s - 0.045, s + 0.045, r * 1.28, 0.0085, r * 0.2, sinew))
    return parts


def rib(name, points, material, sides=6, up=Vector((0, 0, 1))):
    """A rib: a flattened band swept along points [(point, half height along `up`, half thickness)], knobbed at its head
    (the first point)."""
    parts = [gs.band(name, points, material, sides=sides, up=up)]
    head, high, thick = points[0]
    parts.append(gs.knob(name + '_head', head, max(high, thick) * 1.15, material, squash=(1.0, 1.0, 1.0)))
    return parts


def fang(name, base, tip, r, material, bend=Vector()):
    """A tooth or claw from `base` to a point at `tip`, curved by `bend` (the offset of its middle), round at the root."""
    base, tip, bend = Vector(base), Vector(tip), Vector(bend)
    points = []
    for i in range(3):
        t = i / 2
        co = base.lerp(tip, t) + bend * (4 * t * (1 - t))
        radius = r * (1.0 - 0.92 * t ** 1.3)
        points.append((co, radius, radius))
    return gs.band(name, points, material, sides=4)


def pores(name, points, radius, material):
    """Nutrient holes in a bone's face: small dark dimples, each a flattened ball sunk into the surface at `points`."""
    return []  # Sub-texel pores belong in the coarse painted texture.



def around(s, radius, count, phase=0.0):
    """`count` points on a ring of `radius` round the weapon's axis at s (for fangs set round a head)."""
    return [(gs.at(s, radius * math.cos(phase + 2 * math.pi * i / count), radius * math.sin(phase + 2 * math.pi * i / count)),
             Vector((math.cos(phase + 2 * math.pi * i / count), 0.0, math.sin(phase + 2 * math.pi * i / count))))
            for i in range(count)]
