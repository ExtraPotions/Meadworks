"""Shapes shared by the skeleton arsenal (ecp_skel_*): lofts, tubes, knobs, cones, wraps and point attributes.

Weapons lie along -Y (Unity +Z, the game's attach frame: the hand's grip at the origin, the weapon pointing away from the
fist), so `at(s, x, z)` places a point `s` metres up the weapon. Every helper returns the Blender object it made; the
pipeline joins them. Point attributes (`paint`) are float values the materials in grave_paint read, for example "edge"
on a blade's honed edge: after the join, parts without the attribute read 0.
"""
import math

import bmesh
import bpy
from mathutils import Matrix, Vector


def at(s, x=0.0, z=0.0):
    """A point `s` metres up the weapon (Blender -Y), `x` across (Blender X), `z` out of the flat (Blender Z)."""
    return Vector((x, -s, z))


def loft(name, stations, material, sides=8, roundness=0.75, smooth=True):
    """A solid through sections along the weapon: each station (s, half width, half height[, x, z]), capped at both
    ends. `roundness` 1 is round, lower squares the section off a little, as the game's hafts and bones are."""
    mesh = bmesh.new()
    rings = []
    for station in stations:
        s, hw, hh = station[:3]
        ox, oz = (station[3], station[4]) if len(station) > 3 else (0.0, 0.0)
        rings.append([mesh.verts.new(at(s, ox + hw * _round(math.cos(a), roundness), oz + hh * _round(math.sin(a), roundness)))
                      for a in (2 * math.pi * (k + 0.5) / sides for k in range(sides))])
    skin(mesh, rings, sides)
    return mesh_object(name, mesh, material, smooth)


def band(name, points, material, sides=8, up=Vector((0, 0, 1)), smooth=True):
    """A flattened solid swept along points: each (point, half height along `up`, half thickness across), capped."""
    mesh = bmesh.new()
    rings = []
    for i, (co, high, thick) in enumerate(points):
        ahead = (Vector(points[min(i + 1, len(points) - 1)][0]) - Vector(points[max(i - 1, 0)][0])).normalized()
        side = ahead.cross(up)
        if side.length < 0.3:                      # along `up` itself: any upright of the path will do
            side = ahead.cross(Vector((0, 1, 0)) if abs(ahead.y) < 0.9 else Vector((1, 0, 0)))
        side.normalize()
        lift = side.cross(ahead).normalized()
        rings.append([mesh.verts.new(Vector(co) + side * thick * math.cos(a) + lift * high * math.sin(a))
                      for a in (2 * math.pi * k / sides for k in range(sides))])
    skin(mesh, rings, sides)
    return mesh_object(name, mesh, material, smooth)


def skin(mesh, rings, sides, caps=True):
    for a, b in zip(rings, rings[1:]):
        for i in range(sides):
            j = (i + 1) % sides
            mesh.faces.new((a[i], a[j], b[j], b[i]))
    if caps:
        mesh.faces.new(list(reversed(rings[0])))
        mesh.faces.new(rings[-1])
    bmesh.ops.recalc_face_normals(mesh, faces=mesh.faces)


def mesh_object(name, mesh, material, smooth=True):
    data = bpy.data.meshes.new(name)
    mesh.to_mesh(data)
    mesh.free()
    for polygon in data.polygons:
        polygon.use_smooth = smooth
    obj = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(obj)
    if material is not None:
        data.materials.append(material)
    return obj


def knob(name, location, radius, material, squash=(1.0, 1.0, 1.0), segments=8, rings=5):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=rings, radius=radius, location=location)
    obj = bpy.context.active_object
    obj.name = name
    obj.scale = squash
    obj.data.materials.append(material)
    bpy.ops.object.shade_smooth()
    return obj


def cone(name, base, tip, radius, material, tip_ratio=0.35, vertices=6, squash=None):
    """A blunt tapering process from `base` to `tip`; `squash` (across, up) flattens it like a bony blade."""
    base, tip = Vector(base), Vector(tip)
    points = [(base, radius, radius), (base.lerp(tip, 0.55), radius * (0.5 + 0.5 * tip_ratio), radius * (0.5 + 0.5 * tip_ratio)),
              (tip, radius * tip_ratio, radius * tip_ratio)]
    if squash:
        points = [(p, h * squash[1], t * squash[0]) for p, h, t in points]
    return band(name, points, material, sides=vertices)


def tube(name, points, material, resolution=2, sides_resolution=1):
    """A round tube along points [(co, radius)], through a NURBS curve, made a mesh at once."""
    curve = bpy.data.curves.new(name, 'CURVE')
    curve.dimensions = '3D'
    curve.bevel_depth = 1.0
    curve.bevel_resolution = sides_resolution
    curve.use_fill_caps = True
    curve.resolution_u = resolution
    spline = curve.splines.new('NURBS')
    spline.points.add(len(points) - 1)
    for point, (co, radius) in zip(spline.points, points):
        point.co = (*co, 1.0)
        point.radius = radius
    spline.use_endpoint_u = True
    spline.order_u = min(3, len(points))
    obj = bpy.data.objects.new(name, curve)
    bpy.context.collection.objects.link(obj)
    curve.materials.append(material)
    return to_mesh(obj)


def wrap(name, s0, s1, radius, pitch, thickness, material, centre=(0.0, 0.0), squash=1.0, phase=0.0):
    """Turns of cord or strip round the weapon's axis from s0 to s1, `pitch` metres a turn."""
    turns = abs(s1 - s0) / pitch
    steps = max(8, int(turns * 7))
    points = []
    for i in range(steps + 1):
        s = s0 + (s1 - s0) * i / steps
        angle = phase + 2 * math.pi * turns * i / steps
        points.append((at(s, centre[0] + radius * math.cos(angle), centre[1] + radius * squash * math.sin(angle)), thickness))
    return tube(name, points, material, resolution=1)


def rings(name, s_list, radius, thickness, material, centre=(0.0, 0.0), squash=1.0):
    """Separate closed bands of cord (a lashing seen as neat turns), one ring per s."""
    made = []
    for i, s in enumerate(s_list):
        bpy.ops.mesh.primitive_torus_add(major_radius=radius, minor_radius=thickness, major_segments=10, minor_segments=4,
                                         location=at(s, *centre), rotation=(math.radians(90), 0.0, 0.0))
        obj = bpy.context.active_object
        obj.name = f"{name}_{i}"
        obj.scale = (1.0, 1.0, squash) if squash != 1.0 else (1.0, 1.0, 1.0)
        obj.data.materials.append(material)
        bpy.ops.object.shade_smooth()
        made.append(obj)
    return made


def to_mesh(obj):
    for other in bpy.context.scene.objects:
        other.select_set(other is obj)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.convert(target='MESH')
    bpy.ops.object.shade_smooth()
    return bpy.context.active_object


def paint(obj, name, value_of):
    """A float point attribute `name` on the object: value_of(vertex position in object space) for each vertex."""
    attribute = obj.data.attributes.get(name) or obj.data.attributes.new(name, 'FLOAT', 'POINT')
    for vertex, datum in zip(obj.data.vertices, attribute.data):
        datum.value = value_of(vertex.co)
    return obj


def bend(objs, offset):
    """Bends straight parts along a curve: every vertex moves by offset(s), a Vector across the weapon for the point
    `s` up it (world space; the objects' transforms are applied first). An offset that is 0 with no slope at the fist
    (s 0) leaves the grip where the game holds it."""
    for obj in objs:
        if obj.type != 'MESH':
            continue
        obj.data.transform(obj.matrix_world)
        obj.matrix_world = Matrix.Identity(4)
        for vertex in obj.data.vertices:
            vertex.co += offset(-vertex.co.y)


def arc(depth, reach, toward=Vector((-1.0, 0.0, 0.0))):
    """A gentle arc through the fist: straight at s 0, bending `toward` more and more to `depth` metres at `reach`
    metres from the fist (either way), like a spine's curve."""
    return lambda s: Vector(toward) * depth * (s / reach) ** 2


def handle_s(depth, start, end, toward=Vector((-1.0, 0.0, 0.0))):
    """One S over the entire handle, without a straight grip zone. Keep the fist at its origin.

    Smooth endpoint tangents let the head translate rigidly after the handle ends.
    """
    def displacement(s):
        t = _smooth((s - start) / (end - start))
        return depth * math.sin(2.0 * math.pi * t)
    grip = displacement(0.0)
    return lambda s: Vector(toward) * (displacement(s) - grip)


def wave(depth, length, end, calm=0.15, rise=0.2, start=None, toward=Vector((-1.0, 0.0, 0.0))):
    """Several gentle curves one after another, like a real spine's: a sine `depth` metres to either side, one full
    S every `length` metres, held dead straight within `calm` of the fist and rising to full over `rise`, and faded out
    again before `end` (s, up the weapon) so a head there stays on the weapon's line; with `start`, straight below it
    too (a straight part of the shaft there)."""
    def envelope(s):
        grip = _smooth((abs(s) - calm) / rise)
        if start is not None:
            grip *= _smooth((s - start) / rise)
        return grip * (_smooth((end - s) / rise) if s > 0 else 1.0)
    return lambda s: Vector(toward) * depth * envelope(s) * math.sin(2 * math.pi * s / length)


def _smooth(t):
    t = min(max(t, 0.0), 1.0)
    return t * t * (3.0 - 2.0 * t)


def meshes():
    return [o for o in bpy.context.scene.objects if o.type == 'MESH']


def transform(objs, matrix):
    """Moves finished parts by a matrix (for building a part in its own frame and placing it)."""
    for obj in objs:
        obj.matrix_world = matrix @ obj.matrix_world


def _round(v, roundness):
    return math.copysign(abs(v) ** roundness, v)
