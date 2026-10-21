"""A vertebra, the bone every piece of the skeleton arsenal carries, and the vertebra the skeletons drop (ecp_vertebra).

Built in its own frame and placed by a matrix: the body's middle at the origin, the spinal axis along local Z (the head
end up), the arch, the canal and the spinous process behind the body (local +Y), the transverse processes out to the
sides (local X). Sizes scale with `r`, the body's radius. Low-poly like the game's bones: a squat kidney-shaped body with
a lipped rim and a waist, the arch as a thick band round the canal (the hole shows through), a hatchet of a spinous
process, winged transverse processes with knobbed tips, and four articular pegs.

CANAL is the canal's middle in units of r: a shaft threaded through the canal passes there (a weapon's collar); a
shaft through the body passes the origin.

Kinds: 'lumbar' (big body, short hatchet spine, long wings: the dropped item, the mace head), 'thoracic' (a long spine
sloping down, shorter wings: collars on the long weapons), 'neck' (small, short and forked processes: a grip is a
stack of these), 'beast' (a lumbar vertebra from something big, its processes short and heavy: the mace's
head), 'grip' (a neck vertebra with its processes worn short, for a fist to close on), 'tail' (a long narrow tail
vertebra with stubby processes: a dagger's grip tapers down a few of these).
"""
import math

import bmesh
from mathutils import Matrix, Vector

import grave_shapes as gs

CANAL = (0.0, 0.95, 0.0)
SPINE_ROOT = (0.0, 1.5, 0.0)        # where the spinous process leaves the top of the arch
WING_ROOT = (0.5, 0.8, 0.05)        # where a wing leaves the arch's side (x is -0.5 for the other)

KINDS = {
    #            height  canal  spine (points, half heights)                                   wing reach, rise   wing r
    'lumbar': dict(h=0.85, hole=1.0, spine=[(1.62, 0.02), (2.25, -0.12), (2.8, -0.32)], spine_hh=(0.42, 0.4, 0.3),
                   wing=(1.75, 0.2), wing_r=0.27, pegs=1.0),
    'thoracic': dict(h=0.95, hole=1.0, spine=[(1.62, 0.0), (2.1, -0.5), (2.4, -1.2)], spine_hh=(0.34, 0.22, 0.14),
                     wing=(1.45, 0.35), wing_r=0.21, pegs=0.9),
    'beast': dict(h=0.8, hole=1.0, spine=[(1.62, 0.02), (2.0, -0.08), (2.35, -0.2)], spine_hh=(0.46, 0.44, 0.36),
                  wing=(1.6, 0.12), wing_r=0.32, pegs=1.0),
    'grip': dict(h=0.8, hole=0.95, spine=[(1.55, 0.0), (1.8, -0.1)], spine_hh=(0.3, 0.22), wing=(1.12, 0.0),
                 wing_r=0.22, pegs=0.75),
    'neck': dict(h=0.8, hole=1.0, spine=[(1.62, 0.0), (2.0, -0.12)], spine_hh=(0.26, 0.2), wing=(1.3, 0.05),
                 wing_r=0.2, pegs=0.7),
    'tail': dict(h=1.3, hole=0.85, spine=[(1.5, 0.0), (1.75, -0.2)], spine_hh=(0.3, 0.2), wing=(1.2, -0.1),
                 wing_r=0.24, pegs=0.6),
    'shaft': dict(h=1.7, hole=0.8, spine=[(1.35, 0.0), (1.75, -0.3), (2.0, -0.62)], spine_hh=(0.42, 0.34, 0.22),
                  wing=(1.4, -0.3), wing_r=0.3, pegs=0.5, waist=0.68),
}


def build(prefix, place, r, bone, kind='lumbar', spine=True, wings=True, pegs=True, low=False, height=None):
    """The vertebra's parts, placed by the 4x4 matrix `place`; returns them. Leave out the spinous process (`spine`), the
    wings or the pegs where a weapon puts something of its own there: a blade ground out of the spine, a hook for a wing.
    Where the spine starts is SPINE_ROOT (units of r, local). `low` builds it with fewer faces (no knobs, fewer sides),
    for the many vertebrae of a column; `height` overrides the kind's body height (units of r)."""
    k = dict(KINDS[kind], low=low)
    if height is not None:
        k['h'] = height
    parts = [_body(prefix, r, k['h'], bone, sides=6 if low else 10, waist=k.get('waist', 0.88)), _arch(prefix, r, k, bone)]
    parts += (_spine(prefix, r, k, bone) if spine else []) + (_wings(prefix, r, k, bone) if wings else [])
    parts += _pegs(prefix, r, k, bone) if pegs else []
    gs.transform(parts, place)
    return parts


def flat(prefix, front, r, bone, kind='lumbar', wings=True, pegs=True):
    """A vertebra lying in the blade's plane, seen from above on the blade's flat: its spinal axis out of the flat
    (Z), the arch and the root of its spine up the weapon, the front of its body at s = `front` (where the grip or the
    haft meets it). Its spine is left off for a blade ground out of it, from the returned s up the weapon (grave_blade).
    Returns (place, the s of the spine's root)."""
    centre = front + 0.8 * r
    place = frame(gs.at(centre), (0, 0, 1), (0, -1, 0))
    build(prefix, place, r, bone, kind, spine=False, wings=wings, pegs=pegs)
    return place, centre + SPINE_ROOT[1] * r


def column(name, s0, s1, r0, r1, bone, disc=None, kind='shaft', back=(1.0, 0.0, 0.0), turn=0.0, length=1.0, count=None,
           **parts):
    """A spine up the weapon from s0 to s1: low-poly vertebrae stacked on their spinal axes (along the weapon), their
    size going evenly from r0 to r1, the first one's spine towards `back` and each next turned `turn` degrees further
    round the weapon, a disc of `disc` (cartilage) between each pair when given. Either `count` vertebrae, their bodies
    as long as it takes to fill the length (few big pieces, the game's way), or each body `length` times the kind's
    usual height. `parts` goes to build (spine, wings, pegs; pegs are off by default). Returns the objects it made (to
    bend them, grave_shapes.bend)."""
    gap = 0.25
    if count is not None:
        h = max(0.6, (s1 - s0) / (count * (r0 + r1) / 2) - gap)
    else:
        h = KINDS[kind]['h'] * length
        count = max(1, round((s1 - s0) / ((h + gap) * (r0 + r1) / 2)))
    sizes = [r0 + (r1 - r0) * (i + 0.5) / count for i in range(count)]
    stretch = (s1 - s0) / sum((h + gap) * r for r in sizes)
    parts.setdefault('pegs', False)
    s, made = s0, []
    for i, r in enumerate(sizes):
        angle = math.radians(turn * i)
        spine = (back[0] * math.cos(angle) - back[2] * math.sin(angle), 0.0, back[0] * math.sin(angle) + back[2] * math.cos(angle))
        centre = s + h * r / 2
        made += build(f"{name}_{i}", frame(gs.at(centre), (0, -1, 0), spine), r, bone, kind, low=True, height=h, **parts)
        unit = (h + gap) * r * stretch
        if disc is not None and i < count - 1:
            made.append(gs.loft(f"{name}_disc_{i}", [(s + h * r - 0.002, 0.56 * r, 0.5 * r), (s + unit + 0.002, 0.56 * r, 0.5 * r)],
                                disc, sides=6, roundness=1.0))
        s += unit
    return made


def at(place, r, local):
    """A point given in the vertebra's own frame (units of r) under `place`: where to hang something on it."""
    return place @ (Vector(local) * r)


def frame(origin, axis, back):
    """The matrix putting a vertebra at `origin` with its spinal axis along `axis` and its spine towards `back`."""
    z = Vector(axis).normalized()
    y = (Vector(back) - z * Vector(back).dot(z)).normalized()
    x = y.cross(z)
    m = Matrix((x, y, z)).transposed().to_4x4()
    m.translation = Vector(origin)
    return m


def canal(place, r):
    """Where the canal's middle lands under `place`."""
    return place @ (Vector(CANAL) * r)


def _body(prefix, r, h, bone, sides=10, waist=0.88):
    """The body: a squat kidney with a lipped rim at each end, a waist between and dished end plates."""
    mesh = bmesh.new()
    rings = []
    for z, scale in ((-0.5, 0.9), (-0.4, 1.0), (0.0, waist), (0.4, 1.0), (0.5, 0.9)):
        rings.append([mesh.verts.new(_kidney(a, r * scale, z * h * r)) for a in (2 * math.pi * i / sides for i in range(sides))])
    gs.skin(mesh, rings, sides, caps=False)
    for ring, z in ((rings[0], -1), (rings[-1], 1)):
        middle = mesh.verts.new((0.0, -0.1 * r, z * (0.5 * h - 0.07) * r))
        for i in range(sides):
            face = (ring[i], ring[(i + 1) % sides], middle) if z > 0 else (ring[(i + 1) % sides], ring[i], middle)
            mesh.faces.new(face)
    bmesh.ops.recalc_face_normals(mesh, faces=mesh.faces)
    return gs.mesh_object(prefix + "_body", mesh, bone)


def _kidney(angle, radius, z):
    """A point on the body's outline: round in front (-Y), flattened and dished in behind where the canal sits."""
    x, y = radius * math.cos(angle), radius * 0.8 * math.sin(angle)
    if y > 0:
        y *= 0.55 - 0.25 * max(0.0, 1.0 - abs(x) / radius)
    return (x, y, z)


def _arch(prefix, r, k, bone):
    """Pedicles and laminae: a thick band from one side of the body's back round the canal to the other."""
    s = k['hole']
    path = [(-0.5, 0.1), (-0.63, 0.85 * s), (-0.47, 1.42 * s), (0.0, 1.66 * s), (0.47, 1.42 * s), (0.63, 0.85 * s), (0.5, 0.1)]
    heights = [0.4, 0.36, 0.33, 0.38, 0.33, 0.36, 0.4]
    points = [(Vector((x * r, y * r, 0.0)), hh * r, 0.2 * r) for (x, y), hh in zip(path, heights)]
    return gs.band(prefix + "_arch", points, bone, sides=4 if k['low'] else 6)


def _spine(prefix, r, k, bone):
    """The spinous process: a flat blade of bone going back and down from the top of the arch, knobbed at the tip."""
    points = [(Vector((0.0, y * r, z * r)), hh * r, 0.14 * r * (1.0 - 0.35 * i / len(k['spine'])))
              for i, ((y, z), hh) in enumerate(zip(k['spine'], k['spine_hh']))]
    tip = points[-1][0]
    blade = gs.band(prefix + "_spine", points, bone, sides=4 if k['low'] else 6)
    if k['low']:
        return [blade]
    return [blade, gs.knob(prefix + "_spine_tip", tip, 0.13 * r, bone, squash=(0.8, 1.0, 1.3), segments=6, rings=4)]


def _wings(prefix, r, k, bone):
    """The transverse processes: out to each side, a little back and up, flattened, knobbed at the tips."""
    parts = []
    reach, rise = k['wing']
    for side in (-1, 1):
        base = Vector((side * 0.5 * r, 0.8 * r, 0.05 * r))
        tip = Vector((side * reach * r, 1.05 * r, rise * r))
        parts.append(gs.cone(f"{prefix}_wing_{side}", base, tip, k['wing_r'] * r, bone, tip_ratio=0.42, squash=(1.0, 0.5),
                             vertices=4 if k['low'] else 6))
        if not k['low']:
            parts.append(gs.knob(f"{prefix}_wing_tip_{side}", tip, k['wing_r'] * 0.45 * r, bone, squash=(1.0, 1.3, 0.8),
                                 segments=6, rings=4))
    return parts


def _pegs(prefix, r, k, bone):
    """The articular processes: a pair of pegs up and a pair down, where the next vertebra would lock on."""
    parts = []
    p = k['pegs']
    for side in (-1, 1):
        up_base, up_tip = Vector((side * 0.5 * r, 1.0 * r, 0.25 * r)), Vector((side * 0.56 * r, 1.12 * r, 0.75 * p * r))
        down_base, down_tip = Vector((side * 0.36 * r, 1.45 * r, -0.1 * r)), Vector((side * 0.3 * r, 1.62 * r, -0.65 * p * r))
        parts.append(gs.cone(f"{prefix}_peg_up_{side}", up_base, up_tip, 0.16 * r, bone, tip_ratio=0.6, vertices=5))
        parts.append(gs.cone(f"{prefix}_peg_down_{side}", down_base, down_tip, 0.15 * r, bone, tip_ratio=0.6, vertices=5))
    return parts
