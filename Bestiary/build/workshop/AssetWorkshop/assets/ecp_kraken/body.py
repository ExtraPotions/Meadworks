"""The kraken head's one continuous skin: a thick muscular column rising out of the deep (y -5 to the crown near y 0.4),
the head with its face, and the bulbous mantle above it leaning back (widest 1.8 m, top at y 6.5).

The body is lofted from horizontal rings: TABLE gives each height's centre z, half-width and front and back depth
(smoothly interpolated), the top and bottom close as half ellipsoids, and bump() pushes the skin out for the eye mounds,
the scowling brows, the cheeks, the recess round the mouth, folds behind the eyes and lumps. Unity coordinates throughout.

Skinning: the column hangs on a spine, kh_root > kh_body_1 (y -1.0) > kh_body_2 (-2.4) > kh_body_3 (-3.8), so the mod
can bend it into an arc; it blends linearly from one bone to the next (CHAIN). From y -0.8 up it blends from kh_root into
kh_neck, which carries the face (and, from y 2.7 to 3.7, blends into kh_mantle), so the head leans on top of the column.
"""
import math

from mathutils import Vector
from mathutils.noise import noise

import head_parts as hp
from geo import U, Part, monotone, smoothstep, to_unity

SEGMENTS = 40
# y, centre z, half-width x, front depth, back depth
TABLE = [(-4.50, 0.00, 1.02, 0.90, 1.00), (-3.60, 0.00, 1.16, 0.98, 1.12), (-2.40, 0.00, 1.30, 1.04, 1.26),
         (-1.20, 0.00, 1.40, 1.08, 1.36), (-0.30, 0.03, 1.46, 1.08, 1.42), (0.23, 0.07, 1.45, 1.00, 1.40),
         (0.45, 0.10, 1.45, 0.95, 1.40), (0.70, 0.12, 1.47, 0.90, 1.44),
         (1.00, 0.13, 1.48, 0.86, 1.45), (1.25, 0.13, 1.48, 0.80, 1.45), (1.55, 0.13, 1.48, 0.84, 1.44),
         (1.85, 0.11, 1.46, 0.97, 1.42), (2.20, 0.06, 1.42, 1.06, 1.40), (2.55, -0.04, 1.36, 1.02, 1.42),
         (2.90, -0.22, 1.34, 0.95, 1.50), (3.30, -0.46, 1.52, 1.00, 1.66), (3.80, -0.74, 1.72, 1.16, 1.80),
         (4.30, -1.00, 1.80, 1.26, 1.86)]
COLUMNS = [monotone([(row[0], row[c]) for row in TABLE]) for c in range(1, 5)]
TOP_Y, TOP_RISE, TOP_LEAN = 4.30, 2.20, 1.00      # the mantle above its equator: half an ellipsoid, leaning back
BOTTOM_Y, BOTTOM_DROP = -4.50, 0.50               # the column's lower end: half an ellipsoid down to y -5
NECK_BLEND = (-0.8, 0.6)                          # kh_root below, kh_neck above
# The column's spine: each bone's rest position (all on kh_root's axis), and the height where the skin follows it alone;
# between those heights the skin blends linearly from one bone to the next.
BODY_BONES = [("kh_body_1", "kh_root", (0.0, -1.0, 0.0)), ("kh_body_2", "kh_body_1", (0.0, -2.4, 0.0)),
              ("kh_body_3", "kh_body_2", (0.0, -3.8, 0.0))]
CHAIN = [("kh_root", -0.8), ("kh_body_1", -1.7), ("kh_body_2", -2.9), ("kh_body_3", -3.8)]
MANTLE_BLEND = (2.7, 3.7)                         # kh_neck below, kh_mantle above


def section(y):
    """(centre z, half-width, front depth, back depth) of the body at height y, caps included."""
    if y > TOP_Y:
        e = min((y - TOP_Y) / TOP_RISE, 1.0)
        k = math.sqrt(max(1 - e * e, 0.0))
        cz, rx, rf, rb = (f(TOP_Y) for f in COLUMNS)
        return cz - TOP_LEAN * e, rx * k, rf * k, rb * k
    if y < BOTTOM_Y:
        e = min((BOTTOM_Y - y) / BOTTOM_DROP, 1.0)
        k = math.sqrt(max(1 - e * e, 0.0))
        cz, rx, rf, rb = (f(BOTTOM_Y) for f in COLUMNS)
        return cz, rx * k, rf * k, rb * k
    return tuple(f(y) for f in COLUMNS)


def plain_point(y, yaw):
    """Unity point on the body before bumps, and the horizontal outward direction there."""
    cz, rx, rf, rb = section(y)
    w = 0.5 + 0.5 * math.tanh(3 * math.cos(yaw))
    rz = rf * w + rb * (1 - w)
    x, z = rx * math.sin(yaw), cz + rz * math.cos(yaw)
    out = Vector((x, 0.0, z - cz))
    return Vector((x, y, z)), out.normalized() if out.length > 1e-6 else Vector((0, -1 if y < 1 else 1, 0))


def surface_along(y, yaw):
    """The plain surface point (Unity) whose direction from the axis is exactly `yaw`, and that direction."""
    cz, guess = section(y)[0], yaw
    for _ in range(8):
        p, _ = plain_point(y, guess)
        guess += yaw - math.atan2(p.x, p.z - cz)
    return plain_point(y, guess)[0], Vector((math.sin(yaw), 0.0, math.cos(yaw)))


def axis_point(y):
    return Vector((0.0, y, section(y)[0]))


def _gauss(p, centre, sigma):
    return math.exp(-((p - Vector(centre)).length / sigma) ** 2)


def bump(p, y):
    """How far the skin is pushed out at Unity point p: eye mounds, scowling brows, the recess round the beak, cheeks,
    folds behind the eyes, lumps."""
    amount = 0.0
    for bone, e in hp.EYES.items():
        eye, d = Vector(e), Vector(to_unity(hp.eye_direction(bone)))
        amount += 0.26 * _gauss(p, eye + d * 0.12, 0.5)
        sign = 1.0 if eye.x > 0 else -1.0
        outer, inner = eye + Vector((sign * 0.30, 0.58, -0.30)), eye + Vector((-sign * 0.62, 0.30, 0.45))
        amount += 0.28 * math.exp(-(_segment(p, outer, inner) / 0.2) ** 2)
        amount += 0.10 * _gauss(p, eye + Vector((-sign * 0.2, -0.62, 0.25)), 0.32)
    amount -= 0.10 * _gauss(p, (0.0, 1.25, 0.85), 0.42)
    amount += 0.08 * _gauss(p, (0.82, 1.4, 0.78), 0.3)
    back = smoothstep(0.2, -0.6, p.z - section(y)[0]) * smoothstep(2.5, 2.8, y) * (1 - smoothstep(3.4, 3.8, y))
    amount -= 0.05 * back * max(0.0, math.sin((y - 2.5) * 2 * math.pi / 0.32))
    fade = smoothstep(BOTTOM_Y - 0.4, BOTTOM_Y + 0.3, y) * (1 - smoothstep(6.2, 6.5, y))
    return amount + (0.05 + 0.05 * smoothstep(3.0, 4.5, y)) * fade * noise(p * 0.8)


def _segment(p, a, b):
    ab = b - a
    t = min(max((p - a).dot(ab) / ab.length_squared, 0.0), 1.0)
    return (p - (a + ab * t)).length


def body_weights(y):
    if y < NECK_BLEND[0]:
        return _chain_weights(y)
    n, m = smoothstep(*NECK_BLEND, y), smoothstep(*MANTLE_BLEND, y)
    weights = {"kh_root": 1 - n, "kh_neck": n * (1 - m), "kh_mantle": n * m}
    return {bone: w for bone, w in weights.items() if w > 1e-3}


def _chain_weights(y):
    """Below the neck blend: linear between neighbouring bones of the chain, each whole at its height in CHAIN."""
    for (upper, top), (lower, bottom) in zip(CHAIN, CHAIN[1:]):
        if y >= bottom:
            t = (top - y) / (top - bottom)
            return {bone: w for bone, w in ((upper, 1 - t), (lower, t)) if w > 1e-3}
    return {CHAIN[-1][0]: 1.0}


def body_rows():
    ys = [BOTTOM_Y - BOTTOM_DROP * math.sin(math.radians(a)) for a in (80, 65, 50, 35, 18)]
    ys += [BOTTOM_Y + (-1.2 - BOTTOM_Y) * i / 10 for i in range(10)]
    ys += [-1.2 + (0.23 + 1.2) * i / 7 for i in range(7)]
    count = int(round((TOP_Y - 0.23) / 0.15))
    ys += [0.23 + (TOP_Y - 0.23) * i / count for i in range(count + 1)]
    ys += [TOP_Y + TOP_RISE * math.sin(math.radians(a)) for a in (10, 20, 30, 40, 50, 60, 70, 79, 86)]
    return sorted(ys)


def body_vertex(y, yaw):
    p, out = plain_point(y, yaw)
    p = p + out * bump(p, y)
    return U(*p)


def _paint(unity, y, yaw):
    """The column's front and the throat pale, its back darker; the mantle flagged for its own bumps."""
    front, back = smoothstep(0.55, 0.98, math.cos(yaw)), smoothstep(0.2, 0.9, -math.cos(yaw))
    under = max(0.42 * front * (1 - smoothstep(0.1, 0.8, y)), 0.35 * _gauss(unity, (0.0, 0.95, 0.9), 0.5),
                smoothstep(BOTTOM_Y - 0.1, BOTTOM_Y - 0.45, y))
    return {"under": under, "mantle": smoothstep(2.6, 4.0, y), "dorsal": 0.5 * back * (1 - smoothstep(0.5, 1.5, y))}


def body():
    part = Part("body")
    rings = []
    for y in body_rows():
        ring = []
        for i in range(SEGMENTS):
            yaw = 2 * math.pi * i / SEGMENTS
            p = body_vertex(y, yaw)
            index = part.vert(p, body_weights(y), **_paint(Vector(to_unity(p)), y, yaw))
            part.uv_scales[index] = 0.55 + 0.45 * smoothstep(-0.8, 0.4, y)   # the column gets less of the atlas
            ring.append(index)
        rings.append(ring)
    for a, b in zip(rings, rings[1:]):
        part.strip(a, b)
    bottom = part.vert(U(0, BOTTOM_Y - BOTTOM_DROP, section(BOTTOM_Y)[0]), body_weights(-5), under=1.0)
    part.uv_scales[bottom] = 0.55
    part.cap_start(rings[0], bottom)
    top_cz = section(TOP_Y + TOP_RISE)[0]
    part.cap_end(rings[-1], part.vert(U(0, TOP_Y + TOP_RISE, top_cz), body_weights(6.5), mantle=1.0))
    return part
