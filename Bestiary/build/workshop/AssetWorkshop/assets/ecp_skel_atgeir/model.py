"""The skeleton arsenal's atgeir (Elite Creatures Pack), v2: the head is a great vertebra lying flat in the blade's
plane on the end of the shaft, the canal showing through; its spine is drawn out into a long single-edged blade whose
back sweeps round like a rib, one wing is a short spike on the edge side and the other a long fang of a hook curving
up off the back. The shaft's upper half is a spine: six big tail vertebrae stacked end to end with recessed bone joints
between, curving like a real spine, thickening under the head into two bigger ones whose spines lie down its back;
its lower half, where the fist holds, is two long bones laid end to end and seated in bone collars; a fang for a foot.

Built along the weapon (-Y) and then turned into the game's hold: the game's atgeirs (AtgeirIron) lie in the fist at a
slant, their haft through the fist but 21 degrees off the attach frame's axis, so the two-handed clips put the other
hand on the haft. HOLD is that haft direction and BLADE_SIDE the way the blade's edge faces, measured from AtgeirIron's
mesh in its attach frame (Blender axes). About 3.0 m long with the fist 0.76 m from the foot, like AtgeirIron. v1 (a
rib blade with a column of vertebrae under it) is archived in assets/skeleton_weapons_v1.
"""
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "ecp_skel_arsenal"))

import bpy  # noqa: E402
from mathutils import Matrix, Vector  # noqa: E402

import grave_blade as gb  # noqa: E402
import low_bones as bones  # noqa: E402
import low_paint as gp  # noqa: E402
import low_shapes as gs  # noqa: E402
import low_vertebra as gv  # noqa: E402

TEXTURE_SIZE = 128
AO_STRENGTH = 0.2
CATEGORY = 'weapon.atgeir'
NORMAL_MAP = False

HOLD = Vector((-0.355, -0.924, -0.14))          # up the haft, towards the blade
BLADE_SIDE = Vector((-0.925, 0.325, 0.198))     # across the blade, towards its edge

FOOT, HALF, NECK, SHAFT_END = -0.68, 0.5, 1.4, 1.66   # long bones foot to HALF, vertebrae on, thickening at NECK
HEAD_R = 0.048
# (s from the spine's root, back x, edge x, half thickness at the back, bevel): the back sweeping to +X, the edge -X.
BLADE = [(0.0, 0.015, -0.015, 0.0125, 0.45), (0.09, 0.018, -0.042, 0.0112, 0.5), (0.22, 0.024, -0.05, 0.01, 0.5),
         (0.35, 0.032, -0.042, 0.0086, 0.52)]
TIP = (2.28, 0.052)


def build():
    bone, spine, blade, sinew = gp.bone("atgeir_bone"), gp.vertebra("atgeir_vertebra"), gp.bone_blade("atgeir_blade"), gp.sinew()
    bones.jointed_haft("bones", FOOT, HALF, (-0.25,), 0.0195, bone, sinew)
    gv.column("shaft", HALF, NECK, 0.022, 0.025, spine, disc=sinew, count=6)
    gs.wrap("half_sinew", HALF - 0.035, HALF + 0.02, 0.0242, 0.008, 0.003, sinew)
    gv.column("neck", NECK, SHAFT_END, 0.028, 0.034, spine, disc=sinew, kind='thoracic', count=2)
    place, root = _head(spine)
    sections = [(root + ds, xb, xe, t, bevel) for ds, xb, xe, t, bevel in BLADE]
    gb.single("blade", gb.notch(sections, root + 0.25, 0.006, side=1, width=0.012), blade, tip=TIP)
    bones.pores("pore", [gs.at(root + 0.2, -0.004, side * 0.011) for side in (-1, 1)], 0.0036, gp.dark())
    gs.wrap("socket_sinew", SHAFT_END - 0.075, SHAFT_END + 0.004, 0.0235, 0.0075, 0.003, sinew)
    bones.fang("foot", gs.at(FOOT - 0.006), gs.at(-0.8), 0.017, gp.teeth("atgeir_teeth"))
    gs.bend(gs.meshes(), gs.wave(0.028, 0.55, end=1.6, start=HALF + 0.03))   # the spine half curves like a spine's
    _turn()


def _head(spine):
    """The head vertebra with its own wings: a short spike on the edge side (-X), a long hook up off the back (+X)."""
    place, root = gv.flat("head", SHAFT_END - 0.012, HEAD_R, spine, wings=False)
    edge_root, back_root = gv.at(place, HEAD_R, gv.WING_ROOT), gv.at(place, HEAD_R, (-0.5, 0.8, 0.05))
    bones.fang("spike", edge_root, gv.at(place, HEAD_R, (1.7, 1.1, 0.0)), 0.27 * HEAD_R, spine)
    bones.fang("hook", back_root, gv.at(place, HEAD_R, (-2.6, 2.6, 0.0)), 0.3 * HEAD_R, spine,
               bend=Vector((0.03, 0.0, 0.0)))
    return place, root


def _turn():
    """Canonical (haft along -Y, edge towards -X, flats facing Z) into the game's hold."""
    up = HOLD.normalized()
    edge = (BLADE_SIDE - up * BLADE_SIDE.dot(up)).normalized()
    flat = edge.cross(up)
    canonical = Matrix((Vector((0, -1, 0)), Vector((-1, 0, 0)), Vector((0, 0, 1)))).transposed()
    hold = Matrix((up, edge, flat)).transposed()
    turn = (hold @ canonical.inverted()).to_4x4()
    for obj in bpy.context.scene.objects:
        if obj.type == 'MESH':
            obj.matrix_world = turn @ obj.matrix_world
