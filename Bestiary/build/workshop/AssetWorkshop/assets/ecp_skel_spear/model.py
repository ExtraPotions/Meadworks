"""The skeleton arsenal's spear (Elite Creatures Pack), v2: the head is a vertebra. Its body is the socket, seated in a bone collar onto the end of a shaft whose upper half, the fist's half, is a spine - eight big tail vertebrae stacked end to
end, recessed bone between each pair, gently curving below the head - and whose lower half is two long bones
laid end to end, seated in bone collars, the bottom one's knuckles the butt. The head lies flat in the blade's plane with the
canal showing through, its wings stand out to the sides as the lugs, and its spine is drawn out and ground into a broad leaf
point.

Weapon axes (see ecp_skel_arsenal), but reversed like the game's own spears: the fist at the origin, the head down +Y
(Unity -Z) 1.08 m away and the butt 1.35 m up -Y, since the spear clips hold it overhand (SpearBronze and SpearFlint
both). Built head up -Y first and turned half round Z at the end. The blade's flats face Z. 2.43 m long. v1 (a bone
leaf point with a vertebra collar) is archived in assets/skeleton_weapons_v1.
"""
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "ecp_skel_arsenal"))

import bpy  # noqa: E402
from mathutils import Matrix  # noqa: E402

import grave_blade as gb  # noqa: E402
import low_bones as bones  # noqa: E402
import low_paint as gp  # noqa: E402
import low_shapes as gs  # noqa: E402
import low_vertebra as gv  # noqa: E402

TEXTURE_SIZE = 64
AO_STRENGTH = 0.2
CATEGORY = 'weapon.spear'
NORMAL_MAP = False

BUTT, HALF, SHAFT_END = -1.345, -0.32, 0.705   # the shaft: long bones from the butt to HALF, vertebrae on to the head
HEAD_R = 0.042
# (s from the spine's root, half width, half thickness, bevel, ridge): a leaf, widest low, the ridge standing proud.
BLADE = [(0.0, 0.013, 0.011, 0.3, 1.2), (0.055, 0.043, 0.0098, 0.42, 1.45), (0.14, 0.04, 0.009, 0.44, 1.5),
         (0.21, 0.022, 0.0075, 0.48, 1.4)]
TIP = 1.08


def build():
    bone, spine, blade, sinew = gp.bone("spear_bone"), gp.vertebra("spear_vertebra"), gp.bone_blade("spear_blade"), gp.sinew()
    bones.jointed_haft("bones", BUTT, HALF, ((BUTT + HALF) / 2,), 0.0185, bone, sinew)
    gv.column("shaft", HALF, SHAFT_END, 0.022, 0.025, spine, disc=sinew, count=8)
    _, root = gv.flat("head", SHAFT_END - 0.012, HEAD_R, spine)
    sections = [(root + ds, -w, w, t, bevel, ridge) for ds, w, t, bevel, ridge in BLADE]
    gb.blade("blade", gb.notch(sections, root + 0.1, 0.005, side=-1, width=0.012), blade, tip=(TIP, 0.0))
    bones.pores("pore", [gs.at(root + 0.06, 0.018, side * 0.0102) for side in (-1, 1)], 0.0035, gp.dark())
    gs.wrap("socket_sinew", SHAFT_END - 0.07, SHAFT_END + 0.004, 0.0225, 0.0075, 0.0029, sinew)
    gs.bend(gs.meshes(), gs.wave(0.02, 0.45, end=0.62, calm=0.08, rise=0.1, start=HALF + 0.02))   # the spine half curves
    half_turn = Matrix.Rotation(3.14159265, 4, 'Z')
    for obj in bpy.context.scene.objects:
        if obj.type == 'MESH':
            obj.matrix_world = half_turn @ obj.matrix_world
