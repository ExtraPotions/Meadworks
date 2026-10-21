"""The spine the arsenal skeletons drop (Elite Creatures Pack; BRIEF.md): a length of a skeleton's back, eight
vertebrae still joined by dark dried discs, lying on its belly with the spikes up and a gentle S from end to end. It
replaces the single vertebra (ecp_vertebra) as the bone the skeleton arsenal's weapons are made from.

The vertebrae are the arsenal's own (grave_vertebra, the low build the weapons' columns use): four lumbar at the tail
end (-X: big bodies, hatchet spikes, long wings), four thoracic towards the neck (+X: smaller, long spikes sloping
back), tapering evenly. About 0.9 m long: a dropped material is drawn about three times real size (codex
models/items.md). Metres, Z up, the origin on the ground under the middle; one box collider round it all.

    .\build.ps1 -Asset ecp_spine -Lineup
"""
import math
import os
import random
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "ecp_skel_arsenal"))

import bmesh  # noqa: E402
import bpy  # noqa: E402
from mathutils import Matrix, Vector  # noqa: E402

from workshop import paint, paint_specs, scene, shapes  # noqa: E402

import grave_shapes as gs  # noqa: E402
import grave_vertebra as gv  # noqa: E402

CATEGORY = "item.material"
TEXTURE_SIZE = 128
AO_STRENGTH = 0.2          # the paint recipes paint their own hollows (codex/paint.md)
NORMAL_FROM_ALBEDO = 5.4   # bone's normal strength k
DENSITY = 57.0             # px per metre the paint is drawn at: the game's BoneFragments

# The arsenal's lumbar and thoracic vertebrae made chunkier: wings shorter and half again as thick, the thoracic spikes
# broader. A dropped item is seen whole and from above, where the weapons' thin processes read as sticks; the game's
# own neck item (AsksvinCarrionNeck) is eight fat drums.
KINDS = {"item_lumbar": dict(gv.KINDS["lumbar"], wing=(1.5, 0.2), wing_r=0.4),
         "item_thoracic": dict(gv.KINDS["thoracic"], spine=[(1.62, 0.0), (2.05, -0.45), (2.3, -1.0)],
                               spine_hh=(0.4, 0.32, 0.22), wing=(1.3, 0.3), wing_r=0.34)}
gv.KINDS.update(KINDS)
BONES = [("item_lumbar", 0.105), ("item_lumbar", 0.103), ("item_lumbar", 0.1), ("item_lumbar", 0.097),   # tail (-X)
         ("item_thoracic", 0.092), ("item_thoracic", 0.088), ("item_thoracic", 0.084), ("item_thoracic", 0.08)]
GAP = 0.25                 # the disc between two bodies, in units of r
SWAY = 0.05                # the S of the back, sideways along the ground (m)
ROLL = 7.0                 # each vertebra rolled up to this many degrees about the spine, as a real one lies
SEED = 5


def build():
    bone = paint.make("bone.bone", "spine_bone", axis="X", density=DENSITY)
    disc = paint.make("bone.bone", "spine_disc", tones=_tones(("#342d26", "#463c32", "#594d40")), region="secondary",
                      axis="X", patches=[], edges=0.0, blotch_m=0.12, density=DENSITY)
    rng = random.Random(SEED)
    places = _places(rng)
    parts = []
    for i, ((kind, r), place) in enumerate(zip(BONES, places)):
        parts += gv.build(f"spine_{i}", place, r, bone, kind=kind, low=True, pegs=False)
    for i in range(len(BONES) - 1):
        parts.append(_disc(f"spine_disc_{i}", places[i], places[i + 1], BONES[i], BONES[i + 1], disc))
    _ground(parts)
    low, high = scene.bounds(parts)
    shapes.collider_box("col_spine", tuple(high - low), tuple((low + high) / 2))
    return parts


def _tones(hexes):
    return tuple(paint_specs.hex_to_linear(h) for h in hexes)


def _places(rng):
    """Each vertebra's frame: bodies stacked tail to neck along X with a disc between, their fronts on the ground (the
    body reaches 0.8 r in front of its middle), swaying sideways in one S, each rolled a little."""
    centres, x = [], 0.0
    for kind, r in BONES:
        h = gv.KINDS[kind]['h']
        centres.append(Vector((x + h * r / 2, 0.0, 0.8 * r)))
        x += (h + GAP) * r
    length = x - GAP * BONES[-1][1]
    for c in centres:
        c.x -= length / 2
        c.y = SWAY * math.sin(2 * math.pi * c.x / length)
    places = []
    for i, c in enumerate(centres):
        ahead = (centres[min(i + 1, len(centres) - 1)] - centres[max(i - 1, 0)]).normalized()
        roll = math.radians(rng.uniform(-ROLL, ROLL))
        up = Matrix.Rotation(roll, 3, ahead) @ Vector((0.0, 0.0, 1.0))
        places.append(gv.frame(c, ahead, up))
    return places


def _disc(name, a, b, bone_a, bone_b, material):
    """Dried cartilage filling the gap between two bodies (placed at `a` and `b`, each a (kind, r) of BONES): a squat
    ring from one end plate to the next, a little inside their rims so it reads as a dark sunk gap."""
    mesh = bmesh.new()
    rings = []
    for place, (kind, r), end in ((a, bone_a, 1), (b, bone_b, -1)):
        h = 0.5 * gv.KINDS[kind]['h'] * r * end
        rings.append([mesh.verts.new(place @ Vector((0.78 * r * math.cos(t), 0.62 * r * math.sin(t) - 0.1 * r, h)))
                      for t in (2 * math.pi * k / 6 for k in range(6))])
    gs.skin(mesh, rings, 6)
    return gs.mesh_object(name, mesh, material)



def _ground(parts):
    """Lift or drop the whole spine so its lowest point touches the ground."""
    bpy.context.view_layer.update()
    lowest = min((p.matrix_world @ v.co).z for p in parts for v in p.data.vertices)
    for part in parts:
        part.location.z -= lowest
    bpy.context.view_layer.update()
