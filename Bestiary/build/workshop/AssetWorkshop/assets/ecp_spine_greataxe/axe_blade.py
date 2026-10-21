"""The blade: a giant's jawbone for a bearded crescent, its teeth the cutting edge. The jaw is drawn as four outlines in
the axe's plane (head coordinates, y towards the edge negative) and filled as a Coons patch, so the neck narrows, the
horn sweeps up and the beard hangs long and hooks back like the game's blackmetal battleaxe and Skullsplittur.

Rows run root (v = 0) to edge (v = 1): thick at the root, a rounded lip along the top line and the beard, thinner
across the face, then swelling again into the ridge that holds the teeth: a row of square-crowned ivory teeth with two
long fangs (one broken off). Coarse on purpose, like the game's bone gear: recognisable parts, the shape carries it,
the paint does the rest.
"""
import math

import bmesh
from mathutils import Vector

import axe_mesh as geo

ROOT = [(-0.110, 0.080), (-0.110, -0.090)]
TOP = [(-0.110, 0.080), (-0.210, 0.052), (-0.300, 0.064), (-0.370, 0.118), (-0.420, 0.210), (-0.445, 0.300),
       (-0.452, 0.392)]
EDGE = [(-0.452, 0.392), (-0.540, 0.312), (-0.612, 0.188), (-0.652, 0.040), (-0.656, -0.110), (-0.622, -0.260),
        (-0.555, -0.390), (-0.460, -0.490), (-0.360, -0.556), (-0.285, -0.590)]
BEARD = [(-0.110, -0.090), (-0.205, -0.076), (-0.250, -0.120), (-0.268, -0.215), (-0.270, -0.330), (-0.270, -0.450),
         (-0.278, -0.540), (-0.285, -0.590)]
ACROSS = [0.0, 0.10, 0.28, 0.50, 0.72, 0.86, 0.94, 1.0]
THICK = [0.090, 0.074, 0.058, 0.048, 0.050, 0.060, 0.062, 0.046]
LUMPS = 0.005                                # how far the faces heave, knobbly, short of the edge
ALONG = 30
LIP = (0.020, 0.012)                         # extra thickness along the top line and the beard, outermost rows in
TEETH = [(0.05, 0.040, 0.022, 0), (0.13, 0.048, 0.027, 0), (0.21, 0.052, 0.029, 0), (0.29, 0.105, 0.028, 1),
         (0.37, 0.052, 0.030, 0), (0.45, 0.054, 0.031, 0), (0.53, 0.054, 0.031, 0), (0.61, 0.030, 0.030, 0),
         (0.69, 0.052, 0.030, 0), (0.77, 0.112, 0.028, 1), (0.86, 0.048, 0.027, 0), (0.95, 0.040, 0.022, 0)]
# (where along the edge, length out of it, half width along it, 1 for a pointed fang, 0 for a square crown)


def build(mats, x):
    """The jaw in the plane x (head coordinates) and its teeth."""
    curves = [_resampled(ROOT, ALONG), _resampled(EDGE, ALONG), _at(TOP, ACROSS), _at(BEARD, ACROSS)]
    grid = [[_coons(curves, i, j) for j in range(len(ACROSS))] for i in range(ALONG)]
    mesh = bmesh.new()
    faces = [[[mesh.verts.new((x + side * (_thickness(i, j) / 2 + _lump(i, j, side)), p.x, p.y))
               for j, p in enumerate(row)] for i, row in enumerate(grid)] for side in (1, -1)]
    _faces(mesh, *faces)
    jaw = geo.obj_from("bone_jaw", mesh, mats["blade"], smooth=True)
    edge = _resampled(EDGE, 200)
    return [jaw] + [_tooth(edge, x, k, tooth, mats["tusk"]) for k, tooth in enumerate(TEETH)]


def _coons(curves, i, j):
    """The bilinear Coons patch through root R(u), edge E(u), top T(v) and beard B(v)."""
    root, edge, top, beard = curves
    u, v = i / (ALONG - 1), ACROSS[j]
    corners = (1 - u) * (1 - v) * root[0] + (1 - u) * v * edge[0] + u * (1 - v) * root[-1] + u * v * edge[-1]
    return (1 - v) * root[i] + v * edge[i] + (1 - u) * top[j] + u * beard[j] - corners


def _tooth(edge, x, k, tooth, mat):
    """One tooth rooted in the jaw's ridge at u, standing out of it: a square crown, or a fang curving towards the
    horn to a point."""
    u, length, half, fang = tooth
    i = int(round(u * (len(edge) - 1)))
    at, out = edge[i], -_inward(edge, i)
    along = (edge[max(i - 1, 0)] - edge[min(i + 1, len(edge) - 1)]).normalized()
    bend = along * length * (0.15 if fang else 0.0)
    points = [at - out * 0.035, at + out * length * 0.55 + bend * 0.3, at + out * length + bend]
    points = [Vector((x, p.x, p.y)) for p in points]
    tip = (0.002, 0.002) if fang else (half * 0.65, half * 0.55)
    radii = [(half, half * 0.80), (half * 0.90, half * 0.72), tip]
    return geo.sweep(f"bone_tooth_{k}", points, radii, mat, hint=(1, 0, 0), sides=4, turn=math.pi / 4, smooth=False)


def _thickness(i, j):
    """Tapered towards the tips; the lip along the outlines stops short of the edge."""
    u = i / (ALONG - 1)
    from_outline = min(i, ALONG - 1 - i)
    lip = LIP[from_outline] if from_outline < len(LIP) and j < len(ACROSS) - 2 else 0.0
    return THICK[j] * _taper(u) + lip


def _lump(i, j, side):
    """A deterministic heave of the face at grid point (i, j), none at the outlines or the edge."""
    if j == 0 or j >= len(ACROSS) - 2 or i in (0, ALONG - 1):
        return 0.0
    wave = math.sin(i * 2.3 + j * 1.7 + side) * math.cos(i * 0.9 - j * 2.9 + 2 * side)
    return LUMPS * wave


def _taper(u):
    """Horn and beard tips thinner than the middle."""
    return 0.55 + 0.45 * math.sin(math.pi * u)


def _inward(edge, i):
    """Unit normal of the edge pointing into the blade (towards +y)."""
    ahead = edge[min(i + 1, len(edge) - 1)] - edge[max(i - 1, 0)]
    normal = Vector((-ahead.y, ahead.x)).normalized()
    return normal if normal.x > 0 else -normal


def _resampled(points, count):
    """`count` points evenly by arc length along a Catmull-Rom curve through `points`."""
    return _at(points, [k / (count - 1) for k in range(count)])


def _at(points, fractions):
    dense = [Vector((p.y, p.z)) for p in geo.catmull([(0, y, z) for y, z in points], 400)]
    lengths = [0.0]
    for a, b in zip(dense, dense[1:]):
        lengths.append(lengths[-1] + (b - a).length)
    out = []
    for f in fractions:
        target = f * lengths[-1]
        k = next((n for n, l in enumerate(lengths) if l >= target), len(dense) - 1)
        out.append(dense[k].copy())
    return out


def _faces(mesh, front, back):
    rows, cols = len(front), len(front[0])
    for i in range(rows - 1):
        for j in range(cols - 1):
            mesh.faces.new((front[i][j], front[i + 1][j], front[i + 1][j + 1], front[i][j + 1]))
            mesh.faces.new((back[i][j], back[i][j + 1], back[i + 1][j + 1], back[i + 1][j]))
        mesh.faces.new((front[i][0], back[i][0], back[i + 1][0], front[i + 1][0]))
        mesh.faces.new((front[i][-1], front[i + 1][-1], back[i + 1][-1], back[i][-1]))
    for i in (0, rows - 1):
        for j in range(cols - 1):
            mesh.faces.new((front[i][j], front[i][j + 1], back[i][j + 1], back[i][j]))
