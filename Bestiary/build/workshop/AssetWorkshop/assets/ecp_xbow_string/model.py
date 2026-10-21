"""One half of the Skeleton Crossbowman's crossbow string (Elite Creatures Pack): twisted sinew cord exactly one metre
long, from the origin along -Y (Unity: +Z). The mod ties one on each prod tip, points it at the string's middle (the
nut when spanned, the drawing fingers, or straight across when let go) and scales its length to the distance.
"""
import bpy

from workshop import shapes

TEXTURE_SIZE = 128
NORMAL_MAP = False
AO_STRENGTH = 0.0

RADIUS = 0.0026


def build():
    shapes.cylinder("string", RADIUS, 1.0, (0.0, -0.5, 0.0), (1.5708, 0.0, 0.0), _sinew(), vertices=6)


def _sinew():
    """Pale gut with a darker twist running along it."""
    mat = bpy.data.materials.new("string_sinew")
    nodes, links = mat.node_tree.nodes, mat.node_tree.links
    bsdf = next(n for n in nodes if n.bl_idname == 'ShaderNodeBsdfPrincipled')
    bsdf.inputs['Roughness'].default_value = 0.55
    coords = nodes.new('ShaderNodeTexCoord')
    wave = nodes.new('ShaderNodeTexWave')
    wave.wave_type = 'BANDS'
    wave.bands_direction = 'Y'
    wave.inputs['Scale'].default_value = 60.0
    wave.inputs['Distortion'].default_value = 2.0
    links.new(coords.outputs['Object'], wave.inputs['Vector'])
    ramp = nodes.new('ShaderNodeValToRGB')
    ramp.color_ramp.elements[0].color = (0.24, 0.18, 0.10, 1.0)
    ramp.color_ramp.elements[1].color = (0.50, 0.41, 0.27, 1.0)
    links.new(wave.outputs['Fac'], ramp.inputs['Fac'])
    links.new(ramp.outputs['Color'], bsdf.inputs['Base Color'])
    return mat
