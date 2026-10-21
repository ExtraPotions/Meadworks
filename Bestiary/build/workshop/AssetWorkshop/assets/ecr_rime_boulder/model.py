"""The Rime Giant's thrown ice boulder (Elite Creatures Reborn): the look the mod puts on the troll's thrown rock.

A chunk of glacier ice about 1.2 x 1.1 x 1.4 m (the forest troll's thrown rock is about 1.0 x 1.0 x 1.65 m) in a few
big facets, three stubby crystals breaking its outline and two dark stones frozen into it, so it reads as ice torn from
the mountain even in flight. The pivot is its middle (the mod swaps only the look and keeps the game projectile's
collider), so the origin sits inside the ice, not on the ground.
"""
import math
import os
import random
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "ecr_rimegiant"))

from mathutils import Vector  # noqa: E402

import rime_ice  # noqa: E402

TEXTURE_SIZE = 256
AO_STRENGTH = 0.2
RADII = (0.62, 0.8, 0.57)         # half-sizes along Blender x, y (its length) and z
STONE = rime_ice.srgb("5E5F5C")
LIGHT = (0.3, -0.4, 1.0)          # Blender axes: facets facing up and to the front are frosted


def build():
    rng = random.Random(1409)
    builder = rime_ice.Builder()
    builder.hull([_on_ellipsoid(rng) for _ in range(18)], rime_ice.PALE)
    for _ in range(3):
        direction = Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), rng.uniform(0.2, 1))).normalized()
        base = Vector(tuple(d * r * 0.6 for d, r in zip(direction, RADII)))
        length = rng.uniform(0.35, 0.5)
        rime_ice.crystal(builder, base, direction, length, length * 0.34, rng.uniform(0, 1), 5, root=rime_ice.MID)
    builder.close()
    builder.shade(LIGHT, 0.65, 0.1, 1409)
    for _ in range(2):
        direction = Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), rng.uniform(-1, 0.3))).normalized()
        centre = Vector(tuple(d * r * 0.85 for d, r in zip(direction, RADII)))
        rime_ice.chunk(builder, centre, (0.17, 0.15, 0.12), rng, colour=STONE, points=8)
    builder.close()
    builder.finish("ecr_rime_boulder", rime_ice.material("ecr_rime_boulder_ice"))


def _on_ellipsoid(rng):
    """A point near the surface of the boulder's ellipsoid, a little in or out so the facets are uneven."""
    theta, z = rng.uniform(0, 2 * math.pi), rng.uniform(-1, 1)
    ring = math.sqrt(1 - z * z)
    reach = rng.uniform(0.85, 1.05)
    return Vector((ring * math.cos(theta) * RADII[0] * reach, ring * math.sin(theta) * RADII[1] * reach, z * RADII[2] * reach))
