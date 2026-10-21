"""The kraken head's pieces other than the body: beak, lips, throat, siphon, eyes and lids, barnacles.

All positions are Unity's (see geo.U); every piece carries its bone weights and paint.
"""
import math

from mathutils import Vector

from geo import U, Part, frame, smoothstep

HINGE = (0.0, 1.25, 1.0)
EYE_RADIUS = 0.5
EYES = {"kh_eye_l": (-1.2, 2.2, 0.55), "kh_eye_r": (1.2, 2.2, 0.55)}
SIPHON_BASE, SIPHON_LENGTH = (0.80, 1.40, 0.66), 0.72
SIPHON_AIM = (0.18, 0.12, 0.68)
LIPS_CENTER = (0.0, 1.25, 0.90)
MOUTH = (0.0, 1.02, 1.6)


def eye_direction(bone):
    """Forward and out to the side, a little down."""
    side = 1.0 if bone == "kh_eye_r" else -1.0
    yaw, pitch = math.radians(50), math.radians(-4)
    return U(side * math.sin(yaw) * math.cos(pitch), math.sin(pitch), math.cos(yaw) * math.cos(pitch)).normalized()


def siphon_tip():
    aim = Vector(SIPHON_AIM).normalized() * SIPHON_LENGTH
    return tuple(Vector(SIPHON_BASE) + aim)


# ---------------------------------------------------------------- beak

def _beak_path(upper, s):
    """Centre line (Unity y, z) of a beak half at s (0 base, 1 tip)."""
    if upper:
        return 1.27 + 0.08 * s - 0.38 * s ** 2.3, 0.52 + 1.10 * s
    return 1.23 - 0.12 * s - 0.02 * s * s, 0.56 + 0.90 * s


def _beak_size(upper, s):
    if upper:
        return 0.50 * (1 - s) ** 0.7 + 0.03, 0.36 * (1 - s) ** 0.8 + 0.04
    return 0.41 * (1 - s) ** 0.7 + 0.03, 0.28 * (1 - s) ** 0.8 + 0.03


def _beak_section(upper, w, h, count_top=9, count_bottom=7):
    """Crescent cross-section as (x, y, is_outer): an arch (up for the upper half, down for the lower) over a shallow
    concave inner face, meeting in sharp cutting edges."""
    sign = 1.0 if upper else -1.0
    pts = [(w * math.cos(math.pi * i / (count_top - 1)), sign * h * math.sin(math.pi * i / (count_top - 1)) ** 0.8, True)
           for i in range(count_top)]
    for i in range(1, count_bottom + 1):
        a = math.pi + math.pi * i / (count_bottom + 1)
        pts.append((w * math.cos(a), -sign * 0.3 * h * math.sin(a), False))
    return pts


def beak(upper):
    """One half of the parrot beak, weighted to its bone. Rest pose: closed."""
    bone = "kh_beak_upper" if upper else "kh_beak_lower"
    part = Part("beak_upper" if upper else "beak_lower", sharp_angle=50)
    steps = 14
    rings = []
    for k in range(steps + 1):
        s = k / steps
        y, z = _beak_path(upper, s)
        y2, z2 = _beak_path(upper, min(s + 0.01, 1.0) if s < 1 else s)
        y1, z1 = _beak_path(upper, max(s - 0.01, 0.0))
        tangent = U(0, y2 - y1, z2 - z1)
        sx, ny, _ = frame(tangent, U(0, 1, 0))
        w, h = _beak_size(upper, s)
        centre = U(0, y, z)
        ring = []
        for x, yy, outer in _beak_section(upper, w, h):
            edge = abs(x) / max(w, 1e-6)
            top = abs(yy) / max(h, 1e-6) if outer else 0.0
            p = centre + sx * x + ny * yy
            ring.append(part.vert(p, {bone: 1.0}, beak=1.0, amber=(1 - s) ** 1.6 * (0.45 + 0.55 * edge ** 3),
                                  gloss=(smoothstep(0.82, 1.0, top) * (0.35 + 0.65 * s) if upper and outer else 0.0),
                                  inner=0.0 if outer else 0.85))
        rings.append(ring)
    for a, b in zip(rings, rings[1:]):
        part.strip(a, b)
    y, z = _beak_path(upper, 0)
    part.cap_start(rings[0], part.vert(U(0, y, z - 0.02), {bone: 1.0}, beak=1.0, amber=1.0))
    y, z = _beak_path(upper, 1)
    part.cap_end(rings[-1], part.vert(U(0, y - 0.01, z + 0.015), {bone: 1.0}, beak=1.0))
    return part


# ---------------------------------------------------------------- lips, throat

def torus(name, centre, axis, up, radii, minor, major_count, minor_count, weights, paint_fn, roll=0.0):
    """
    A closed elliptical torus round `axis` (Blender vectors): `radii` is the hole's (inner edge) half-width and
    half-height, `minor` the tube's radius, a number or f(major angle). `roll` turns the ellipse about the axis (radians).
    paint_fn(major_angle, minor_angle) -> paint dict.
    """
    part = Part(name)
    s0, n0, t = frame(axis, up)
    s, n = s0 * math.cos(roll) + n0 * math.sin(roll), n0 * math.cos(roll) - s0 * math.sin(roll)
    rings = []
    for j in range(major_count):
        th = 2 * math.pi * j / major_count
        m = minor(th) if callable(minor) else minor
        a, b = radii[0] + m, radii[1] + m
        mid = centre + s * (a * math.cos(th)) + n * (b * math.sin(th))
        radial = (s * (math.cos(th) / a) + n * (math.sin(th) / b)).normalized()
        ring = []
        for i in range(minor_count):
            ph = 2 * math.pi * i / minor_count
            p = mid + radial * (m * math.cos(ph)) + t * (m * math.sin(ph))
            ring.append(part.vert(p, weights, **paint_fn(th, ph)))
        rings.append(ring)
    for j in range(major_count):
        part.strip(rings[j], rings[(j + 1) % major_count])
    return part


def lips():
    """The fleshy collar round the beak's base; its inner edge dark where it meets the mouth."""
    return torus("lips", U(*LIPS_CENTER), U(0, 0, 1), U(0, 1, 0), (0.44, 0.40), lambda th: 0.15 + 0.025 * math.sin(7 * th),
                 22, 10, {"kh_neck": 1.0}, lambda th, ph: {"lip": 1.0, "inner": smoothstep(0.2, 0.9, -math.cos(ph)) * 0.9})


def ellipsoid(name, centre, radii, weights, segments=12, rows=8, **paint):
    """A closed ellipsoid (Unity centre and radii)."""
    part = Part(name)
    s, n, t = frame(U(0, 1, 0), U(0, 0, 1))          # poles along Unity Y
    c = U(*centre)
    rs = [part.ring(c + t * (-radii[1] * math.cos(math.pi * k / rows)), s, n,
                    lambda a, k=k: (radii[0] * math.sin(math.pi * k / rows), radii[2] * math.sin(math.pi * k / rows)),
                    segments, weights, **paint) for k in range(1, rows)]
    for a, b in zip(rs, rs[1:]):
        part.strip(a, b)
    part.cap_start(rs[0], part.vert(c + t * (-radii[1]), weights, **paint))
    part.cap_end(rs[-1], part.vert(c + t * radii[1], weights, **paint))
    return part


def throat():
    """Dark flesh behind the beak, what shows between the open jaws."""
    return ellipsoid("throat", (0.0, 1.25, 0.52), (0.50, 0.46, 0.16), {"kh_neck": 1.0}, inner=1.0)


# ---------------------------------------------------------------- siphon

SIPHON_PROFILE = [(0.0, 0.30), (0.20, 0.26), (0.42, 0.20), (0.58, 0.195), (0.66, 0.225), (0.70, 0.235),
                  (0.72, 0.20), (0.70, 0.15), (0.62, 0.13), (0.45, 0.11), (0.35, 0.10)]


def siphon():
    """The funnel on the right of the face: a lathed tube, hollow at its mouth, all on kh_siphon."""
    part = Part("siphon")
    axis = U(*SIPHON_AIM).normalized()
    s, n, t = frame(axis, U(0, 1, 0))
    base = U(*SIPHON_BASE)
    rings = []
    for index, (d, r) in enumerate(SIPHON_PROFILE):
        inside = index >= 7
        w = {"kh_siphon": 1.0} if inside else {"kh_siphon": smoothstep(0.0, 0.25, d), "kh_neck": 1 - smoothstep(0.0, 0.25, d)}
        rings.append(part.ring(base + t * d, s, n, r, 12, w, inner=1.0 if inside else 0.0,
                               lip=smoothstep(0.55, 0.72, d) * 0.6 if not inside else 0.0, under=0.15))
    for a, b in zip(rings, rings[1:]):
        part.strip(a, b)
    part.cap_start(rings[0], part.vert(base - t * 0.02, {"kh_neck": 1.0}))
    part.cap_end(rings[-1], part.vert(base + t * 0.33, {"kh_siphon": 1.0}, inner=1.0))
    return part


# ---------------------------------------------------------------- eyes

# The lid's cross-section (outward from the eye's axis, along its gaze from the eye's centre), as a closed loop: from
# inside the eyeball out over the rounded lid edge, back over the socket into the skin, and back inside.
LID_PROFILE = [(0.00, 0.22), (0.02, 0.34), (0.06, 0.39), (0.12, 0.39), (0.18, 0.33), (0.23, 0.20), (0.25, 0.02),
               (0.15, -0.08), (0.05, 0.06)]


def eyelid(bone):
    """
    The eye's socket: a fleshy almond lid round the eyeball that slopes back into the skin (no rim standing proud),
    the upper lid heavy and low over the eye, the inner corner dropped into a scowl; dark on its inner edge.
    """
    d = eye_direction(bone)
    sign = 1.0 if bone == "kh_eye_r" else -1.0
    s0, n0, t = frame(d, U(0, 1, 0))
    roll = sign * math.radians(14)
    s, n = s0 * math.cos(roll) + n0 * math.sin(roll), n0 * math.cos(roll) - s0 * math.sin(roll)
    centre = U(*EYES[bone]) + U(0, -0.04, 0)
    part = Part("lid_" + bone)
    rings = []
    count = 22
    for j in range(count):
        th = 2 * math.pi * j / count
        hole = (0.37 * math.cos(th), (0.20 if math.sin(th) > 0 else 0.27) * math.sin(th))
        heavy = 1.0 + 0.3 * max(0.0, math.sin(th))
        out = (s * math.cos(th) + n * math.sin(th)).normalized()
        edge = centre + s * hole[0] + n * hole[1]
        ring = [part.vert(edge + out * (r * heavy) + t * (a - (0.03 if r < 0.2 else 0.0) * heavy), {"kh_neck": 1.0},
                          lid=smoothstep(0.1, 0.02, r) if a > 0.25 else 0.0)
                for r, a in LID_PROFILE]
        rings.append(ring)
    for j in range(count):
        part.strip(rings[j], rings[(j + 1) % count])
    return part


def eyeball(bone):
    """The eye, its own mesh on its own bone, planar UVs along its gaze: the texture's centre is the pupil."""
    part = Part("eye_" + bone)
    d = eye_direction(bone)
    s, n, t = frame(d, U(0, 1, 0))
    c, r = U(*EYES[bone]), EYE_RADIUS
    rows = 12
    rings = [part.ring(c + t * (-r * math.cos(math.pi * k / rows)), s, n, r * math.sin(math.pi * k / rows), 18,
                       {bone: 1.0}) for k in range(1, rows)]
    for a, b in zip(rings, rings[1:]):
        part.strip(a, b)
    part.cap_start(rings[0], part.vert(c - t * r, {bone: 1.0}))
    part.cap_end(rings[-1], part.vert(c + t * r, {bone: 1.0}))
    part.uv_frame = (c, s, n, r)
    return part


# ---------------------------------------------------------------- barnacles

def barnacle(part, point, normal, radius, weights):
    """A small volcano of shell: base ring sunk into the skin, a narrower rim, a crater in the middle.
    weights(p) gives the bone weights at a Blender point."""
    s, n, t = frame(normal, U(0, 1, 0) if abs(normal.z) < 0.9 else U(0, 0, 1))
    c = Vector(point)
    base = part.ring(c - t * (0.3 * radius), s, n, 0.95 * radius, 7, lambda p, a: weights(p), barn=1.0)
    rim = part.ring(c + t * (0.7 * radius), s, n, 0.55 * radius, 7, lambda p, a: weights(p), barn=1.0, phase=0.3)
    part.strip(base, rim)
    crater = c + t * (0.38 * radius)
    part.cap_end(rim, part.vert(crater, weights(crater), barn=1.0, crater=1.0))
