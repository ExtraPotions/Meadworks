"""The Greydwarf Slinger's satchel of stones (Elite Creatures Reborn), worn at the hip so a slinger stands out from
the pack at a distance: a soft leather bag, its flap folded back over the top, a drawstring at the mouth, a strap loop
at the back, and stones heaped in the mouth.

The origin is the middle of the bag's back, where it hangs against the body; the bag bulges to the front (-Y) and
its mouth is at the top (+Z).
"""
import math
import random

import bmesh
import bpy

from workshop import shapes

TEXTURE_SIZE = 256
AO_STRENGTH = 0.6

WIDTH, DEPTH, HEIGHT = 0.15, 0.07, 0.13


def build():
    random.seed(11)
    leather, dark, stone = _leather(), _dark_leather(), _stone()
    _bag(leather)
    _flap(dark)
    bpy.ops.mesh.primitive_torus_add(major_radius=WIDTH * 0.47, minor_radius=0.004, major_segments=20,
                                     minor_segments=4, location=(0.0, -DEPTH / 2, HEIGHT * 0.36))
    string = bpy.context.active_object
    string.name = "drawstring"
    string.scale = (1.0, DEPTH / WIDTH * 1.05, 1.0)
    string.data.materials.append(dark)
    shapes.box("strap_loop", (0.03, 0.012, 0.07), (0.0, 0.004, HEIGHT * 0.35), material=dark, bevel=0.003)
    _stones(stone)


def _bag(material):
    """A soft rounded box, fuller low down where the stones weigh it, gathered a little at the mouth."""
    bpy.ops.mesh.primitive_uv_sphere_add(segments=20, ring_count=12, radius=1.0)
    bag = bpy.context.active_object
    bag.name = "bag"
    mesh = bmesh.new()
    mesh.from_mesh(bag.data)
    for vert in mesh.verts:
        x, y, z = vert.co
        squared = lambda v: math.copysign(abs(v) ** 0.6, v)       # sphere towards box
        sag = 1.08 - 0.12 * z                                      # wider at the bottom
        vert.co = (squared(x) * WIDTH / 2 * sag, -DEPTH / 2 + squared(y) * DEPTH / 2 * sag,
                   min(z, 0.72) * HEIGHT / 2 / 0.86)
    mesh.to_mesh(bag.data)
    mesh.free()
    bag.data.materials.append(material)


def _flap(material):
    """The lid, folded back over the top edge and hanging down the back."""
    shapes.box("flap", (WIDTH * 0.9, 0.005, 0.07), (0.0, 0.008, HEIGHT * 0.33), (math.radians(-12), 0.0, 0.0),
               material, bevel=0.002)


def _stones(material):
    """Six pebbles heaped in the mouth, each a squashed, jittered ball."""
    for i in range(6):
        radius = random.uniform(0.014, 0.02)
        x = -WIDTH * 0.32 + i * WIDTH * 0.13 + random.uniform(-0.008, 0.008)
        bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1, radius=radius,
                                              location=(x, -DEPTH / 2 + random.uniform(-0.015, 0.012),
                                                        HEIGHT * 0.4 + random.uniform(0.0, 0.012)))
        stone = bpy.context.active_object
        stone.name = f"stone_{i}"
        stone.scale = (random.uniform(0.95, 1.25), random.uniform(0.85, 1.1), random.uniform(0.7, 0.9))
        stone.rotation_euler = (random.uniform(0, 3), random.uniform(0, 3), random.uniform(0, 3))
        for vert in stone.data.vertices:
            vert.co *= random.uniform(0.9, 1.08)
        stone.data.materials.append(material)


def _noisy(name, dark, light, scale, roughness, bump=0.0):
    mat = bpy.data.materials.new(name)
    nodes, links = mat.node_tree.nodes, mat.node_tree.links
    bsdf = next(n for n in nodes if n.bl_idname == 'ShaderNodeBsdfPrincipled')
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
    return _noisy("satchel_leather", (0.13, 0.065, 0.028), (0.30, 0.16, 0.07), 90.0, 0.7, bump=0.25)


def _dark_leather():
    return _noisy("satchel_strap", (0.06, 0.03, 0.012), (0.14, 0.075, 0.032), 200.0, 0.65)


def _stone():
    return _noisy("satchel_stones", (0.16, 0.155, 0.14), (0.36, 0.35, 0.32), 40.0, 0.9, bump=0.4)
