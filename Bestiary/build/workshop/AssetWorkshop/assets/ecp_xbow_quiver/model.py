"""The Skeleton Crossbowman's bolt quiver (Elite Creatures Pack), worn on the left hip: a stiff leather case, narrower
at the bottom, a thick rim band round the mouth, a stitched seam down the front, a hanging loop at the back and a bone
cap on the bottom. Empty: the bolts in it are the game's own bone bolt, which the mod stands in the mouth (the
workshop's Unity side places their slots, BOLT_SLOTS below).

The origin is the middle of the case's back at the mouth, where it hangs against the hip; the case bulges to the front
(-Y) and hangs down (-Z) from its open mouth at the top.
"""
import math

import bmesh
import bpy

from workshop import materials, shapes

TEXTURE_SIZE = 256
AO_STRENGTH = 0.6

WIDTH, DEPTH, LENGTH = 0.11, 0.07, 0.31
TAPER = 0.72                                   # the bottom's size against the mouth's
WALL = 0.006

# Where the bolts stand in the mouth, nock end up 13 cm out of it (Blender axes): x across, y into the case, the
# mouth is z 0. The game's bone bolt at 0.75 of its size is 0.43 m, so its head stays 1 to 2 cm off the bottom.
BOLT_SLOTS = ((-0.030, -0.022), (0.004, -0.018), (0.032, -0.026), (-0.014, -0.046), (0.020, -0.050))


def build():
    leather, dark, bone = _leather(), _dark_leather(), _bone()
    _case(leather)
    _rim(dark)
    _seam(dark)
    for i, z in enumerate((-0.08, -0.22)):
        _ring_band(f"band_{i}", z, 0.0045, dark)
    shapes.box("loop", (0.028, 0.012, 0.06), (0.0, 0.006, -0.02), material=dark, bevel=0.003)
    bpy.ops.mesh.primitive_uv_sphere_add(segments=12, ring_count=6, radius=1.0, location=(0.0, -DEPTH * TAPER / 2, -LENGTH))
    cap = bpy.context.active_object
    cap.name = "cap"
    cap.scale = (WIDTH * TAPER * 0.5, DEPTH * TAPER * 0.52, 0.012)
    cap.data.materials.append(bone)
    _to_meshes()


def _taper(z):
    """How much of the mouth's size the case has at height z (0 at the mouth, -LENGTH at the bottom)."""
    return 1.0 + (TAPER - 1.0) * (-z / LENGTH)


def _case(material):
    """An open rounded-rectangle tube with a wall, closed at the bottom."""
    mesh = bmesh.new()
    sides, levels = 16, 6
    outer, inner = [], []
    for level in range(levels + 1):
        z = -LENGTH * level / levels
        outer.append(_ring(mesh, z, _taper(z), 0.0, sides))
        inner.append(_ring(mesh, z, _taper(z), WALL, sides))
    for rings in (outer, inner):
        for a, b in zip(rings, rings[1:]):
            for i in range(sides):
                j = (i + 1) % sides
                mesh.faces.new((a[i], a[j], b[j], b[i]))
    for i in range(sides):
        j = (i + 1) % sides
        mesh.faces.new((outer[0][j], outer[0][i], inner[0][i], inner[0][j]))
    mesh.faces.new(list(reversed(outer[-1])))
    bmesh.ops.recalc_face_normals(mesh, faces=mesh.faces)
    data = bpy.data.meshes.new("case")
    mesh.to_mesh(data)
    mesh.free()
    obj = bpy.data.objects.new("case", data)
    bpy.context.collection.objects.link(obj)
    data.materials.append(material)


def _ring(mesh, z, scale, inset, sides):
    ring = []
    for i in range(sides):
        angle = 2 * math.pi * i / sides
        c, s = math.cos(angle), math.sin(angle)
        x = (WIDTH / 2 * scale - inset) * math.copysign(abs(c) ** 0.6, c)
        y = -DEPTH / 2 * scale + (DEPTH / 2 * scale - inset) * math.copysign(abs(s) ** 0.6, s)
        ring.append(mesh.verts.new((x, y, z)))
    return ring


def _rim(material):
    _ring_band("rim", -0.004, 0.0065, material)


def _ring_band(name, z, thickness, material):
    """A strap round the case at height z, following its rounded corners."""
    scale = _taper(z)
    points = []
    for i in range(24):
        angle = 2 * math.pi * i / 24
        c, s = math.cos(angle), math.sin(angle)
        x = (WIDTH / 2 * scale + thickness * 0.4) * math.copysign(abs(c) ** 0.6, c)
        y = -DEPTH / 2 * scale + (DEPTH / 2 * scale + thickness * 0.4) * math.copysign(abs(s) ** 0.6, s)
        points.append(((x, y, z), thickness))
    return _tube(name, points, material, closed=True)


def _seam(material):
    """The stitched seam down the front, a raised cord following the taper."""
    points = [((0.0, -DEPTH * _taper(z) - 0.0015, z), 0.0028) for z in (-0.012, -LENGTH * 0.5, -LENGTH + 0.018)]
    return _tube("seam", points, material)


def _tube(name, points, material, closed=False):
    curve = bpy.data.curves.new(name, 'CURVE')
    curve.dimensions = '3D'
    curve.bevel_depth = 1.0
    curve.bevel_resolution = 1
    curve.use_fill_caps = not closed
    spline = curve.splines.new('POLY' if closed else 'NURBS')
    spline.points.add(len(points) - 1)
    for point, (co, radius) in zip(spline.points, points):
        point.co = (*co, 1.0)
        point.radius = radius
    spline.use_cyclic_u = closed
    spline.use_endpoint_u = True
    obj = bpy.data.objects.new(name, curve)
    bpy.context.collection.objects.link(obj)
    curve.materials.append(material)
    scene_curves.append(obj)
    return obj


scene_curves = []


def _to_meshes():
    for obj in bpy.context.scene.objects:
        obj.select_set(obj in scene_curves)
    bpy.context.view_layer.objects.active = scene_curves[0]
    bpy.ops.object.convert(target='MESH')


def _noisy(name, dark, light, scale, roughness, bump=0.0):
    mat = bpy.data.materials.new(name)
    nodes, links = mat.node_tree.nodes, mat.node_tree.links
    bsdf = materials.principled(mat)
    bsdf.inputs['Roughness'].default_value = roughness
    noise = nodes.new('ShaderNodeTexNoise')
    noise.inputs['Scale'].default_value = scale
    noise.inputs['Detail'].default_value = 5.0
    ramp = nodes.new('ShaderNodeValToRGB')
    ramp.color_ramp.elements[0].color = (*dark, 1.0)
    ramp.color_ramp.elements[1].color = (*light, 1.0)
    links.new(noise.outputs['Fac'], ramp.inputs['Fac'])
    links.new(ramp.outputs['Color'], bsdf.inputs['Base Color'])
    if bump > 0:
        node = nodes.new('ShaderNodeBump')
        node.inputs['Strength'].default_value = bump
        node.inputs['Distance'].default_value = 0.002
        links.new(noise.outputs['Fac'], node.inputs['Height'])
        links.new(node.outputs['Normal'], bsdf.inputs['Normal'])
    return mat


def _leather():
    """Old stiff leather, darkened and scuffed."""
    return _noisy("quiver_leather", (0.09, 0.05, 0.025), (0.24, 0.14, 0.07), 80.0, 0.7, bump=0.3)


def _dark_leather():
    return _noisy("quiver_strap", (0.045, 0.024, 0.011), (0.12, 0.065, 0.03), 200.0, 0.65)


def _bone():
    return _noisy("quiver_bone", (0.30, 0.25, 0.17), (0.55, 0.49, 0.37), 60.0, 0.6, bump=0.2)
