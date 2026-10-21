"""One band of the Greydwarf Slinger's slingshot (Elite Creatures Reborn): a strip of twisted sinew exactly one metre
long, from the origin along -Y (Unity: +Z). The mod hangs one on each prong tip, turns it at the pouch and scales its
length to the distance, so the bands stretch as the slinger draws.
"""
import bpy

from workshop import shapes

TEXTURE_SIZE = 128
NORMAL_MAP = False
AO_STRENGTH = 0.0

RADIUS = 0.0034


def build():
    band = shapes.cylinder("band", RADIUS, 1.0, (0.0, -0.5, 0.0), (1.5708, 0.0, 0.0), _sinew(), vertices=8)
    band.scale = (1.35, 1.0, 1.0)   # a flat strip, wider than thick


def _sinew():
    """Pale gut with a darker twist running along it."""
    mat = bpy.data.materials.new("band_sinew")
    nodes, links = mat.node_tree.nodes, mat.node_tree.links
    bsdf = next(n for n in nodes if n.bl_idname == 'ShaderNodeBsdfPrincipled')
    bsdf.inputs['Roughness'].default_value = 0.55
    coords = nodes.new('ShaderNodeTexCoord')
    wave = nodes.new('ShaderNodeTexWave')
    wave.wave_type = 'BANDS'
    wave.bands_direction = 'Y'
    wave.inputs['Scale'].default_value = 40.0
    wave.inputs['Distortion'].default_value = 2.0
    links.new(coords.outputs['Object'], wave.inputs['Vector'])
    ramp = nodes.new('ShaderNodeValToRGB')
    ramp.color_ramp.elements[0].color = (0.22, 0.15, 0.08, 1.0)
    ramp.color_ramp.elements[1].color = (0.46, 0.36, 0.23, 1.0)
    links.new(wave.outputs['Fac'], ramp.inputs['Fac'])
    links.new(ramp.outputs['Color'], bsdf.inputs['Base Color'])
    return mat
