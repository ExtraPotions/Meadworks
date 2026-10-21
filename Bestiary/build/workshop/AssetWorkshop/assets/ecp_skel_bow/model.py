"""The skeleton arsenal's bow (Elite Creatures Pack), v2: limbs of backbone. Each limb is a column of vertebrae,
biggest at the grip and smaller towards the tip, threaded on a rib that gives the bow its bend (angular like the
skeleton's own, Characters/Skeleton/weapons/skeleton_bow), their spines standing out on the bow's back and their wings
to the sides, like the game's Spinesnap; the grip is three short bone vertebrae between the two limbs, the arrow riding over
the upper one; finger bones cap both tips for nocks. The string is not part of the model: the bow's wielder draws it
(the showcase stretches it from the tips to the drawing fingers).

Built upright (the top tip up -Y, the string side +X, the flats facing Z), then turned into the game's hold: the
skeletons hold their bow slanted in the left fist (skeleton_bow's attach frame: the stave through the fist, its string
0.35 m behind the grip along STRING, the limbs along LIMBS; measured from the game's mesh, Blender axes). The build
writes out/ecp_skel_bow_points.json: the tips, the rest (where the arrow passes the bow) and the string's middle at
rest, turned, in Blender and in Unity axes. About 1.34 m tip to tip. v1 (two ribs with a vertebra shelf) is archived
in assets/skeleton_weapons_v1.
"""
import json
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "ecp_skel_arsenal"))

import bpy  # noqa: E402
from mathutils import Matrix, Vector  # noqa: E402

import low_paint as gp  # noqa: E402
import low_shapes as gs  # noqa: E402
import low_vertebra as gv  # noqa: E402

TEXTURE_SIZE = 128
AO_STRENGTH = 0.2
CATEGORY = 'weapon.bow'
NORMAL_MAP = False

STRING = Vector((0.751, -0.006, 0.659))         # from the grip towards the string's middle
LIMBS = Vector((0.492, -0.704, -0.512))         # along the stave
TOP = -1                                        # which way along LIMBS the top tip lies
POINTS_FILE = os.path.join(os.path.dirname(os.path.abspath(__file__)), "out", "ecp_skel_bow_points.json")

# The rib a limb is threaded on, grip to tip: (s, x, half width, half thickness); the other limb is its mirror.
LIMB = [(0.0, 0.0, 0.017, 0.015), (0.11, -0.004, 0.012, 0.011), (0.33, 0.09, 0.01, 0.009), (0.585, 0.29, 0.008, 0.007),
        (0.645, 0.325, 0.0075, 0.0065)]
TIP_S, TIP_X = 0.66, 0.34                       # the nock, where the string is tied
REST = (0.088, -0.02)                           # (s, x): the arrow passes over the upper grip vertebra here
COLUMN = (0.105, 0.52, 5, 0.033, 0.020)          # a limb's vertebrae: first and last s, how many, first and last r


def build():
    bone, spine, sinew = gp.bone("bow_bone"), gp.vertebra("bow_vertebra"), gp.sinew()
    for side in (1, -1):
        gs.band(f"rib_{side}", [((x, -side * s, 0.0), w, t) for s, x, w, t in LIMB], bone, sides=6)
        _column(side, spine)
        gs.knob(f"nock_{side}", (TIP_X, -side * TIP_S, 0.0), 0.015, bone, squash=(1.0, 1.5, 1.0))
        gs.cone(f"nock_hook_{side}", (TIP_X - 0.004, -side * (TIP_S + 0.012), 0.0), (TIP_X + 0.018, -side * (TIP_S + 0.026), 0.0),
                0.006, bone, vertices=5)
    gs.loft("grip_core", [(-0.075, 0.019, 0.017), (0.075, 0.019, 0.017)], bone, sides=8)
    gv.column("grip", -.075, .075, .019, .019, spine, disc=sinew, kind="grip", count=3)
    gs.rings("grip_sinew", [0.074, -0.074], 0.021, 0.0034, sinew)
    _points(_turn())


def _column(side, spine):
    """One limb's vertebrae along its rib, spines out on the bow's back (-X), smaller towards the tip."""
    first, last, count, r0, r1 = COLUMN
    for i in range(count):
        t = i / (count - 1)
        s = first + (last - first) * t
        ahead = Vector((_limb_x(s + 0.01) - _limb_x(s - 0.01), -side * 0.02, 0.0))   # along the limb, towards its tip
        place = gv.frame((_limb_x(s), -side * s, 0.0), ahead, (-1, 0, 0))
        gv.build(f"limb_{side}_{i}", place, r0 + (r1 - r0) * t, spine, kind='tail', pegs=False, low=True, height=3.1)


def _limb_x(s):
    """The limb's middle line across (x) at s along it."""
    s = abs(s)
    for (a, xa, _, _), (b, xb, _, _) in zip(LIMB, LIMB[1:]):
        if a <= s <= b:
            return xa + (xb - xa) * (s - a) / (b - a)
    return LIMB[-1][1]


def _turn():
    """Upright (top -Y, string +X, flats Z) into the skeleton bow's hold."""
    back = STRING.normalized()
    top = (TOP * LIMBS - back * (TOP * LIMBS).dot(back)).normalized()
    flat = -back.cross(top)
    upright = Matrix((Vector((1, 0, 0)), Vector((0, -1, 0)), Vector((0, 0, 1)))).transposed()
    hold = Matrix((back, top, flat)).transposed()
    turn = (hold @ upright.inverted()).to_4x4()
    for obj in bpy.context.scene.objects:
        if obj.type == 'MESH':
            obj.matrix_world = turn @ obj.matrix_world
    return turn


def _points(turn):
    points = {"tip_top": (TIP_X, -TIP_S, 0.0), "tip_bottom": (TIP_X, TIP_S, 0.0), "rest": (REST[1], -REST[0], 0.0),
              "string_rest": (TIP_X, 0.0, 0.0)}
    blender = {k: list(turn @ Vector(v)) for k, v in points.items()}
    unity = {k: [-v[0], v[2], -v[1]] for k, v in blender.items()}
    os.makedirs(os.path.dirname(POINTS_FILE), exist_ok=True)
    with open(POINTS_FILE, "w", encoding="utf-8") as handle:
        json.dump({"blender": blender, "unity": unity}, handle, indent=2)
