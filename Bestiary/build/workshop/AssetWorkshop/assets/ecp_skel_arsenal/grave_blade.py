"""Blades of the skeleton arsenal: faceted like the game's own (SwordIron, KnifeCopper, AxeIron are a few flat facets
each), with the honed edge marked in the "edge" point attribute so grave_paint.iron paints it pale.

`blade` lofts a double-edged blade up the weapon from sections (s, left x, right x, half thickness, bevel, fuller): eight
points round each section, the two edges sharp, the flats bevelled to them. `fuller` below 1 sinks the flat's middle
line (a fuller), above 1 raises it (a spear's midrib). A section with one edge pulled in is a notch.

`slab` lofts a flat piece out from the haft or the tang: an axe head, an atgeir's hook, a crossguard. Its sections
(u, v low, v high, half thickness) run along `direction` in the plane of the blade; the last is the honed edge.
"""
import bmesh
from mathutils import Vector

import grave_shapes as gs


def blade(name, sections, material, tip=None, base_cap=True):
    """sections: (s, left x, right x, half thickness, bevel 0..1, fuller); tip: (s, x) the point, or None for a flat end."""
    mesh, rings, edges = bmesh.new(), [], []
    for s, xl, xr, t, bevel, fuller in sections:
        w = xr - xl
        points = [(xl, 0.0), (xl + w * bevel, t), ((xl + xr) / 2, t * fuller), (xr - w * bevel, t),
                  (xr, 0.0), (xr - w * bevel, -t), ((xl + xr) / 2, -t * fuller), (xl + w * bevel, -t)]
        ring = [mesh.verts.new(gs.at(s, x, z)) for x, z in points]
        rings.append(ring)
        edges += [ring[0], ring[4]]
    gs.skin(mesh, rings, 8, caps=False)
    if base_cap:
        mesh.faces.new(list(reversed(rings[0])))
    _close(mesh, rings[-1], tip, edges)
    return _finish(name, mesh, material, edges)


def single(name, sections, material, tip=None):
    """A single-edged blade (a seax): sections (s, back x, edge x, half thickness at the back, bevel 0..1), the back
    flat and thick, the edge towards `edge x`."""
    mesh, rings, edges = bmesh.new(), [], []
    for s, xb, xe, t, bevel in sections:
        xv = xe - (xe - xb) * bevel
        points = [(xb, t), ((xb + xv) / 2, t * 0.85), (xv, t * 0.6), (xe, 0.0), (xv, -t * 0.6), ((xb + xv) / 2, -t * 0.85), (xb, -t)]
        ring = [mesh.verts.new(gs.at(s, x, z)) for x, z in points]
        rings.append(ring)
        edges.append(ring[3])
    gs.skin(mesh, rings, 7, caps=False)
    mesh.faces.new(list(reversed(rings[0])))
    _close(mesh, rings[-1], tip, edges)
    return _finish(name, mesh, material, edges)


def slab(name, origin, direction, sections, material, flat=Vector((0, 0, 1)), edge_last=True):
    """A flat piece from `origin` along `direction` (in the plane of the blade; `flat` is its normal). sections:
    (u, v low, v high, half thickness): u along `direction`, v across it in the plane (along flat x direction), the
    thickness out of the plane. The last section is the honed edge, unless edge_last is False."""
    d = Vector(direction).normalized()
    across = Vector(flat).cross(d).normalized()
    mesh, rings, edges = bmesh.new(), [], []
    for u, v0, v1, t in sections:
        base = Vector(origin) + d * u
        ring = [mesh.verts.new(base + across * v + Vector(flat) * z) for v, z in ((v0, t), (v1, t), (v1, -t), (v0, -t))]
        rings.append(ring)
    gs.skin(mesh, rings, 4)
    if edge_last:
        edges = rings[-1]
    return _finish(name, mesh, material, edges)


def _close(mesh, ring, tip, edges):
    if tip is None:
        mesh.faces.new(ring)
        return
    point = mesh.verts.new(gs.at(tip[0], tip[1], 0.0))
    edges.append(point)
    for i in range(len(ring)):
        mesh.faces.new((ring[i], ring[(i + 1) % len(ring)], point))


def _finish(name, mesh, material, edges):
    bmesh.ops.recalc_face_normals(mesh, faces=mesh.faces)
    mesh.verts.index_update()
    marked = {v.index for v in edges}
    obj = gs.mesh_object(name, mesh, material, smooth=False)
    attribute = obj.data.attributes.new("edge", 'FLOAT', 'POINT')
    for i, datum in enumerate(attribute.data):
        datum.value = 1.0 if i in marked else 0.0
    return obj


def notch(sections, s, depth, side=1, width=0.012):
    """Adds a chip in the edge at s: three sections, the middle one pulled in by `depth` on the `side` edge (+1 right)."""
    out = list(sections)
    below = _interpolate(sections, s - width)
    middle = list(_interpolate(sections, s))
    above = _interpolate(sections, s + width)
    if side > 0:
        middle[2] -= depth
    else:
        middle[1] += depth
    out += [below, tuple(middle), above]
    return sorted(out, key=lambda section: section[0])


def _interpolate(sections, s):
    for a, b in zip(sections, sections[1:]):
        if a[0] <= s <= b[0]:
            f = (s - a[0]) / (b[0] - a[0])
            return tuple([s] + [x + (y - x) * f for x, y in zip(a[1:], b[1:])])
    raise ValueError(f"s {s} outside the blade")
