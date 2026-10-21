"""Sucker cups for the tentacle and the head's arms: a pale raised rim round a dark pit, sunk into the skin.

The game mesh carries a simple cup (walls and a dished top, no base: it sits inside the skin); the bake mesh a detailed
one (rim, inner lip, pit) whose paint is baked onto the game mesh's texture, so small suckers exist only as paint.
"""
from mathutils import Vector

from geo import frame


def cup(part, center, normal, along, radius, segments, weights, detailed):
    """A cup on the skin at `center`, facing `normal`; `weights(p)` gives each vertex's bone weights."""
    s, n, t = frame(normal, along)          # T is the cup's axis (out of the skin), N along the arm
    c, r = Vector(center), radius

    def ring(height, size, **paint):
        return part.ring(c + t * (height * r), s, n, size * r, segments, lambda p, a: weights(p), **paint)

    base = ring(-0.35, 0.90, rim=1.0)
    rim = ring(0.30, 1.0, rim=1.0)
    part.strip(base, rim)
    if not detailed:
        pit = part.vert(c + t * (0.04 * r), weights(c), pit=1.0)
        part.cap_end(rim, pit)
        return
    lip = ring(0.34, 0.64, rim=1.0)
    hole = ring(0.10, 0.44, pit=1.0)
    bottom = part.vert(c + t * (0.0 * r), weights(c), pit=1.0)
    part.strip(rim, lip)
    part.strip(lip, hole)
    part.cap_end(hole, bottom)


def rows(start, end, length, radius_at, spacing=1.25):
    """
    Sucker positions along an arm of `length` metres from fraction `start` to `end`: two alternating rows, each sucker
    `spacing` sucker radii after the previous one. radius_at(u) is the sucker radius at fraction u.
    Yields (u, row) with row 0 or 1.
    """
    u, row = start, 0
    while u <= end:
        yield u, row
        u += spacing * radius_at(u) / length
        row = 1 - row
