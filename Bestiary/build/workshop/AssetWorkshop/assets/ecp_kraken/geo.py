"""Geometry for the kraken: parts built in Blender space with bone weights and paint values, turned into meshes.

Positions in the contract are Unity's (x right, y up, z forward); U() turns them into Blender's (the workshop's
convention: Blender -Y is Unity +Z, Blender Z is Unity Y, so Unity +X is Blender -X). Everything else is built in
Blender space with right-handed frames, so the winding rules below hold:

    ring(c, S, N, ...)        points c + r (cos a S + sin a N), counter-clockwise about T = S x N
    strip(ring A, ring B)     quads between two rings, B further along T: faces point outward
    cap_end / cap_start       fans closing a tube at its far / near end, pointing out of the tube
"""
import math

import bmesh
import bpy
from mathutils import Vector


def U(x, y, z):
    """A Unity position or direction as a Blender vector."""
    return Vector((-x, -z, y))


def to_unity(v):
    return (-v[0], v[2], -v[1])


def frame(tangent, up_hint):
    """(S, N, T): T along the tangent, N as close to up_hint as possible, S = N x T (right-handed, S x N = T)."""
    t = Vector(tangent).normalized()
    up = Vector(up_hint)
    n = (up - t * up.dot(t))
    if n.length < 1e-6:
        n = Vector((0, 0, 1)) if abs(t.z) < 0.9 else Vector((1, 0, 0))
        n = n - t * n.dot(t)
    n.normalize()
    return n.cross(t), n, t


def smoothstep(a, b, x):
    t = min(max((x - a) / (b - a), 0.0), 1.0)
    return t * t * (3 - 2 * t)


def monotone(points):
    """A smooth monotone cubic through (x, y) points (Fritsch-Carlson); returns f(x), clamped at the ends."""
    xs, ys = [p[0] for p in points], [p[1] for p in points]
    n = len(xs)
    d = [(ys[i + 1] - ys[i]) / (xs[i + 1] - xs[i]) for i in range(n - 1)]
    m = [d[0]] + [0.0 if d[i - 1] * d[i] <= 0 else (d[i - 1] + d[i]) / 2 for i in range(1, n - 1)] + [d[-1]]
    for i in range(n - 1):
        if d[i] == 0:
            m[i] = m[i + 1] = 0.0
            continue
        a, b = m[i] / d[i], m[i + 1] / d[i]
        s = a * a + b * b
        if s > 9:
            k = 3 / math.sqrt(s)
            m[i], m[i + 1] = k * a * d[i], k * b * d[i]

    def f(x):
        if x <= xs[0]:
            return ys[0]
        if x >= xs[-1]:
            return ys[-1]
        i = max(j for j in range(n - 1) if xs[j] <= x)
        h = xs[i + 1] - xs[i]
        t = (x - xs[i]) / h
        h00, h10 = 2 * t ** 3 - 3 * t ** 2 + 1, t ** 3 - 2 * t ** 2 + t
        h01, h11 = -2 * t ** 3 + 3 * t ** 2, t ** 3 - t ** 2
        return h00 * ys[i] + h10 * h * m[i] + h01 * ys[i + 1] + h11 * h * m[i + 1]
    return f


class Part:
    """One piece of a mesh: vertices (Blender space), faces, bone weights per vertex, paint per vertex or face."""

    def __init__(self, name, closed=True, sharp_angle=None):
        self.name, self.closed, self.sharp_angle = name, closed, sharp_angle
        self.verts, self.faces, self.weights = [], [], []
        self.vpaint, self.fpaint = {}, {}
        self.seams = []            # vertex index pairs: UV seams
        self.uv_scale = 1.0        # >1 gives the part more of the atlas (see bake_export.unwrap_smart)
        self.uv_scales = {}        # per vertex, overriding uv_scale

    def vert(self, p, weights, **paint):
        index = len(self.verts)
        self.verts.append(Vector(p))
        self.weights.append(dict(weights))
        for key, value in paint.items():
            self.vpaint.setdefault(key, {})[index] = value
        return index

    def face(self, indices, **paint):
        index = len(self.faces)
        self.faces.append(tuple(indices))
        for key, value in paint.items():
            self.fpaint.setdefault(key, {})[index] = value
        return index

    def ring(self, center, s, n, radius, count, weights, phase=0.0, **paint):
        """radius: a number or f(angle) -> (radius along S, radius along N). weights/paint may be f(point, angle)."""
        out = []
        for i in range(count):
            a = phase + 2 * math.pi * i / count
            rs, rn = (radius, radius) if not callable(radius) else radius(a)
            p = Vector(center) + s * (rs * math.cos(a)) + n * (rn * math.sin(a))
            w = weights(p, a) if callable(weights) else weights
            values = {k: (v(p, a) if callable(v) else v) for k, v in paint.items()}
            out.append(self.vert(p, w, **values))
        return out

    def strip(self, a, b, **paint):
        count = len(a)
        for i in range(count):
            j = (i + 1) % count
            self.face((a[i], a[j], b[j], b[i]), **paint)

    def cap_end(self, ring, pole, **paint):
        for i in range(len(ring)):
            self.face((ring[i], ring[(i + 1) % len(ring)], pole), **paint)

    def cap_start(self, ring, pole, **paint):
        for i in range(len(ring)):
            self.face((ring[(i + 1) % len(ring)], ring[i], pole), **paint)


def to_object(part, name=None):
    """The part as a Blender mesh object: vertex groups for its bone weights, corner attributes for its paint."""
    mesh = bpy.data.meshes.new(name or part.name)
    mesh.from_pydata([tuple(v) for v in part.verts], [], part.faces)
    mesh.validate(clean_customdata=False)
    obj = bpy.data.objects.new(mesh.name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    if part.closed:
        _recalc_normals(mesh)
    _weights(obj, part)
    _paint(mesh, part)
    _uv_hints(mesh, part)
    for poly in mesh.polygons:
        poly.use_smooth = True
    if part.sharp_angle is not None:
        mesh.set_sharp_from_angle(angle=math.radians(part.sharp_angle))
    return obj


def _uv_hints(mesh, part):
    seams = {tuple(sorted(e)) for e in part.seams}
    for edge in mesh.edges:
        edge.use_seam = tuple(sorted(edge.vertices)) in seams
    centre = sum(part.verts, Vector()) / max(len(part.verts), 1)
    scales = [part.uv_scales.get(i, part.uv_scale) for i in range(len(mesh.vertices))]
    mesh.attributes.new('uvscale', 'FLOAT', 'POINT').data.foreach_set('value', scales)
    mesh.attributes.new('uvpivot', 'FLOAT_VECTOR', 'POINT').data.foreach_set('vector', list(centre) * len(mesh.vertices))


def _recalc_normals(mesh):
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(mesh)
    bm.free()


def _weights(obj, part):
    groups = {}
    for index, weights in enumerate(part.weights):
        for bone, w in weights.items():
            if w <= 0:
                continue
            if bone not in groups:
                groups[bone] = obj.vertex_groups.get(bone) or obj.vertex_groups.new(name=bone)
            groups[bone].add([index], w, 'REPLACE')


def _paint(mesh, part):
    names = set(part.vpaint) | set(part.fpaint)
    for name in names:
        attr = mesh.attributes.new(name, 'FLOAT', 'CORNER')
        vvalues, fvalues = part.vpaint.get(name, {}), part.fpaint.get(name, {})
        values = [0.0] * len(mesh.loops)
        for poly in mesh.polygons:
            face_value = fvalues.get(poly.index)
            for loop in poly.loop_indices:
                v = mesh.loops[loop].vertex_index
                values[loop] = max(face_value or 0.0, vvalues.get(v, 0.0))
        attr.data.foreach_set("value", values)


def join(objs, name):
    """Joins objects (vertex groups and attributes merge by name) into one, named `name`."""
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objs:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    if len(objs) > 1:
        bpy.ops.object.join()
    obj = bpy.context.view_layer.objects.active
    obj.name = obj.data.name = name
    return obj


def triangles(obj):
    return sum(len(p.vertices) - 2 for p in obj.data.polygons)


def check_outward(obj):
    """Sum of (face centre - centroid) . normal * area: negative means the faces point inward."""
    mesh = obj.data
    centroid = sum((v.co for v in mesh.vertices), Vector()) / max(len(mesh.vertices), 1)
    return sum((p.center - centroid).dot(p.normal) * p.area for p in mesh.polygons)
