"""Node plumbing for the paint recipes (paint.py): small functions that add one node, wire it and hand back its output
socket, so a recipe reads as a chain of layers. Coordinates are object coordinates, which the pipeline's join turns
into world metres, so every pattern keeps its size in metres whatever the part."""
import bpy

from . import materials


def new_material(name, roughness=0.8, metallic=0.0):
    """A fresh material with one Principled BSDF; returns (material, bsdf)."""
    mat = bpy.data.materials.new(name)
    bsdf = materials.principled(mat)
    bsdf.inputs['Roughness'].default_value = roughness
    bsdf.inputs['Metallic'].default_value = metallic
    return mat, bsdf


def node(mat, kind, **props):
    made = mat.node_tree.nodes.new(kind)
    for key, value in props.items():
        setattr(made, key, value)
    return made


def link(mat, source, target):
    mat.node_tree.links.new(source, target)


def put(mat, socket, value):
    """A number or colour tuple into a socket, or a link from another socket."""
    if isinstance(value, (int, float)):
        socket.default_value = value
    elif isinstance(value, tuple):
        socket.default_value = value if len(value) == len(socket.default_value) else (*value, 1.0)
    else:
        link(mat, value, socket)


def coords(mat, stretch=(1.0, 1.0, 1.0), offset=(0.0, 0.0, 0.0), rotation=(0.0, 0.0, 0.0)):
    """Object coordinates, scaled per axis (below 1: the pattern is drawn out along that axis), shifted and turned."""
    mapping = node(mat, 'ShaderNodeMapping')
    mapping.inputs['Scale'].default_value = stretch
    mapping.inputs['Location'].default_value = offset
    mapping.inputs['Rotation'].default_value = rotation
    link(mat, node(mat, 'ShaderNodeTexCoord').outputs['Object'], mapping.inputs['Vector'])
    return mapping.outputs['Vector']


def noise(mat, scale, detail=2.0, vector=None, roughness=0.5, distortion=0.0):
    """Perlin fBm noise; Fac centres on 0.5 (see paint_layers.SPREAD for its spread by detail)."""
    made = node(mat, 'ShaderNodeTexNoise')
    made.inputs['Scale'].default_value = scale
    made.inputs['Detail'].default_value = detail
    made.inputs['Roughness'].default_value = roughness
    made.inputs['Distortion'].default_value = distortion
    link(mat, vector if vector is not None else coords(mat), made.inputs['Vector'])
    return made.outputs['Fac']


def voronoi(mat, scale, feature='DISTANCE_TO_EDGE', vector=None, randomness=1.0):
    made = node(mat, 'ShaderNodeTexVoronoi', feature=feature)
    made.inputs['Scale'].default_value = scale
    made.inputs['Randomness'].default_value = randomness
    link(mat, vector if vector is not None else coords(mat), made.inputs['Vector'])
    return made.outputs['Distance']


def white(mat, cell):
    """White noise constant over cubes of `cell` metres: one random value per painted texel."""
    snapped = node(mat, 'ShaderNodeVectorMath', operation='SNAP')
    link(mat, node(mat, 'ShaderNodeTexCoord').outputs['Object'], snapped.inputs[0])
    snapped.inputs[1].default_value = (cell, cell, cell)
    made = node(mat, 'ShaderNodeTexWhiteNoise', noise_dimensions='3D')
    link(mat, snapped.outputs['Vector'], made.inputs['Vector'])
    return made.outputs['Value']


def ramp(mat, fac, stops):
    """A colour ramp over fac with stops [(position, (r, g, b))], linear interpolation."""
    made = node(mat, 'ShaderNodeValToRGB')
    elements = made.color_ramp.elements
    while len(elements) < len(stops):
        elements.new(0.5)
    for element, (position, rgb) in zip(elements, sorted(stops, key=lambda s: s[0])):
        element.position, element.color = position, (*rgb, 1.0)
    link(mat, fac, made.inputs['Fac'])
    return made.outputs['Color']


def mask(mat, fac, low, high):
    """0 below low, 1 above high, a straight ramp between (a factor socket)."""
    made = node(mat, 'ShaderNodeMapRange', clamp=True)
    link(mat, fac, made.inputs['Value'])
    made.inputs['From Min'].default_value, made.inputs['From Max'].default_value = low, high
    return made.outputs['Result']


def rgb(mat, colour):
    made = node(mat, 'ShaderNodeRGB')
    made.outputs['Color'].default_value = (*colour[:3], 1.0)
    return made.outputs['Color']


def mix(mat, a, b, factor, blend='MIX'):
    """Colour a blended with b (sockets or colour tuples) by factor (number or socket)."""
    made = node(mat, 'ShaderNodeMix', data_type='RGBA', blend_type=blend, clamp_result=True)
    put(mat, _by_id(made.inputs, 'Factor_Float'), factor)
    put(mat, _by_id(made.inputs, 'A_Color'), a)
    put(mat, _by_id(made.inputs, 'B_Color'), b)
    return _by_id(made.outputs, 'Result_Color')


def _by_id(sockets, identifier):
    """A socket by identifier: the Mix node has a float, a vector and a colour socket under each name."""
    return next(s for s in sockets if s.identifier == identifier)


def math(mat, operation, a, b=None, clamp=False):
    made = node(mat, 'ShaderNodeMath', operation=operation, use_clamp=clamp)
    for socket, value in zip(made.inputs, (a, b)):
        if value is not None:
            put(mat, socket, value)
    return made.outputs['Value']


def bw(mat, colour):
    made = node(mat, 'ShaderNodeRGBToBW')
    link(mat, colour, made.inputs['Color'])
    return made.outputs['Val']


def axis(mat, name, vector=None):
    """One component ('X', 'Y', 'Z') of object coordinates or of a vector socket."""
    split = node(mat, 'ShaderNodeSeparateXYZ')
    source = vector if vector is not None else node(mat, 'ShaderNodeTexCoord').outputs['Object']
    link(mat, source, split.inputs['Vector'])
    return split.outputs[name]


def geometry(mat):
    return node(mat, 'ShaderNodeNewGeometry')


def occlusion(mat, distance, samples=16, inside=False):
    made = node(mat, 'ShaderNodeAmbientOcclusion', samples=samples, inside=inside, only_local=True)
    made.inputs['Distance'].default_value = distance
    return made.outputs['AO']


def edge_strength(mat, radius):
    """How far the true normal leans from a bevelled one of `radius` metres: high along hard edges, near zero on
    smooth curved surfaces (so a smooth-shaded thin tube does not count as all edge, unlike pointiness)."""
    bevel = node(mat, 'ShaderNodeBevel', samples=8)
    bevel.inputs['Radius'].default_value = radius
    difference = node(mat, 'ShaderNodeVectorMath', operation='DISTANCE')
    link(mat, bevel.outputs['Normal'], difference.inputs[0])
    link(mat, geometry(mat).outputs['Normal'], difference.inputs[1])
    return difference.outputs['Value']


def bump(mat, height, distance, strength=1.0):
    made = node(mat, 'ShaderNodeBump')
    made.inputs['Strength'].default_value = strength
    made.inputs['Distance'].default_value = distance
    link(mat, height, made.inputs['Height'])
    return made.outputs['Normal']
