"""The skeleton arsenal's axe (Elite Creatures Pack), v2: the head is one great vertebra lying flat in the blade's plane,
bored through its body and driven onto a haft that is a spine of long tail vertebrae, cartilage
between them and a point at each end; its spine is drawn out and ground into the broad blade, its
canal shows through between haft and blade, and its two wings sweep forward as horns, the lower one the beard, the
upper a spike beside the haft's top. A broad bone collar seats the head.

Weapon axes (see ecp_skel_arsenal): the fist round the grip at the origin, the haft up -Y (Unity +Z), the blade out to
-X (Unity +X, as AxeIron's), its flats facing Z. 0.9 m long, the blade 0.23 m out from the haft, like AxeIron. v1 (a
shoulder-blade head with a vertebra collar) is archived in assets/skeleton_weapons_v1.
"""
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
CATEGORY = 'weapon.axe_1h'
NORMAL_MAP = False

BUTT, TOP = -0.12, 0.7                          # the haft's spine
HEAD, HEAD_R = 0.6, 0.058                       # the vertebra's body on the haft, and its size
# Out from the spine's root along -X: (u, v low, v high, half thickness), v along the haft (+ up the weapon).
BLADE = [(0.0, -0.019, 0.019, 0.016), (0.035, -0.033, 0.03, 0.012), (0.08, -0.078, 0.055, 0.0082),
         (0.13, -0.112, 0.07, 0.0052), (0.152, -0.118, 0.073, 0.0009)]


def build():
    bone, spine, blade, sinew = gp.bone("axe_bone"), gp.vertebra("axe_vertebra"), gp.bone_blade("axe_blade"), gp.sinew()
    gv.column("haft", BUTT, TOP, 0.021, 0.023, spine, disc=sinew, count=8)
    bones.fang("butt", gs.at(BUTT + 0.004), gs.at(-0.17), 0.011, bone)
    bones.fang("crown", gs.at(TOP - 0.004), gs.at(0.745), 0.011, bone)
    place = gv.frame(gs.at(HEAD), (0, 0, 1), (-1, 0, 0))
    gv.build("head", place, HEAD_R, spine, kind='lumbar', spine=False, wings=False)
    gb.slab("blade", gv.at(place, HEAD_R, gv.SPINE_ROOT), (-1, 0, 0), BLADE, blade)
    for side in (-1, 1):                        # local x is +Y (down the haft): -1 the upper horn, +1 the beard
        root = gv.at(place, HEAD_R, (side * 0.5, 0.8, 0.0))
        tip = gv.at(place, HEAD_R, (side * 2.4, 1.75, 0.0))
        bones.fang(f"horn_{side}", root, tip, 0.3 * HEAD_R, spine, bend=Vector((-0.012, side * 0.01, 0.0)))
    bones.pores("pore", [gs.at(HEAD - 0.01, -0.15, side * 0.0075) for side in (-1, 1)], 0.0045, gp.dark())
    gs.rings("head_sinew", [HEAD + 0.064, HEAD + 0.071, HEAD - 0.064, HEAD - 0.071], 0.0215, 0.0034, sinew)
    # The full handle, including the butt and grip, follows one S; the head remains rigid above it.
    gs.bend(gs.meshes(), gs.handle_s(0.0325, -0.17, 0.49))
