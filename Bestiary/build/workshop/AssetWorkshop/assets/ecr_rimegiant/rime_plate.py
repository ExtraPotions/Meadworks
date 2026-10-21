"""The Rime Giant's rime plates (Elite Creatures Reborn): eight chunky slabs of ice crystals, each built to the skin it
sits on. assets/ecr_rime_plate_<n>/model.py builds plate n.

A plate is made in its own frame, the one RimeFit measured on the game's Troll: x across, y up the body or the limb,
z out of the skin, the origin on the skin at the plate's middle. Its back follows the skin, sunk a few centimetres in so
no gap shows as the skin flexes; its outline is an irregular oval kept where the skin is measured, not too steep and
not too far off the middle's height (a plate on a shoulder wraps it, it does not reach down the arm). On top, a
chunky lens of ice in a few big facets, frosted at the rim, a lump or two of ice, a big crystal with a few smaller ones
leaning up the body, and two shards along the rim so its outline reads from far off; every facet is then shaded by
which way it faces (low-poly and chunky, like the game's crystals). The mod breaks plates off one at a time and lets
them tumble away, so each is a whole chunk: closed, with a deep blue-grey underside.
"""
import math
import random

from mathutils import Vector

import rime_fit
import rime_ice
from rime_ice import DEEP, FROST, MID, PALE, mix

TEXTURE_SIZE = 256
AO_STRENGTH = 0.15
EMBED = 0.05                     # the back sits this far into the skin
SIDES = 9                        # corners of the outline
RINGS = (0.0, 0.55, 0.95)        # the top's rings, as fractions of the outline; the rim is the last
TOP_COLOURS = (mix(MID, PALE, 0.5), PALE, FROST)
ROOT = mix(MID, PALE, 0.5)       # a crystal's colour where it grows out of the slab
LIGHT = (0.3, 0.8, 0.55)         # facets facing this way (across, up, out) are frosted, facing away deep blue
LEAN_UP = {0: (-0.45, -0.1)}     # how far up the body crystals lean; the chest's lean down, clear of the sleeper's chin


def build(index):
    plate = rime_fit.load()["plates"][index]
    rng = random.Random(7919 * (index + 1))
    skin = rime_fit.plate_skin(plate)
    shape = Shape(plate, skin, rng)
    builder = rime_ice.Builder(lambda p: rime_fit.blender(*p))
    rim = _top(builder, shape, rng)
    _underside(builder, shape, rim)
    _chunks(builder, shape, rng)
    _crystals(builder, shape, rng)
    _shards(builder, shape, rng)
    builder.close()
    builder.shade(LIGHT, 0.35, 0.1, index)
    builder.finish(plate["name"], rime_ice.material(plate["name"] + "_ice"))


class Shape:
    """The plate's outline on the skin and its thickness."""

    def __init__(self, plate, skin, rng):
        self.index = plate["index"]
        self.skin = skin
        self.size = min(plate["width"], plate["height"])
        self.area = plate["width"] * plate["height"]
        self.thick = 0.18 + 0.12 * self.size
        a, b = plate["width"] * 0.5, plate["height"] * 0.5
        limit = 0.5 * min(plate["width"], plate["height"])
        self.angles = [2 * math.pi * k / SIDES + rng.uniform(-0.18, 0.18) for k in range(SIDES)]
        self.radii = [self._fits(math.cos(t), math.sin(t), rng.uniform(0.85, 1.0) / math.hypot(math.cos(t) / a, math.sin(t) / b), limit)
                      for t in self.angles]

    def _fits(self, c, s, reach, limit, steps=16):
        """How far out along a direction the skin can carry the plate."""
        fit = 0.0
        for i in range(1, steps + 1):
            r = reach * i / steps
            depth, steep = self.skin.at(r * c, r * s), rime_fit.slope(self.skin, r * c, r * s)
            if depth is None or abs(depth) > limit or steep is None or steep > 2.8:
                break
            fit = r
        return max(fit, reach * 0.3)

    def point(self, fraction, k):
        """(x, y) on the outline's k-th spoke, `fraction` of the way out."""
        r = self.radii[k % SIDES] * fraction
        return r * math.cos(self.angles[k % SIDES]), r * math.sin(self.angles[k % SIDES])

    def skin_at(self, x, y):
        depth = self.skin.at(x, y)
        return depth if depth is not None else 0.0

    def top(self, fraction):
        """How far the top stands off the skin at a fraction of the way out: full in the middle, a third at the rim."""
        return self.thick * (0.35 if fraction >= RINGS[-1] else 1.0 - 0.55 * fraction * fraction)


def _top(builder, shape, rng):
    rings = []
    for i, fraction in enumerate(RINGS):
        count = 1 if fraction == 0.0 else SIDES
        ring = []
        for k in range(count):
            x, y = shape.point(fraction, k)
            ring.append(builder.vert((x, y, shape.skin_at(x, y) + shape.top(fraction) * rng.uniform(0.85, 1.15))))
        rings.append(ring)
    for k in range(SIDES):
        n = (k + 1) % SIDES
        builder.face([rings[0][0], rings[1][k], rings[1][n]], [TOP_COLOURS[0], TOP_COLOURS[1], TOP_COLOURS[1]])
    for i in range(1, len(rings) - 1):
        _band(builder, rings[i], rings[i + 1], TOP_COLOURS[i], TOP_COLOURS[i + 1], rng)
    return rings[-1]


def _underside(builder, shape, rim):
    """The back, following the skin just inside it, and the sides from the frosted rim down to it."""
    rings = []
    for fraction in (0.0, 0.5, 1.0):
        points = [shape.point(fraction, k) for k in range(1 if fraction == 0.0 else SIDES)]
        rings.append([builder.vert((x, y, shape.skin_at(x, y) - EMBED)) for x, y in points])
    for k in range(SIDES):
        n = (k + 1) % SIDES
        builder.face([rings[0][0], rings[1][n], rings[1][k]], [DEEP] * 3)
        builder.face([rings[1][k], rings[1][n], rings[2][n], rings[2][k]], [DEEP] * 4)
        builder.face([rim[k], rim[n], rings[2][n], rings[2][k]], [FROST, FROST, MID, MID])


def _band(builder, inner, outer, inner_colour, outer_colour, rng):
    """Quads between two rings, each split into two triangles along a random diagonal: facets."""
    for k in range(SIDES):
        n = (k + 1) % SIDES
        a, b, c, d = inner[k], outer[k], outer[n], inner[n]
        if rng.random() < 0.5:
            builder.face([a, b, c], [inner_colour, outer_colour, outer_colour])
            builder.face([a, c, d], [inner_colour, outer_colour, inner_colour])
        else:
            builder.face([a, b, d], [inner_colour, outer_colour, inner_colour])
            builder.face([b, c, d], [outer_colour, outer_colour, inner_colour])


def _chunks(builder, shape, rng):
    """One or two big faceted lumps of ice half sunk into the lens."""
    for _ in range(rng.randint(1, 2)):
        x, y = shape.point(rng.uniform(0.2, 0.55), rng.randrange(SIDES))
        centre = (x, y, shape.skin_at(x, y) + shape.top(0.4) * 0.7)
        radii = (shape.size * rng.uniform(0.2, 0.28), shape.size * rng.uniform(0.16, 0.24), shape.thick * rng.uniform(0.7, 1.0))
        rime_ice.chunk(builder, centre, radii, rng, points=9)


def _crystals(builder, shape, rng):
    """One big crystal near the middle with one or two smaller ones beside it, rooted in the slab, leaning out and up
    the body: few, fat, clearly outlined, the slab still the plate."""
    cx, cy = shape.point(rng.uniform(0.0, 0.3), rng.randrange(SIDES))
    lean = Vector((rng.uniform(-0.35, 0.35), rng.uniform(*LEAN_UP.get(shape.index, (0.3, 0.7))), 1.0))
    _crystal(builder, shape, cx, cy, lean, rng.uniform(2.0, 2.4), rng)
    for _ in range(rng.randint(1, 2)):
        x, y = shape.point(rng.uniform(0.3, 0.55), rng.randrange(SIDES))
        axis = lean + Vector((x, y, 0.0)).normalized() * 0.5 + Vector((rng.uniform(-0.2, 0.2), rng.uniform(-0.2, 0.2), 0.0))
        _crystal(builder, shape, x, y, axis, rng.uniform(1.1, 1.5), rng)


def _crystal(builder, shape, x, y, axis, scale, rng):
    base = Vector((x, y, shape.skin_at(x, y) + shape.thick * 0.3))
    length = shape.thick * scale
    rime_ice.crystal(builder, base, axis, length, length * rng.uniform(0.26, 0.32), rng.uniform(0, 1), rng.choice((5, 6)), root=ROOT)


def _shards(builder, shape, rng):
    """Two flat shards along the rim, pointing out sideways, to break the outline."""
    for k in rng.sample(range(SIDES), 2):
        x, y = shape.point(0.8, k)
        base = Vector((x, y, shape.skin_at(x, y) + shape.thick * 0.3))
        axis = Vector((x, y, 0.0)).normalized() + Vector((0.0, 0.0, rng.uniform(0.5, 0.9)))
        length = shape.thick * rng.uniform(1.1, 1.6)
        rime_ice.crystal(builder, base, axis, length, length * 0.28, rng.uniform(0, 1), 5, root=ROOT)
