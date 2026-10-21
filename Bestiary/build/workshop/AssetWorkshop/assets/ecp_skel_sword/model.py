"""The skeleton arsenal's sword (Elite Creatures Pack), v2: a blade with a backbone. The guard is a big vertebra lying
flat in the blade's plane, its wings the quillons and the canal showing through, and the blade is its spine drawn out
into a broad double edge; down the middle of the blade, from the guard nearly to the point, runs a backbone of smaller and
smaller vertebrae grown into it, their arches and spinous processes a knobbled ridge on the front; the grip is a
spine too, three short vertebrae with recessed bone between, and the pommel one more vertebra, its spine pointing down: one
spine through the whole sword.

Weapon axes (see ecp_skel_arsenal): the fist round the grip's middle at the origin, the blade up -Y (Unity +Z), its flats
facing Z, the backbone on the +Z flat. About 1.12 m from the pommel's spine to the point, like the skeleton's own
sword (0.97 m past the grip) and SwordIron (1.18 m). v1 is archived in assets/skeleton_weapons_v1.
"""
import math
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "ecp_skel_arsenal"))

from mathutils import Vector  # noqa: E402

import grave_blade as gb  # noqa: E402
import low_bones as bones  # noqa: E402
import low_paint as gp  # noqa: E402
import low_shapes as gs  # noqa: E402
import low_vertebra as gv  # noqa: E402

TEXTURE_SIZE = 64
AO_STRENGTH = 0.2
CATEGORY = 'weapon.sword'
NORMAL_MAP = False

GRIP = (-0.056, 0.054)                          # the grip's ends
POMMEL_R, GUARD_R = 0.024, 0.045
# (s, half width, half thickness, bevel, ridge); the first section is replaced by the guard's spine root.
BLADE = [(0.0, 0.034, 0.0125, 0.3, 1.2), (0.22, 0.05, 0.0112, 0.34, 1.25), (0.52, 0.043, 0.0098, 0.36, 1.25),
         (0.78, 0.037, 0.0088, 0.38, 1.2), (0.91, 0.025, 0.0072, 0.42, 1.15)]
TIP = 0.99
BACKBONE_END = 0.925                            # the blade's backbone runs from the guard to here, near the point
SNAKE = 0.014                                   # how far it winds to either side on its way (one S)


def build():
    bone, spine, blade, sinew = gp.bone("sword_bone"), gp.vertebra("sword_vertebra"), gp.bone_blade("sword_blade"), gp.sinew()
    gv.column("grip", GRIP[0], GRIP[1], 0.019, 0.021, spine, disc=sinew, kind='grip', count=3)
    pommel = gv.frame(gs.at(GRIP[0] - 0.8 * POMMEL_R), (0, 0, 1), (0, 1, 0))
    gv.build("pommel", pommel, POMMEL_R, spine, kind='lumbar')
    _, root = gv.flat("guard", GRIP[1], GUARD_R, spine)
    sections = [(max(s, root), -w, w, t, bevel, ridge) for s, w, t, bevel, ridge in BLADE]
    gb.blade("blade", gb.notch(gb.notch(sections, 0.46, 0.007, side=1), 0.7, 0.006, side=-1), blade, tip=(TIP, 0.0))
    backbone = gv.column("backbone", root + 0.004, BACKBONE_END, 0.03, 0.013, spine, disc=sinew, kind='shaft', count=8,
                         back=(0, 0, 1), wings=False)
    start = root + 0.004
    gs.bend(backbone, lambda s: Vector((SNAKE * math.sin(2 * math.pi * (s - start) / (BACKBONE_END - start)), 0.0, 0.0)))
    bones.pores("pore", [gs.at(s, x, -0.0105) for s, x in ((0.3, 0.02), (0.6, -0.016), (0.74, 0.01))], 0.0036, gp.dark())

