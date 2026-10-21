"""The kraken's head, ecp_kraken_head: origin at the centre of the crown (where the column meets the head), face towards
Unity +Z, up +Y.

A bulbous mantle rises from the head and leans back (widest 1.8 m, top at y 6.5, centred near z -0.8); on the face two
big eyes under heavy brows, a parrot beak in a fleshy collar, the siphon on the right below the right eye. Below the
crown the body goes on down as a thick muscular column to y -5, the roots of the six long tentacles wrapped round it
(body.py, column.py). Bones (Unity positions, all rest rotations identity):

    kh_root (0,0,0)                        the top of the column
      kh_body_1 (0,-1,0) > kh_body_2 (0,-2.4,0) > kh_body_3 (0,-3.8,0)   the column's spine: the mod bends it
      kh_neck (0,0.8,0)                    the head on top of the column; the bite lunge pivots here
        kh_mantle (0,2.6,-0.4)             the mantle above the eyes
        kh_beak_upper, kh_beak_lower       the hinge (0,1.25,1.0); rest pose closed
        kh_siphon                          the siphon's base; kh_ink (marker) at its mouth
        kh_eye_l, kh_eye_r                 the eye centres (the eyeballs are their own mesh)
    markers: kh_mouth (beak tip, on kh_neck), kh_ink (on kh_siphon),
             kh_tentacle_0..5 (on kh_body_3, on the tentacle roots at y -3.5)
"""
import math
import random

from mathutils import Vector
from mathutils.bvhtree import BVHTree

import body
import column
import head_parts as hp
from geo import U, Part

NECK, MANTLE = (0.0, 0.8, 0.0), (0.0, 2.6, -0.4)
# height, yaw (degrees from +Z towards +X), count: on the mantle, the lower head and the column's back between roots
BARNACLES = [(4.75, 150, 7), (5.55, -160, 6), (3.80, -125, 5), (4.30, 112, 5), (6.10, 178, 4), (3.05, -150, 4),
             (1.70, 135, 3), (5.20, 95, 3), (-1.0, 180, 4), (-2.3, 135, 3), (-0.9, -135, 3)]


def barnacles(skin):
    """Clusters placed on the skin by casting rays from the body's axis."""
    tree = BVHTree.FromPolygons([tuple(v) for v in skin.verts], skin.faces)
    part = Part("barnacles", closed=False)
    rng = random.Random(7)
    for y, yaw, count in BARNACLES:
        centre, _ = body.plain_point(y, math.radians(yaw))
        for k in range(count):
            offset = Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), rng.uniform(-1, 1))) * 0.3 if k else Vector()
            hit = _cast(tree, centre + offset, y + offset.y)
            if hit:
                radius = rng.uniform(0.05, 0.13) if k else 0.14
                hp.barnacle(part, hit[0], hit[1], radius, column.weights)
    return part


def _cast(tree, target, y):
    origin = U(*body.axis_point(y))
    direction = (U(*target) - origin).normalized()
    location, normal, _, _ = tree.ray_cast(origin, direction)
    if location is None:
        return None
    return location, normal if normal.dot(direction) > 0 else -normal


def rig():
    bones = [("kh_root", "", (0.0, 0.0, 0.0)), ("kh_neck", "kh_root", NECK), ("kh_mantle", "kh_neck", MANTLE),
             ("kh_beak_upper", "kh_neck", hp.HINGE), ("kh_beak_lower", "kh_neck", hp.HINGE),
             ("kh_siphon", "kh_neck", hp.SIPHON_BASE),
             ("kh_eye_l", "kh_neck", hp.EYES["kh_eye_l"]), ("kh_eye_r", "kh_neck", hp.EYES["kh_eye_r"])]
    bones += body.BODY_BONES
    markers = [("kh_mouth", "kh_neck", hp.MOUTH), ("kh_ink", "kh_siphon", tuple(round(c, 4) for c in hp.siphon_tip()))]
    return bones, markers + column.markers()


def build():
    """Returns (game parts, bake parts, eye parts, bones, markers)."""
    bones, markers = rig()
    skin = body.body()
    roots = range(len(column.TENTACLE_YAWS))

    def parts(detail):
        items = [body.body() if detail else skin, hp.beak(True), hp.beak(False), hp.lips(), hp.throat(), hp.siphon(),
                 hp.eyelid("kh_eye_l"), hp.eyelid("kh_eye_r"), barnacles(skin)]
        items += [column.root_mesh(k) for k in roots]
        if detail:
            items += [column.root_suckers(k) for k in roots]
        return items
    return parts(False), parts(True), [hp.eyeball("kh_eye_l"), hp.eyeball("kh_eye_r")], bones, markers
