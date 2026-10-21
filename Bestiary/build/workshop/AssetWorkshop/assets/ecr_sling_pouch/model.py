"""The pouch of the Greydwarf Slinger's slingshot (Elite Creatures Reborn): a cupped patch of leather with a sinew
loop at each end, where the bands are tied. The origin is the middle of the cup, where the stone sits; the cup opens
to the front (-Y, towards the fork) and the loops lie along X at +-POUCH_HALF_WIDTH (Unity: the same, x mirrored).
"""
import math

import bmesh
import bpy

TEXTURE_SIZE = 128
AO_STRENGTH = 0.4

POUCH_HALF_WIDTH = 0.03


def build():
    leather, sinew = _material("pouch_leather", (0.13, 0.065, 0.028)), _material("pouch_sinew", (0.42, 0.33, 0.21))
    _cup(leather)
    for side in (-1, 1):
        bpy.ops.mesh.primitive_torus_add(major_radius=0.006, minor_radius=0.0022, major_segments=10, minor_segments=5,
                                         location=(side * POUCH_HALF_WIDTH, 0.002, 0.0), rotation=(0.0, math.pi / 2, 0.0))
        loop = bpy.context.active_object
        loop.name = f"loop_{side}"
        loop.data.materials.append(sinew)


def _cup(material):
    """A flattened sphere pressed into a shallow cup: the back bulges, the front hollows round the stone."""
    bpy.ops.mesh.primitive_uv_sphere_add(segments=16, ring_count=8, radius=1.0)
    cup = bpy.context.active_object
    cup.name = "cup"
    mesh = bmesh.new()
    mesh.from_mesh(cup.data)
    for vert in mesh.verts:
        x, y, z = vert.co
        y = -abs(y) * 0.35 + 0.35 * (1 - x * x)      # both faces bowed back: a cup opening forwards
        vert.co = (x * 0.027, y * 0.014, z * 0.019)
    mesh.to_mesh(cup.data)
    mesh.free()
    cup.data.materials.append(material)


def _material(name, colour):
    mat = bpy.data.materials.new(name)
    nodes, links = mat.node_tree.nodes, mat.node_tree.links
    bsdf = next(n for n in nodes if n.bl_idname == 'ShaderNodeBsdfPrincipled')
    bsdf.inputs['Roughness'].default_value = 0.7
    noise = nodes.new('ShaderNodeTexNoise')
    noise.inputs['Scale'].default_value = 400.0
    ramp = nodes.new('ShaderNodeValToRGB')
    ramp.color_ramp.elements[0].color = tuple(c * 0.6 for c in colour) + (1.0,)
    ramp.color_ramp.elements[1].color = (*colour, 1.0)
    links.new(noise.outputs['Fac'], ramp.inputs['Fac'])
    links.new(ramp.outputs['Color'], bsdf.inputs['Base Color'])
    return mat
