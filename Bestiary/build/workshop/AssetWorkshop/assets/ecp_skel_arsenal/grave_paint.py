"""Materials of the skeleton arsenal - every weapon made of bone - painted like the game's own bone things and baked
into one atlas per weapon.

The palette comes from the game's textures (reference export): Characters/Skeleton/model/Texture/Skeleton_d.tga (a 128 px
sheet: cream bone highlights, brown striations along the grain, a dark brown ground), Spinesnap's yellowed vertebrae,
LargeBone and the bone tower shield, and the Skeleton Crossbowman's bone crossbow. The game paints big soft shapes, so
every pattern here is low in frequency; colours are linear (Blender's base colour). Bindings are sinew and dark hide.

Blades of ground bone carry a point attribute "edge" (grave_blade): 1 on the edge, 0 on the flat; `bone_blade` paints
the ground edge paler and cleaner than the grimed flat, the way the game paints a honed edge as a pale stripe.
"""
import bpy

from workshop import materials

BONE_DARK, BONE_LIGHT = (0.22, 0.155, 0.088), (0.57, 0.475, 0.335)
SPINE_DARK, SPINE_LIGHT = (0.25, 0.175, 0.085), (0.61, 0.5, 0.32)
GROUND = (0.78, 0.70, 0.56)                     # bone ground to an edge: pale and clean


def bone(name="grave_bone"):
    """Long bones, ribs and knuckles: beige, brown in the grain, darker where it is dirty."""
    return _bone(name, BONE_DARK, BONE_LIGHT, stretch=(1.0, 0.12, 1.0))


def vertebra(name="grave_vertebra"):
    """Vertebrae, a little yellower, like the Spinesnap bow's."""
    return _bone(name, SPINE_DARK, SPINE_LIGHT, stretch=(1.0, 0.35, 1.0))


def bone_blade(name="grave_bone_blade"):
    """A blade ground out of bone: the bone's own colour on the flat, a pale clean band along the edge."""
    mat = _bone(name, BONE_DARK, BONE_LIGHT, stretch=(1.0, 0.12, 1.0))
    bsdf = materials.principled(mat)
    colour = bsdf.inputs['Base Color'].links[0].from_socket
    edge = _math(mat, 'POWER', _attribute(mat, "edge"), 3.0)
    _link(mat, _mix(mat, colour, _rgb(mat, GROUND), edge), bsdf.inputs['Base Color'])
    return mat


def teeth(name="grave_teeth"):
    """Teeth and fangs: paler and yellower than bone, darker at the root."""
    return _plain(name, (0.36, 0.30, 0.19), (0.74, 0.66, 0.50), 60.0, 0.5)


def leather(name="grave_leather"):
    """Dark hide wound round a grip."""
    return _plain(name, (0.045, 0.026, 0.014), (0.13, 0.072, 0.036), 45.0, 0.7)


def sinew(name="grave_sinew"):
    """Dried sinew and rawhide lashing, yellow-brown."""
    return _plain(name, (0.13, 0.09, 0.05), (0.33, 0.25, 0.14), 120.0, 0.65)


def feather(name="grave_feather"):
    return _plain(name, (0.028, 0.026, 0.024), (0.12, 0.11, 0.095), 80.0, 0.8, stretch=(3.0, 1.0, 1.0))


def dark(name="grave_hollow"):
    """The inside of a hole in the bone (a pore, the marrow): near black-brown."""
    return materials.flat(name, (0.03, 0.02, 0.012), roughness=0.95)


def _bone(name, dark_colour, light_colour, stretch):
    """Cream bone in soft patches, brown striations along the grain, dirtier in big low blotches: the game's skeleton."""
    mat, bsdf = _new(name, 0.7)
    base = _ramp(mat, _noise(mat, 7.0, 2.0), 0.2, 0.8, dark_colour, light_colour)
    streaks = _ramp(mat, _noise(mat, 38.0, 2.0, stretch=stretch, offset=7.0), 0.36, 0.64, (0.6, 0.5, 0.4), (1, 1, 1))
    dirt = _ramp(mat, _noise(mat, 3.5, 2.0, offset=19.0), 0.45, 0.75, (1, 1, 1), (0.55, 0.44, 0.34))
    colour = _mix(mat, _mix(mat, base, streaks, 1.0, 'MULTIPLY'), dirt, 1.0, 'MULTIPLY')
    _link(mat, colour, bsdf.inputs['Base Color'])
    _bump(mat, _noise(mat, 38.0, 2.0, stretch=stretch, offset=7.0), bsdf, 0.3)
    return mat


def _plain(name, dark_colour, light_colour, scale, roughness, stretch=(1.0, 1.0, 1.0)):
    mat, bsdf = _new(name, roughness)
    _link(mat, _ramp(mat, _noise(mat, scale, 3.0, stretch=stretch), 0.3, 0.7, dark_colour, light_colour), bsdf.inputs['Base Color'])
    return mat


def _new(name, roughness):
    mat = bpy.data.materials.new(name)
    bsdf = materials.principled(mat)
    bsdf.inputs['Roughness'].default_value = roughness
    return mat, bsdf


def _noise(mat, scale, detail, stretch=(1.0, 1.0, 1.0), offset=0.0):
    """Noise in object coordinates (world ones once the parts are joined), so patterns keep their size."""
    nodes = mat.node_tree.nodes
    coords = nodes.new('ShaderNodeTexCoord')
    mapping = nodes.new('ShaderNodeMapping')
    mapping.inputs['Scale'].default_value = stretch
    mapping.inputs['Location'].default_value = (offset, offset * 0.7, offset * 0.3)
    noise = nodes.new('ShaderNodeTexNoise')
    noise.inputs['Scale'].default_value = scale
    noise.inputs['Detail'].default_value = detail
    _link(mat, coords.outputs['Object'], mapping.inputs['Vector'])
    _link(mat, mapping.outputs['Vector'], noise.inputs['Vector'])
    return noise.outputs['Fac']


def _ramp(mat, fac, low, high, colour_low, colour_high):
    ramp = mat.node_tree.nodes.new('ShaderNodeValToRGB')
    ramp.color_ramp.elements[0].position, ramp.color_ramp.elements[1].position = low, high
    ramp.color_ramp.elements[0].color = (*colour_low, 1.0)
    ramp.color_ramp.elements[1].color = (*colour_high, 1.0)
    _link(mat, fac, ramp.inputs['Fac'])
    return ramp.outputs['Color']


def _mix(mat, a, b, factor, blend='MIX'):
    mix = mat.node_tree.nodes.new('ShaderNodeMix')
    mix.data_type = 'RGBA'
    mix.blend_type = blend
    _set(mat, _socket(mix.inputs, 'Factor_Float'), factor)
    _link(mat, a, _socket(mix.inputs, 'A_Color'))
    _link(mat, b, _socket(mix.inputs, 'B_Color'))
    return _socket(mix.outputs, 'Result_Color')


def _socket(sockets, identifier):
    return next(s for s in sockets if s.identifier == identifier)


def _math(mat, operation, a, b):
    node = mat.node_tree.nodes.new('ShaderNodeMath')
    node.operation = operation
    node.use_clamp = True
    _set(mat, node.inputs[0], a)
    _set(mat, node.inputs[1], b)
    return node.outputs[0]


def _attribute(mat, name):
    node = mat.node_tree.nodes.new('ShaderNodeAttribute')
    node.attribute_type = 'GEOMETRY'
    node.attribute_name = name
    return node.outputs['Fac']


def _rgb(mat, colour):
    node = mat.node_tree.nodes.new('ShaderNodeRGB')
    node.outputs[0].default_value = (*colour, 1.0)
    return node.outputs[0]


def _bump(mat, height, bsdf, strength):
    node = mat.node_tree.nodes.new('ShaderNodeBump')
    node.inputs['Strength'].default_value = strength
    node.inputs['Distance'].default_value = 0.002
    _link(mat, height, node.inputs['Height'])
    _link(mat, node.outputs['Normal'], bsdf.inputs['Normal'])


def _set(mat, socket, value):
    if isinstance(value, (int, float)):
        socket.default_value = value
    else:
        _link(mat, value, socket)


def _link(mat, output, socket):
    mat.node_tree.links.new(output, socket)
