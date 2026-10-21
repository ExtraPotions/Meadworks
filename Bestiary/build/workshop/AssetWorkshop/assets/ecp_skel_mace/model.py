"""The skeleton arsenal's mace (Elite Creatures Pack), v2: a club made of a backbone. The upper half is a spine of
five vertebrae off some beast, growing bigger towards the head and turning a little each, so their spines and wings
stand out as flanges all round; they are threaded on a bone core, and below them the haft goes on as a spine of small tail
vertebrae with recessed bone joints between, tapering to a point for the foot: the whole mace one tail, small at the
fist and huge at the head.

Weapon axes (see ecp_skel_arsenal): the fist round the grip at the origin, the haft up -Y (Unity +Z). About 0.96 m
long and 0.27 m across the head, like MaceIron (1.03 m, 0.22 m) and the skeleton's own mace. v1 (two vertebrae set with
fangs on a femur) is archived in assets/skeleton_weapons_v1.
"""
import math
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "ecp_skel_arsenal"))

import low_bones as bones  # noqa: E402
import low_paint as gp  # noqa: E402
import low_shapes as gs  # noqa: E402
import low_vertebra as gv  # noqa: E402

TEXTURE_SIZE = 64
AO_STRENGTH = 0.2
CATEGORY = 'weapon.mace'
NORMAL_MAP = False

BUTT = -0.1                                     # the haft's spine starts here, its tail tip below
SPINE = (0.5, 5, 0.03, 0.00975, 65.0)          # five broad head bones, retaining the original end radii and reach


def build():
    bone, spine, sinew = gp.bone("mace_bone"), gp.vertebra("mace_vertebra"), gp.sinew()
    gv.column("haft", BUTT, 0.47, 0.021, 0.027, spine, disc=sinew, count=5)
    bones.fang("butt", gs.at(BUTT + 0.004), gs.at(-0.15), 0.011, bone)
    top = _backbone(spine)
    gs.loft("core", [(0.44, 0.017, 0.017), (top, 0.015, 0.015)], bone, sides=8)
    gs.knob("core_end", gs.at(top), 0.019, bone, squash=(1.0, 0.8, 1.0))
    gs.rings("joint_sinew", [0.448, 0.456, 0.464, 0.472], 0.0235, 0.0038, sinew)
    # Curve the entire tail-vertebra handle, butt through grip to neck; keep the heavy head rigid.
    gs.bend(gs.meshes(), gs.handle_s(0.0275, -0.15, 0.44))


def _backbone(spine):
    """The club's vertebrae up the core, each bigger and turned a little further round than the one below; returns the
    s just above the top one."""
    s, count, r, grow, turn = SPINE
    for i in range(count):
        size = r + grow * i
        angle = math.radians(turn * i)
        back = (math.cos(angle), 0.0, math.sin(angle))
        gv.build(f"spine_{i}", gv.frame(gs.at(s), (0, -1, 0), back), size, spine, kind='beast')
        if i < count - 1:
            s += 0.28548 / (count - 1)          # original first-to-last head spacing
    return s + 0.45 * size
