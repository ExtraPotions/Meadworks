"""The Rime Giant's crust (Elite Creatures Reborn): the snow and rime it wears only while dormant, so the sleeping troll
reads as a snowy outcrop on a ridge. assets/ecr_rime_crust (the upper back) and ecr_rime_crust_<n> build the pieces.

Built from what RimeCrustFit measured on the game's Troll in the Sleeping clip's first frame (world axes, metres,
turned into Blender's here). A blanket of snow settles where the skin faces up, deepest where it is flattest, its top
smoothed into soft low-poly drifts that never bridge a cliff; a skirt hangs from every edge down into the skin, so no
gap shows as the troll breathes; clumps of snow and lumps of rime ice sit on it, and icicles hang where an edge
overhangs empty air. Craggy lumps of rime cling to the back and sides (and the shins and knees in front, leaving the
face clear), grey where they face sideways and white where snow sits on their tops, so the outline turns to rock. Each
piece is the part on one bone, so it rides that bone; the hands twitch in their sleep and stay bare.
"""
import math
import random
from collections import Counter

from mathutils import Vector

import rime_fit
import rime_ice
from rime_ice import CRAG_RAMP, PALE, RIME, SNOW, SNOW_RAMP, SNOW_SHADE, mix

TEXTURE_SIZE = 256
AO_STRENGTH = 0.2
PIECES = ("ecr_rime_crust", "ecr_rime_crust_1", "ecr_rime_crust_2", "ecr_rime_crust_3", "ecr_rime_crust_4",
          "ecr_rime_crust_5", "ecr_rime_crust_6")   # one per crust mount, in the fit's order (Spine2 first)
DEPTH = 0.6         # snow on flat skin, metres
SETTLES = 0.3       # snow settles where the skin's normal points at least this far up (about 72 degrees of slope)
SKIRT = 0.3         # how far an edge's skirt hangs below the skin
STRIDE = 2          # the blanket takes every second cell of the fit's 10 cm grid: big, low-poly facets
CLIFF = 2.2         # snow never bridges a step steeper than this (rise over run) between neighbouring cells
EDGE = 0.4          # the snow thins to this much of its depth at an edge
LIGHT = (0.25, 1.0, 0.35)
CRAG_SPACING = 0.5  # rime crags on the back and sides keep at least this far apart


def build(piece):
    crust = rime_fit.load()["crust"]
    field = Field(crust)
    quads = [q for q in field.quads() if field.owner(q) == piece]
    rng = random.Random(4099 + piece)
    convert = (lambda p: rime_fit.blender(*p))
    sheet, clumps, ice = rime_ice.Builder(convert), rime_ice.Builder(convert), rime_ice.Builder(convert)
    if quads:
        edges = _blanket(sheet, field, quads)
        _lumps(clumps, ice, field, quads, rng)
        _icicles(ice, field, quads, edges, rng)
    _crags(clumps, crust, piece, rng)
    if not clumps.bm.faces and not sheet.bm.faces:
        raise SystemExit(f"crust piece {piece} ({field.mounts[piece]}) has no snow and no crags")
    _finish(piece, sheet, clumps, ice)
    print(f"WORKSHOP crust piece on {field.mounts[piece]}: {len(quads)} cells, {len(quads) * field.step ** 2:.2f} m2 of snow", flush=True)


def _finish(piece, sheet, clumps, ice):
    """Each builder closed, shaded by facing on its own ramp, and added to the scene (the pipeline joins them)."""
    snow, rime = rime_ice.material(PIECES[piece] + "_snow", snow=True), rime_ice.material(PIECES[piece] + "_rime")
    for builder, stops, weight, name, look in ((sheet, SNOW_RAMP, 0.55, "sheet", snow), (clumps, CRAG_RAMP, 0.85, "clumps", snow),
                                               (ice, None, 0.35, "ice", rime)):
        if not builder.bm.faces:
            continue
        builder.close()
        if builder is sheet:
            builder.turn_up()
        builder.shade(LIGHT, weight, 0.06, piece, stops)
        builder.finish(f"{PIECES[piece]}_{name}", look)


class Field:
    """The fit's grid of cells: which carry snow, the skin under them smoothed, the snow's depth and its top."""

    def __init__(self, crust):
        columns, rows = range(0, crust["columns"], STRIDE), range(0, crust["rows"], STRIDE)
        picked = [r * crust["columns"] + c for r in rows for c in columns]
        self.columns, self.rows, self.step = len(columns), len(rows), crust["spacing"] * STRIDE
        self.x0, self.z0, self.mounts = crust["x0"], crust["z0"], crust["mounts"]
        self.mount = [crust["mount"][i] for i in picked]
        self.cliff = CLIFF * self.step
        height, up = [crust["height"][i] for i in picked], [crust["up"][i] for i in picked]
        self.snow = [height[i] > rime_fit.MISS and self.mount[i] >= 0 and up[i] > SETTLES for i in range(len(height))]
        depth = [DEPTH * _smoothstep(SETTLES, 0.85, up[i]) if self.snow[i] else 0.0 for i in range(len(height))]
        self.base = self._smooth(height, height, 2)
        self.depth = [max(d, 0.08) if s else 0.0 for d, s in zip(self._smooth(depth, height, 2), self.snow)]
        self.top = [self.base[i] + self.depth[i] * (1.0 + _drift(*self.xz(i))) for i in range(len(height))]

    def index(self, column, row):
        return row * self.columns + column

    def xz(self, i):
        return self.x0 + (i % self.columns) * self.step, self.z0 + (i // self.columns) * self.step

    def point(self, i, lift=0.0):
        x, z = self.xz(i)
        return (x, self.top[i] + lift, z)

    def _smooth(self, values, height, passes):
        """Averages each snowy cell with its snowy neighbours on the same level (no step steeper than CLIFF)."""
        for _ in range(passes):
            smoothed = list(values)
            for i, snowy in enumerate(self.snow):
                if snowy:
                    near = [values[j] for j in self.around(i) if self.snow[j] and abs(height[j] - height[i]) < self.cliff]
                    smoothed[i] = sum(near) / len(near)
            values = smoothed
        return values

    def around(self, i):
        column, row = i % self.columns, i // self.columns
        return [self.index(c, r) for c in range(max(0, column - 1), min(self.columns, column + 2))
                for r in range(max(0, row - 1), min(self.rows, row + 2))]

    def quads(self):
        """Every grid square whose four corners carry snow on one level, as its four cell indices (anticlockwise)."""
        for row in range(self.rows - 1):
            for column in range(self.columns - 1):
                corners = (self.index(column, row), self.index(column + 1, row),
                           self.index(column + 1, row + 1), self.index(column, row + 1))
                heights = [self.base[i] for i in corners]
                if all(self.snow[i] for i in corners) and max(heights) - min(heights) < self.cliff:
                    yield corners

    def owner(self, quad):
        """The crust piece a square belongs to: the mount most of its corners' skin belongs to."""
        return Counter(self.mount[i] for i in quad).most_common(1)[0][0]


def _smoothstep(low, high, x):
    t = min(1.0, max(0.0, (x - low) / (high - low)))
    return t * t * (3 - 2 * t)


def _drift(x, z):
    """A slow swell over the snow, so the blanket lies in drifts rather than a shell."""
    return 0.18 * math.sin(1.7 * x + 0.3) * math.cos(1.3 * z + 1.1) + 0.1 * math.sin(3.1 * x - 2.3 * z)


def _blanket(builder, field, quads):
    """The snow's top, thinning at the edges, then a skirt down every edge; returns the edges (pairs of cells)."""
    uses = Counter(frozenset(pair) for quad in quads for pair in zip(quad, quad[1:] + quad[:1]))
    edges = [tuple(edge) for edge, count in uses.items() if count == 1]
    at = _outline(field, quads, edges)
    verts = {i: builder.vert(p) for i, p in at.items()}
    below = {i: builder.vert((at[i][0], field.base[i] - SKIRT, at[i][2])) for edge in edges for i in edge}
    for quad in quads:
        colours = [mix(SNOW_SHADE, SNOW, min(1.0, field.depth[i] / DEPTH * 1.5)) for i in quad]
        builder.face([verts[i] for i in quad], colours)
    for a, b in edges:
        builder.face([verts[a], verts[b], below[b], below[a]], [SNOW, SNOW, SNOW_SHADE, SNOW_SHADE])
    return edges


def _outline(field, quads, edges):
    """Where each corner of the blanket goes: on its cell, lower at the edge (the snow thins out), and the edge's
    staircase along the grid eased into a smooth line."""
    at = {i: list(field.point(i)) for quad in quads for i in quad}
    rim = {}
    for a, b in edges:
        rim.setdefault(a, []).append(b)
        rim.setdefault(b, []).append(a)
    for i in rim:
        at[i][1] = field.base[i] + field.depth[i] * EDGE
    for _ in range(3):
        eased = {i: [(at[i][k] + sum(at[j][k] for j in near) / len(near)) / 2 for k in (0, 2)] for i, near in rim.items()}
        for i, (x, z) in eased.items():
            at[i][0], at[i][2] = x, z
    return {i: tuple(p) for i, p in at.items()}


def _lumps(clumps, ice, field, quads, rng):
    """Clumps of snow and a few lumps of rime ice, half sunk into the blanket."""
    area = len(quads) * field.step * field.step
    for n in range(max(1, min(14, round(area / 0.7)))):
        quad = rng.choice(quads)
        x, y, z = (sum(c) / 4 for c in zip(*(field.point(i) for i in quad)))
        size = rng.uniform(0.12, 0.3)
        radii = (size * rng.uniform(0.8, 1.2), size * rng.uniform(0.45, 0.7), size * rng.uniform(0.8, 1.2))
        rime_ice.chunk(ice if n % 3 == 2 else clumps, (x, y - radii[1] * 0.35, z), radii, rng, colour=PALE if n % 3 == 2 else SNOW)


def _crags(builder, crust, piece, rng):
    """Lumps of rime clinging to the back and sides where the fit found skin, spaced apart, mostly sunk into it: the
    sleeper's outline turns craggy, grey below and white where snow sits on their tops."""
    points = [(Vector((p["x"], p["y"], p["z"])), Vector((n["x"], n["y"], n["z"])))
              for p, n, m in zip(crust["crags"], crust["cragNormals"], crust["cragMount"]) if m == piece]
    rng.shuffle(points)
    chosen = []
    for at, normal in points:
        if all((at - other).length > CRAG_SPACING for other, _ in chosen):
            chosen.append((at, normal))
    for at, normal in chosen:
        size = rng.uniform(0.4, 0.8)
        rime_ice.crag(builder, at + normal * size * 0.15, normal, size, rng, RIME)


def _icicles(ice, field, quads, edges, rng):
    """Icicles from edge cells whose next cell outwards drops away by more than an icicle's length (or is empty air)."""
    inside = {i for quad in quads for i in quad}
    candidates = []
    for i in {i for edge in edges for i in edge}:
        for j in field.around(i):
            drop = field.base[i] - (field.base[j] if field.base[j] > rime_fit.MISS else 0.0)
            if j not in inside and drop > 1.0 and j != i:
                candidates.append((i, j, drop))
    for i, j, drop in rng.sample(candidates, min(len(candidates), 6)):
        (xi, zi), (xj, zj) = field.xz(i), field.xz(j)
        root = Vector((xi + (xj - xi) * 0.6, field.base[i] + 0.04, zi + (zj - zi) * 0.6))
        length = min(drop - 0.25, rng.uniform(0.18, 0.5))
        axis = Vector((rng.uniform(-0.12, 0.12), -1.0, rng.uniform(-0.12, 0.12)))
        rime_ice.crystal(ice, root, axis, length, length * 0.16, rng.uniform(0, 1), 5, root=PALE)
