"""A giant two-handed greataxe for a skeleton (Elite Creatures Pack), all bone, in the look of the game's own bone gear.

The haft is a whole spine, tailbone to neck, curving like a living back (axe_spine); it rises into a skull strapped in
rawhide, a giant's jawbone for a bearded crescent set against one temple, its teeth the cutting edge, a tusk through
the other temple for the back hook, a fang out of the crown (axe_head, axe_blade, axe_skull). Built and painted like
the game's own bone gear (Spinesnap bow, bone tower shield, Skeleton): few chunky parts, a small point-filtered atlas
painted with light, ochre bone, brown in every hollow; ivory teeth, tusk and fang; near-black leather grips and dark
sinew bindings (axe_paint).

Axe coordinates: Z up the haft, the blade's edge towards -Y, the spikes of the spine along the back (+Y), the skull
looking out of the blade's flat (+X). The origin is the middle of the lower grip, where the leading hand holds it;
UPPER_GRIP is the other hand. About 2.1 m long, the head a metre from hook to edge: bigger than any battleaxe in the
game (1.6 to 1.8 m), built for a 2 m skeleton.
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bpy  # noqa: E402
from mathutils import Matrix  # noqa: E402

import axe_head  # noqa: E402
import axe_paint  # noqa: E402
import axe_spine  # noqa: E402

TEXTURE_SIZE = 256                          # the game's bone gear is 64 to 128 px, point filtered
AO_STRENGTH = 0.7
LOWER_GRIP = sum(axe_spine.GRIPS[0]) / 2
UPPER_GRIP = sum(axe_spine.GRIPS[1]) / 2


def build():
    mats = {"bone": axe_paint.bone(), "skull": axe_paint.skull(), "blade": axe_paint.blade(), "tusk": axe_paint.tusk(),
            "leather": axe_paint.leather(), "sinew": axe_paint.sinew(),
            "socket": axe_paint.socket_dark()}
    centre = axe_spine.build(mats)
    top = centre.at(axe_spine.top(centre))[0]
    axe_head.build(mats, top + axe_head.above_spine())
    pivot = centre.at(LOWER_GRIP)[0]
    for obj in bpy.context.scene.objects:
        if obj.type == 'MESH':
            obj.data.transform(Matrix.Translation(-pivot))
    return centre, pivot
