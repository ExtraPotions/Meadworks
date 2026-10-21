"""The haft: a whole spine, tailbone to neck, following the S of a living back in the blade's plane, built the way
the game builds its Spinesnap bow: few, chunky vertebrae, each a drum with one big wedge of a spine and two stubs.

Axe coordinates: Z up the haft, the blade's edge towards -Y, the spikes (spinous processes) along the back (+Y), the
side processes out of the blade's flats (+-X). From the bottom: coccyx (the butt), sacrum (the pommel, sinew bound on
it), four lumbar vertebrae (hatchet spines), eight thoracic (long spines sloping down) and four cervical (short) up
into the skull. Two stretches are wrapped in leather for the hands.
"""
import math
import random

import bmesh

from mathutils import Vector

import axe_mesh as geo

# The centre line through (y, z): tail curling back, lumbar bowing forward, thoracic back, the neck forward again.
CURVE = [(0.034, 0.000), (0.014, 0.060), (-0.004, 0.140), (-0.024, 0.240), (-0.058, 0.430), (-0.046, 0.630),
         (0.010, 0.860), (0.060, 1.080), (0.050, 1.300), (0.002, 1.480), (-0.030, 1.620), (-0.022, 1.735)]
SACRUM = (0.075, 0.250)                      # arc length from the tail tip, bottom to top
# (count, spacing, radius, body height, kind), bottom to top, starting where the sacrum ends
REGIONS = [(4, 0.104, 0.041, 0.086, "lumbar"), (8, 0.092, 0.036, 0.076, "thoracic"), (4, 0.068, 0.030, 0.056, "cervical")]
SPINES = {"lumbar": (0.055, 12, 0.050), "thoracic": (0.082, 45, 0.046), "cervical": (0.040, 20, 0.030)}   # length, slope, tall
STUBS = {"lumbar": (0.052, 0.25), "thoracic": (0.036, 0.60), "cervical": (0.026, 0.20)}              # length, sweep back
GRIPS = [(0.300, 0.520), (0.930, 1.150)]     # arc lengths wrapped in leather: the lower and the upper hand
SEED = 7


class Centre:
    """The centre line resampled by arc length: point and frame (x = side, y = back, z = up the haft) at any s."""

    def __init__(self):
        pts = geo.catmull([(0.0, y, z) for y, z in CURVE], 600)
        self.points, self.lengths = pts, [0.0]
        for a, b in zip(pts, pts[1:]):
            self.lengths.append(self.lengths[-1] + (b - a).length)
        self.total = self.lengths[-1]

    def at(self, s):
        i = max(1, min(len(self.points) - 1, next((k for k, l in enumerate(self.lengths) if l >= s), len(self.points) - 1)))
        t = (s - self.lengths[i - 1]) / max(1e-9, self.lengths[i] - self.lengths[i - 1])
        point = self.points[i - 1].lerp(self.points[i], min(1.0, max(0.0, t)))
        up = (self.points[i] - self.points[i - 1]).normalized()
        side = Vector((1.0, 0.0, 0.0))
        return point, side, up.cross(side).normalized(), up

    def matrix(self, s, roll=0.0):
        point, side, back, up = self.at(s)
        c, n = math.cos(roll), math.sin(roll)
        return geo.frame_matrix(point, side * c + back * n, back * c - side * n, up)


def build(mats):
    """Every bone of the spine, the discs between, the pommel ring and the grips. Returns the centre line."""
    centre, rng = Centre(), random.Random(SEED)
    _pommel(centre, mats)
    s = SACRUM[1]
    for count, spacing, radius, height, kind in REGIONS:
        for k in range(count):
            s += spacing / 2
            _vertebra(centre, s, radius, height, kind, _gripped(s), rng, mats, f"{kind}_{k}")
            s += spacing / 2
            if k < count - 1 or kind != "cervical":
                _disc(centre, s, radius, mats["sinew"], f"disc_{kind}_{k}")
    for i, (a, b) in enumerate(GRIPS):
        _grip(centre, a, b, mats, i)
    _binding(centre, top(centre) - 0.022, 0.030, 0.034, mats["sinew"], "neck_binding")
    return centre


def top(centre):
    """Arc length of the last cervical vertebra's top: where the skull sits."""
    return SACRUM[1] + sum(n * spacing for n, spacing, *_ in REGIONS)


def _gripped(s):
    return any(a - 0.02 <= s <= b + 0.02 for a, b in GRIPS)


def _vertebra(centre, s, r, h, kind, gripped, rng, mats, name):
    """Drum, spine and stubs in the vertebra's own frame, then placed on the centre line."""
    r, h = r * rng.uniform(0.95, 1.05), h * rng.uniform(0.96, 1.04)
    m = centre.matrix(s, roll=math.radians(rng.uniform(-5, 5)))
    parts = [_body(r, h, mats["bone"], name), _spinous(r, kind, gripped, rng, mats["bone"], name)]
    parts += _transverse(r, kind, mats["bone"], name)
    for part in parts:
        geo.place(part, m)


def _body(r, h, mat, name):
    """A drum, a little waisted, rims at both ends."""
    rows = [(-0.50, 0.90), (-0.32, 1.0), (0.0, 0.90), (0.32, 1.0), (0.50, 0.90)]
    stations = [(h * z, r * k, r * k * 0.90, 0.0, -0.05 * r) for z, k in rows]
    return geo.loft_z(name + "_body", stations, mat, sides=8, power=0.9)


def _spinous(r, kind, gripped, rng, mat, name):
    """One big wedge out of the back: hatchets on the lumbar, long down-sloping blades on the thoracic."""
    length, slope, tall = SPINES[kind]
    length *= (0.55 if gripped else 1.0) * rng.uniform(0.9, 1.1)
    slope = math.radians(slope + rng.uniform(-5, 5))
    base = Vector((0.0, 0.55 * r, 0.0))
    ahead = Vector((0.0, math.cos(slope), -math.sin(slope)))
    reach = 0.45 * r + length
    points = [base, base + ahead * reach * 0.5, base + ahead * reach]
    radii = [(tall / 2, 0.012), (tall * 0.32, 0.008), (0.002, 0.002)]
    return geo.sweep(f"{name}_spine", points, radii, mat, hint=(1, 0, 0), sides=4, smooth=True)


def _transverse(r, kind, mat, name):
    """Two stubs to the sides, sloping back."""
    length, back = STUBS[kind]
    parts = []
    for side in (-1, 1):
        base = Vector((side * 0.55 * r, 0.45 * r, 0.0))
        tip = base + Vector((side * (0.45 * r + length), length * back, length * 0.2))
        radii = [(0.013, 0.009), (0.004, 0.003)]
        parts.append(geo.sweep(f"{name}_side_{side}", [base, tip], radii, mat, hint=(0, 0, 1), sides=4, smooth=True))
    return parts


def _disc(centre, s, r, mat, name):
    """Dried cartilage between two drums: a dark ring, a little sunk."""
    stations = [(-0.008, r * 0.80, r * 0.72, 0.0, -0.05 * r), (0.0, r * 0.86, r * 0.77, 0.0, -0.05 * r),
                (0.008, r * 0.80, r * 0.72, 0.0, -0.05 * r)]
    geo.place(geo.loft_z(name, stations, mat, sides=8), centre.matrix(s))


def _pommel(centre, mats):
    """Tailbone knuckles to a point, the sacrum (a broad wedge across the flats, wings at the top, a crest of knobs down
    its back, dark foramina down its front), a sinew binding where the lumbar begin."""
    for i, (s, radius) in enumerate([(0.018, 0.012), (0.040, 0.016), (0.062, 0.020)]):
        point, side, back, up = centre.at(s)
        geo.knob(f"coccyx_{i}", point, (radius * 1.2, radius, radius * 0.95), mats["bone"], segments=6, rings=4)
    point, side, back, up = centre.at(0.006)
    geo.sweep("coccyx_tip", [point + up * 0.006, point - up * 0.020], [(0.009, 0.008), (0.001, 0.001)], mats["bone"],
              sides=4, smooth=True)
    stations = [(0.070, 0.022, 0.017), (0.130, 0.046, 0.024), (0.190, 0.064, 0.028), (0.232, 0.070, 0.030),
                (0.254, 0.056, 0.030)]
    _sacrum(centre, stations, mats["bone"])
    for side_sign in (-1, 1):
        point, side, back, up = centre.at(0.232)
        geo.knob(f"sacral_wing_{side_sign}", point + side * side_sign * 0.068 + back * 0.004, (0.024, 0.021, 0.026),
                 mats["bone"], 6, 4)
    _sacral_marks(centre, mats)
    _binding(centre, SACRUM[1] + 0.012, 0.030, 0.042, mats["sinew"], "pommel_binding")


def _sacral_marks(centre, mats):
    """The crest of knobs down the back and the paired foramina, dark, down the front."""
    for k in range(3):
        s = 0.120 + 0.045 * k
        point, side, back, up = centre.at(s)
        depth = 0.020 + 0.010 * k / 2
        geo.knob(f"sacral_crest_{k}", point + back * (depth + 0.004), (0.009, 0.011, 0.013), mats["bone"], 6, 4)
        for side_sign in (-1, 1):
            at = point + side * side_sign * (0.018 + 0.008 * k) - back * (depth - 0.001)
            geo.knob(f"sacral_hole_{k}_{side_sign}", at, (0.008, 0.003, 0.007), mats["socket"], 6, 3)


def _sacrum(centre, stations, mat):
    """A lofted wedge whose sections follow the centre line (each station: arc length, half width, half depth)."""
    mesh = bmesh.new()
    rings = []
    for s, a, b in stations:
        point, side, back, up = centre.at(s)
        rings.append(geo.ring(mesh, point - back * 0.004, side, back, a, b, 8, 0.0, 0.8))
    geo.skin(mesh, rings)
    return geo.obj_from("sacrum", mesh, mat, smooth=True)


def _binding(centre, s, span, radius, mat, name):
    """Sinew wound round the column: `span` metres of turns centred on arc length s, each turn a bulge."""
    turns = max(2, int(span / 0.009))
    stations = []
    for k in range(2 * turns + 1):
        z = -span / 2 + span * k / (2 * turns)
        swell = radius + (0.004 if k % 2 else 0.0)
        stations.append((z, swell, swell * 0.95, 0.0, 0.0))
    geo.place(geo.loft_z(name, stations, mat, sides=8), centre.matrix(s))


def _grip(centre, a, b, mats, index):
    """Leather wound round the column between a and b, like the Spinesnap's grip: a plain dark sleeve, swelling a
    little in the middle, bound in sinew at each end; the spines poke out through it."""
    radius = 0.046 if index == 0 else 0.042
    steps = 6
    mesh = bmesh.new()
    rings = []
    for k in range(steps + 1):
        point, side, back, up = centre.at(a + (b - a) * k / steps)
        swell = 1.0 - 0.10 * (1.0 - math.sin(math.pi * k / steps))
        rings.append(geo.ring(mesh, point, side, back, radius * swell, radius * 0.93 * swell, 8, math.pi / 8))
    geo.skin(mesh, rings)
    geo.obj_from(f"grip_{index}", mesh, mats["leather"], smooth=True)
    for end in (a, b):
        _binding(centre, end, 0.022, radius, mats["sinew"], f"grip_{index}_tie_{end}")
