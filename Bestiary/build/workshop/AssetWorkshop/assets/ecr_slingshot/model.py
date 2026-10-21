"""The Greydwarf Slinger's slingshot (Elite Creatures Reborn): a forked branch in its bark, the handle wrapped in a
leather strap, sinew ties at the prong tips where the bands are knotted. Only the fork: the bands and the pouch are
their own assets (ecr_sling_band, ecr_sling_pouch), because the mod stretches them to the drawing hand at runtime.

The origin is the middle of the grip, where the left hand holds it; the prongs point up (+Z) and the front (-Y) faces
the target. The band anchors, where the mod ties each band on, are BAND_ANCHORS (Blender axes; Unity: x' = -x,
y' = z, z' = -y).
"""
import math

import bpy

from workshop import scene

TEXTURE_SIZE = 512
AO_STRENGTH = 0.5

BAND_ANCHORS = ((-0.052, 0.004, 0.186), (0.050, 0.005, 0.191))

HANDLE = [((0.0, 0.004, -0.085), 0.0185), ((0.002, 0.001, -0.03), 0.0165), ((0.0, 0.0, 0.03), 0.0165),
          ((0.0, 0.0, 0.07), 0.0165)]
PRONG_A = [((0.0, 0.0, 0.045), 0.0165), ((-0.022, 0.003, 0.105), 0.013), ((-0.046, -0.001, 0.16), 0.011),
           ((-0.053, 0.002, 0.207), 0.0095)]
PRONG_B = [((0.0, 0.0, 0.045), 0.0165), ((0.021, -0.002, 0.11), 0.013), ((0.044, 0.003, 0.165), 0.011),
           ((0.051, 0.002, 0.212), 0.0095)]
CROTCH = ((0.0, 0.0, 0.074), (0.021, 0.019, 0.026))   # a knot where the branch splits, hiding the joint


def build():
    bark, leather, sinew = _bark(), _leather(), _sinew()
    for name, points in (("handle", HANDLE), ("prong_a", PRONG_A), ("prong_b", PRONG_B)):
        _tube(name, points, bark, bevel_resolution=2, resolution=4)
    _crotch(bark)
    _helix("grip_wrap", (0.0, 0.0), -0.062, 0.038, 0.0185, 0.0068, 0.0034, leather)
    for i, (points, anchor) in enumerate(((PRONG_A, BAND_ANCHORS[0]), (PRONG_B, BAND_ANCHORS[1]))):
        _tie(f"tie_{i}", points, anchor, sinew)
    _to_meshes()


def _tube(name, points, material, bevel_resolution=2, resolution=1):
    """A round, tapering tube through the points: each point's radius is the tube's there."""
    curve = bpy.data.curves.new(name, 'CURVE')
    curve.dimensions = '3D'
    curve.bevel_depth = 1.0
    curve.bevel_resolution = bevel_resolution
    curve.use_fill_caps = True
    curve.resolution_u = resolution
    spline = curve.splines.new('NURBS')
    spline.points.add(len(points) - 1)
    for point, (co, radius) in zip(spline.points, points):
        point.co = (*co, 1.0)
        point.radius = radius
    spline.use_endpoint_u = True
    spline.order_u = 3
    obj = bpy.data.objects.new(name, curve)
    bpy.context.collection.objects.link(obj)
    curve.materials.append(material)
    return obj


def _helix(name, centre, z0, z1, radius, pitch, thickness, material, turns_offset=0.0):
    """A strap wound round a vertical axis from z0 to z1, `pitch` metres per turn."""
    steps = max(8, int((z1 - z0) / pitch * 10))
    points = []
    for i in range(steps + 1):
        z = z0 + (z1 - z0) * i / steps
        angle = 2 * math.pi * ((z - z0) / pitch + turns_offset)
        points.append(((centre[0] + radius * math.cos(angle), centre[1] + radius * math.sin(angle), z), thickness))
    return _tube(name, points, material, bevel_resolution=1)


def _tie(name, prong, anchor, material):
    """A few turns of sinew round the prong just below its tip, turned to the prong's slant."""
    tip, below = prong[-1][0], prong[-2][0]
    tie = _helix(name, (0.0, 0.0), -0.011, 0.011, 0.0118, 0.0055, 0.0026, material)
    direction = (tip[0] - below[0], tip[1] - below[1], tip[2] - below[2])
    tie.rotation_mode = 'QUATERNION'
    from mathutils import Vector
    tie.rotation_quaternion = Vector((0, 0, 1)).rotation_difference(Vector(direction).normalized())
    tie.location = anchor


def _crotch(material):
    (x, y, z), (rx, ry, rz) = CROTCH
    bpy.ops.mesh.primitive_uv_sphere_add(segments=12, ring_count=7, radius=1.0, location=(x, y, z))
    knot = bpy.context.active_object
    knot.name = "crotch"
    knot.scale = (rx, ry, rz)
    knot.data.materials.append(material)


def _to_meshes():
    curves = [o for o in bpy.context.scene.objects if o.type == 'CURVE']
    scene.select_only(curves)
    bpy.ops.object.convert(target='MESH')


def _bark():
    """Grey-brown bark in long streaks along the branch, knotted by a coarser noise."""
    mat = bpy.data.materials.new("slingshot_bark")
    nodes, links = mat.node_tree.nodes, mat.node_tree.links
    bsdf = next(n for n in nodes if n.bl_idname == 'ShaderNodeBsdfPrincipled')
    bsdf.inputs['Roughness'].default_value = 0.9
    coords = nodes.new('ShaderNodeTexCoord')
    stretch = nodes.new('ShaderNodeMapping')
    stretch.inputs['Scale'].default_value = (1.0, 1.0, 0.12)
    links.new(coords.outputs['Object'], stretch.inputs['Vector'])
    streaks = nodes.new('ShaderNodeTexNoise')
    streaks.inputs['Scale'].default_value = 160.0
    streaks.inputs['Detail'].default_value = 6.0
    links.new(stretch.outputs['Vector'], streaks.inputs['Vector'])
    ramp = nodes.new('ShaderNodeValToRGB')
    ramp.color_ramp.elements[0].position, ramp.color_ramp.elements[1].position = 0.35, 0.7
    ramp.color_ramp.elements[0].color = (0.045, 0.032, 0.022, 1.0)
    ramp.color_ramp.elements[1].color = (0.24, 0.18, 0.12, 1.0)
    links.new(streaks.outputs['Fac'], ramp.inputs['Fac'])
    links.new(ramp.outputs['Color'], bsdf.inputs['Base Color'])
    bump = nodes.new('ShaderNodeBump')
    bump.inputs['Strength'].default_value = 0.5
    bump.inputs['Distance'].default_value = 0.003
    links.new(streaks.outputs['Fac'], bump.inputs['Height'])
    links.new(bump.outputs['Normal'], bsdf.inputs['Normal'])
    return mat


def _leather():
    mat = bpy.data.materials.new("slingshot_leather")
    nodes, links = mat.node_tree.nodes, mat.node_tree.links
    bsdf = next(n for n in nodes if n.bl_idname == 'ShaderNodeBsdfPrincipled')
    bsdf.inputs['Roughness'].default_value = 0.65
    noise = nodes.new('ShaderNodeTexNoise')
    noise.inputs['Scale'].default_value = 300.0
    noise.inputs['Detail'].default_value = 4.0
    ramp = nodes.new('ShaderNodeValToRGB')
    ramp.color_ramp.elements[0].color = (0.07, 0.035, 0.015, 1.0)
    ramp.color_ramp.elements[1].color = (0.19, 0.10, 0.045, 1.0)
    links.new(noise.outputs['Fac'], ramp.inputs['Fac'])
    links.new(ramp.outputs['Color'], bsdf.inputs['Base Color'])
    return mat


def _sinew():
    mat = bpy.data.materials.new("slingshot_sinew")
    bsdf = next(n for n in mat.node_tree.nodes if n.bl_idname == 'ShaderNodeBsdfPrincipled')
    bsdf.inputs['Base Color'].default_value = (0.42, 0.33, 0.21, 1.0)
    bsdf.inputs['Roughness'].default_value = 0.6
    return mat
