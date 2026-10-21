"""Geometry helpers for the spine greataxe: sweeps, lofts, knobs and the object plumbing, all through bmesh so every
part is a plain mesh the pipeline can join. Lengths in metres; frames are (origin, x, y, z) unit vectors.
"""
import math

import bmesh
import bpy
from mathutils import Matrix, Vector


def obj_from(name, mesh, material, smooth=True):
    """Links a bmesh as a new object with one material; smooth or faceted shading."""
    bmesh.ops.recalc_face_normals(mesh, faces=mesh.faces)
    for face in mesh.faces:
        face.smooth = smooth
    data = bpy.data.meshes.new(name)
    mesh.to_mesh(data)
    mesh.free()
    obj = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(obj)
    data.materials.append(material)
    return obj


def skin(mesh, rings, cap=True):
    """Quads between consecutive rings of equal length; the ends closed with n-gons."""
    sides = len(rings[0])
    for a, b in zip(rings, rings[1:]):
        for i in range(sides):
            j = (i + 1) % sides
            mesh.faces.new((a[i], a[j], b[j], b[i]))
    if cap:
        mesh.faces.new(list(reversed(rings[0])))
        mesh.faces.new(rings[-1])


def ring(mesh, centre, side, up, a, b, sides, turn=0.0, power=1.0):
    """A ring of `sides` vertices round `centre`: half-width `a` along `side`, `b` along `up`. power < 1 squares it."""
    out = []
    for i in range(sides):
        angle = 2 * math.pi * i / sides + turn
        c, s = math.cos(angle), math.sin(angle)
        c, s = math.copysign(abs(c) ** power, c), math.copysign(abs(s) ** power, s)
        out.append(mesh.verts.new(Vector(centre) + side * (a * c) + up * (b * s)))
    return out


def frames(points, hint):
    """(tangent, side, up) at each point of a polyline; side = tangent x hint, up = side x tangent."""
    out = []
    for i, _ in enumerate(points):
        ahead = Vector(points[min(i + 1, len(points) - 1)]) - Vector(points[max(i - 1, 0)])
        tangent = ahead.normalized()
        side = tangent.cross(Vector(hint)).normalized()
        out.append((tangent, side, side.cross(tangent).normalized()))
    return out


def sweep(name, points, radii, material, hint=(1, 0, 0), sides=4, turn=0.0, power=1.0, smooth=False, cap=True):
    """A tube through `points`, each section an ellipse of half-sizes radii[i] = (along side, along up).
    With four sides and no turn the section is a diamond: good for spikes, hooks and blade-like processes."""
    mesh = bmesh.new()
    rings = []
    for co, (tangent, side, up), (a, b) in zip(points, frames(points, hint), radii):
        rings.append(ring(mesh, co, side, up, max(a, 1e-4), max(b, 1e-4), sides, turn, power))
    skin(mesh, rings, cap)
    return obj_from(name, mesh, material, smooth)


def loft_z(name, stations, material, sides=8, power=1.0, smooth=True, matrix=None):
    """A solid through horizontal sections: stations = (z, half x, half y, centre x, centre y)."""
    mesh = bmesh.new()
    rings = [ring(mesh, (cx, cy, z), Vector((1, 0, 0)), Vector((0, 1, 0)), ax, ay, sides, math.pi / sides, power)
             for z, ax, ay, cx, cy in stations]
    skin(mesh, rings)
    if matrix is not None:
        bmesh.ops.transform(mesh, matrix=matrix, verts=mesh.verts)
    return obj_from(name, mesh, material, smooth)


def knob(name, centre, radii, material, segments=8, rings=5, matrix=None, smooth=True):
    """A squashed low-poly sphere; `matrix` places it after the squash."""
    mesh = bmesh.new()
    bmesh.ops.create_uvsphere(mesh, u_segments=segments, v_segments=rings, radius=1.0)
    scale = Matrix.Diagonal((*radii, 1.0))
    bmesh.ops.transform(mesh, matrix=Matrix.Translation(centre) @ scale, verts=mesh.verts)
    if matrix is not None:
        bmesh.ops.transform(mesh, matrix=matrix, verts=mesh.verts)
    return obj_from(name, mesh, material, smooth)


def place(obj, matrix):
    """Moves an object's mesh by `matrix` (applied to the data, so the pipeline sees world coordinates)."""
    obj.data.transform(matrix)
    return obj


def frame_matrix(origin, x, y, z):
    """The matrix taking local coordinates into the frame (origin, x, y, z)."""
    m = Matrix((x, y, z)).transposed().to_4x4()
    m.translation = origin
    return m


def densify(points, step):
    """The polyline resampled so no segment is longer than `step`."""
    out = [Vector(points[0])]
    for a, b in zip(points, points[1:]):
        a, b = Vector(a), Vector(b)
        n = max(1, int(math.ceil((b - a).length / step)))
        out.extend(a.lerp(b, k / n) for k in range(1, n + 1))
    return out


def catmull(points, samples):
    """A Catmull-Rom curve through `points`, `samples` points evenly in parameter, ends included."""
    pts = [Vector(p) for p in points]
    pts = [pts[0] * 2 - pts[1]] + pts + [pts[-1] * 2 - pts[-2]]
    out = []
    for k in range(samples):
        t = k / (samples - 1) * (len(points) - 1)
        i = min(int(t), len(points) - 2)
        out.append(_catmull_at(pts[i], pts[i + 1], pts[i + 2], pts[i + 3], t - i))
    return out


def _catmull_at(p0, p1, p2, p3, t):
    t2, t3 = t * t, t * t * t
    return 0.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2 + (-p0 + 3 * p1 - 3 * p2 + p3) * t3)
