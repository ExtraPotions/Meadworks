"""Material recipes. The pipeline bakes each material's Base Color and Normal into the asset's one texture atlas,
so any Principled BSDF node setup works; these are the common looks. Colours are linear RGB tuples.
"""
import bpy

LINEAR = 'LINEAR'


def flat(name, color, roughness=0.8, metallic=0.0):
    mat, bsdf = _new(name)
    bsdf.inputs['Base Color'].default_value = (*color, 1.0)
    bsdf.inputs['Roughness'].default_value = roughness
    bsdf.inputs['Metallic'].default_value = metallic
    return mat


def wood(name, light=(0.30, 0.18, 0.085), dark=(0.11, 0.065, 0.03), axis='X', scale=18.0):
    """Rings around the grain axis (the plank's long axis: 'X', 'Y' or 'Z'), stretched along it into grain lines."""
    mat, bsdf = _new(name, roughness=0.85)
    wave = _node(mat, 'ShaderNodeTexWave', wave_type='RINGS', rings_direction=axis, wave_profile='SAW')
    wave.inputs['Scale'].default_value = scale
    wave.inputs['Distortion'].default_value = 7.0
    wave.inputs['Detail'].default_value = 4.0
    _link(mat, _stretched(mat, axis, 0.1), wave.inputs['Vector'])
    _ramp_into(mat, wave.outputs['Fac'], dark, light, bsdf.inputs['Base Color'], stops=(0.0, 0.6))
    _bump_into(mat, wave.outputs['Fac'], 0.06, bsdf.inputs['Normal'])
    return mat


def iron(name, color=(0.10, 0.10, 0.11), rust=(0.20, 0.09, 0.04)):
    mat, bsdf = _new(name, roughness=0.45, metallic=0.8)
    noise = _noise(mat, scale=30.0, detail=8.0)
    _ramp_into(mat, noise.outputs['Fac'], rust, color, bsdf.inputs['Base Color'], stops=(0.25, 0.55))
    _bump_into(mat, noise.outputs['Fac'], 0.15, bsdf.inputs['Normal'])
    return mat


def stone(name, light=(0.34, 0.33, 0.31), dark=(0.13, 0.13, 0.12), scale=5.0):
    mat, bsdf = _new(name, roughness=0.9)
    cells = _node(mat, 'ShaderNodeTexVoronoi', feature='DISTANCE_TO_EDGE')
    cells.inputs['Scale'].default_value = scale
    _link(mat, _coords(mat), cells.inputs['Vector'])
    _ramp_into(mat, cells.outputs['Distance'], dark, light, bsdf.inputs['Base Color'], stops=(0.0, 0.12))
    noise = _noise(mat, scale=25.0, detail=6.0)
    _bump_into(mat, noise.outputs['Fac'], 0.3, bsdf.inputs['Normal'])
    return mat


def _new(name, roughness=0.8, metallic=0.0):
    mat = bpy.data.materials.new(name)
    bsdf = principled(mat)
    bsdf.inputs['Roughness'].default_value = roughness
    bsdf.inputs['Metallic'].default_value = metallic
    return mat, bsdf


def principled(mat):
    return next(n for n in mat.node_tree.nodes if n.bl_idname == 'ShaderNodeBsdfPrincipled')


def output(mat):
    outs = [n for n in mat.node_tree.nodes if n.bl_idname == 'ShaderNodeOutputMaterial']
    return next((n for n in outs if n.is_active_output), outs[0])


def _node(mat, kind, **props):
    node = mat.node_tree.nodes.new(kind)
    for key, value in props.items():
        setattr(node, key, value)
    return node


def _link(mat, from_socket, to_socket):
    mat.node_tree.links.new(from_socket, to_socket)


def _coords(mat):
    """Object coordinates: after the pipeline joins the parts these are world coordinates, so patterns keep scale."""
    return _node(mat, 'ShaderNodeTexCoord').outputs['Object']


def _stretched(mat, axis, factor):
    """Object coordinates squashed along one axis, so noise varies slowly along it (long grain)."""
    mapping = _node(mat, 'ShaderNodeMapping')
    mapping.inputs['Scale'].default_value = tuple(factor if a == axis else 1.0 for a in "XYZ")
    _link(mat, _coords(mat), mapping.inputs['Vector'])
    return mapping.outputs['Vector']


def _noise(mat, scale, detail):
    noise = _node(mat, 'ShaderNodeTexNoise')
    noise.inputs['Scale'].default_value = scale
    noise.inputs['Detail'].default_value = detail
    _link(mat, _coords(mat), noise.inputs['Vector'])
    return noise


def _ramp_into(mat, fac, low, high, target, stops=(0.0, 1.0)):
    ramp = _node(mat, 'ShaderNodeValToRGB')
    ramp.color_ramp.elements[0].position, ramp.color_ramp.elements[1].position = stops
    ramp.color_ramp.elements[0].color = (*low, 1.0)
    ramp.color_ramp.elements[1].color = (*high, 1.0)
    _link(mat, fac, ramp.inputs['Fac'])
    _link(mat, ramp.outputs['Color'], target)


def _bump_into(mat, height, strength, target):
    bump = _node(mat, 'ShaderNodeBump')
    bump.inputs['Strength'].default_value = strength
    bump.inputs['Distance'].default_value = 0.01
    _link(mat, height, bump.inputs['Height'])
    _link(mat, bump.outputs['Normal'], target)
