"""The greataxe's materials, all bone but the bindings, painted the way the game paints its bone: node setups the
pipeline bakes into one small atlas, point filtered in the game.

The game's bone (Skeleton_d, SpineSnap_d, 64 to 128 px) is painted light, not rendered: colour in big soft blotches,
a lighter worn crown on every ridge and edge, near-black brown in every gap, no fine grain; its big bone surfaces
(Bonemass' bonebone_d) carry high-contrast smoky grime, cream to near-black brown, over a pitted normal map. So the
recipes here are: big low-detail blotches and stains, smoky grime over part of it, convex edges lifted (Cycles'
pointiness), surfaces facing up a shade lighter than those facing down, the hollows darkened by the ambient-occlusion
node, and a pitted bump. Colours (linear RGB) from those textures, the wolf fang (ivory) and the Spinesnap's grip
(near-black red-brown leather).
"""
import bpy

from workshop import materials

BONE = ((0.060, 0.030, 0.010), (0.330, 0.225, 0.100), (0.610, 0.455, 0.205))      # hollow, mid, light
SKULL = ((0.050, 0.026, 0.010), (0.360, 0.255, 0.135), (0.630, 0.490, 0.285))
BLADE = ((0.045, 0.023, 0.008), (0.270, 0.180, 0.082), (0.530, 0.395, 0.185))     # a giant's old jawbone
TUSK = ((0.080, 0.056, 0.032), (0.420, 0.375, 0.290), (0.680, 0.635, 0.530))
STAIN = (0.20, 0.100, 0.045)                                                        # the game's rusty-brown streaks
GRIME = (0.075, 0.042, 0.018)                                                       # bonebone_d's smoky dark brown
LEATHER = ((0.018, 0.008, 0.006), (0.075, 0.034, 0.022))
SINEW = ((0.035, 0.018, 0.009), (0.170, 0.095, 0.050))


def bone(name="spine_bone", palette=BONE, stain=0.5, grain=(1.0, 1.0, 1.0), grime=0.25, pits=0.2):
    """Old bone as the game paints it. `stain` is how much of it the rusty-brown stains cover, `grime` how much the
    smoky dark grime (0 to 1 each); `grain` stretches the blotches (small along an axis: streaks along it); `pits` is
    the strength of the pitted bump."""
    mat, bsdf = _new(name, roughness=0.8)
    hollow, mid, light = palette
    blotches = _ramp(mat, _noise(mat, 9.0, 1.5, grain), mid, light, (0.34, 0.66))
    tint = _ramp(mat, _noise(mat, 3.5, 1.0), (0.90, 0.88, 0.80), (1.06, 1.02, 0.94), (0.3, 0.7))
    stained = _stains(mat, _mix(mat, blotches, tint, 'MULTIPLY', 1.0), stain, grain)
    grimy = _grime(mat, stained, grime, grain)
    worn = _worn_edges(mat, grimy, tuple(min(1.0, c * 1.15) for c in light))
    _link(mat, _cavity(mat, _top_light(mat, worn), hollow, 0.05, (0.3, 0.95)), bsdf.inputs['Base Color'])
    _bump(mat, _noise(mat, 38.0, 4.0), pits, bsdf)
    return mat


def skull():
    return bone("spine_skull", SKULL, stain=0.35, grime=0.3)


def blade():
    """The jaw: older bone than the spine, more stained, streaked from the root out to the edge (-Y)."""
    return bone("spine_blade", BLADE, stain=0.7, grain=(1.0, 0.45, 1.0), grime=0.85, pits=0.45)


def tusk():
    """The teeth, the hook and the crown's fang: pale ivory, like the game's wolf fangs."""
    return bone("spine_tusk", TUSK, stain=0.15, grime=0.1, pits=0.1)


def leather(name="spine_leather"):
    """Dark red-brown leather, like the Spinesnap's grip: nearly flat, a little lighter where it bulges."""
    mat, bsdf = _new(name, roughness=0.7)
    dark, light = LEATHER
    base = _ramp(mat, _noise(mat, 14.0, 1.5), dark, light, (0.3, 0.8))
    worn = _worn_edges(mat, base, tuple(c * 1.8 for c in light))
    _link(mat, _cavity(mat, _top_light(mat, worn), dark, 0.02, (0.4, 0.95)), bsdf.inputs['Base Color'])
    return mat


def sinew(name="spine_sinew"):
    """Dried sinew: mid brown, darker between the turns."""
    mat, bsdf = _new(name, roughness=0.7)
    dark, light = SINEW
    base = _ramp(mat, _noise(mat, 20.0, 1.5), dark, light, (0.3, 0.8))
    _link(mat, _cavity(mat, _worn_edges(mat, base, tuple(c * 1.5 for c in light)), dark, 0.01, (0.4, 0.95)),
          bsdf.inputs['Base Color'])
    return mat


def socket_dark(name="spine_socket"):
    """Inside eye sockets and the nose: near black-brown, as the game paints them."""
    return materials.flat(name, (0.020, 0.011, 0.006), roughness=0.95)


def _stains(mat, colour, amount, grain):
    """Rusty-brown stains in big patches over `amount` of the surface."""
    stained = _mix(mat, colour, (*STAIN, 1.0), 'MIX', 0.0)
    start = 0.72 - 0.30 * amount
    patches = _ramp(mat, _noise(mat, 5.0, 1.5, grain), (0, 0, 0), (0.6, 0.6, 0.6), (start, start + 0.18))
    _link(mat, _bw(mat, patches), stained.node.inputs['Factor'])
    return stained


def _grime(mat, colour, amount, grain):
    """Smoky dark grime in big high-contrast clouds over `amount` of the surface, like the game's bonebone_d."""
    grimy = _mix(mat, colour, (*GRIME, 1.0), 'MIX', 0.0)
    start = 0.66 - 0.28 * amount
    clouds = _ramp(mat, _noise(mat, 6.0, 6.0, grain), (0, 0, 0), (0.85, 0.85, 0.85), (start, start + 0.10))
    _link(mat, _bw(mat, clouds), grimy.node.inputs['Factor'])
    return grimy


def _worn_edges(mat, colour, light):
    """Every convex edge and ridge lifted towards `light`, the painter's worn highlight (Cycles pointiness)."""
    geometry = _node(mat, 'ShaderNodeNewGeometry')
    edge = _ramp(mat, geometry.outputs['Pointiness'], (0, 0, 0), (0.6, 0.6, 0.6), (0.58, 0.72))
    lifted = _mix(mat, colour, (*light, 1.0), 'MIX', 0.0)
    _link(mat, _bw(mat, edge), lifted.node.inputs['Factor'])
    return lifted


def _top_light(mat, colour):
    """Painted light from above: facing up a shade lighter, facing down a shade darker."""
    geometry = _node(mat, 'ShaderNodeNewGeometry')
    split = _node(mat, 'ShaderNodeSeparateXYZ')
    _link(mat, geometry.outputs['Normal'], split.inputs['Vector'])
    shade = _ramp(mat, split.outputs['Z'], (0.78, 0.78, 0.78), (1.1, 1.1, 1.1), (0.0, 1.0))
    return _mix(mat, colour, shade, 'MULTIPLY', 1.0)


def _new(name, roughness, metallic=0.0):
    mat = bpy.data.materials.new(name)
    bsdf = materials.principled(mat)
    bsdf.inputs['Roughness'].default_value = roughness
    bsdf.inputs['Metallic'].default_value = metallic
    return mat, bsdf


def _node(mat, kind):
    return mat.node_tree.nodes.new(kind)


def _link(mat, a, b):
    mat.node_tree.links.new(a, b)


def _noise(mat, scale, detail, stretch=None):
    """Noise over object coordinates (world ones once the pipeline joins the parts), optionally stretched."""
    coords = _node(mat, 'ShaderNodeTexCoord').outputs['Object']
    if stretch:
        mapping = _node(mat, 'ShaderNodeMapping')
        mapping.inputs['Scale'].default_value = stretch
        _link(mat, coords, mapping.inputs['Vector'])
        coords = mapping.outputs['Vector']
    noise = _node(mat, 'ShaderNodeTexNoise')
    noise.inputs['Scale'].default_value = scale
    noise.inputs['Detail'].default_value = detail
    _link(mat, coords, noise.inputs['Vector'])
    return noise.outputs['Fac']


def _ramp(mat, fac, low, high, stops):
    ramp = _node(mat, 'ShaderNodeValToRGB')
    first, last = ramp.color_ramp.elements
    first.position, last.position = stops
    first.color, last.color = (*low, 1.0), (*high, 1.0)
    _link(mat, fac, ramp.inputs['Fac'])
    return ramp.outputs['Color']


def _mix(mat, a, b, blend, factor):
    """a mixed with b (a socket, or a plain RGBA colour) by `factor`."""
    mix = _node(mat, 'ShaderNodeMix')
    mix.data_type = 'RGBA'
    mix.blend_type = blend
    mix.inputs['Factor'].default_value = factor
    _link(mat, a, mix.inputs['A'])
    if isinstance(b, tuple):
        mix.inputs['B'].default_value = b
    else:
        _link(mat, b, mix.inputs['B'])
    return mix.outputs['Result']


def _cavity(mat, colour, hollow, distance, stops):
    """The colour darkened towards `hollow` where the ambient-occlusion node finds a crevice within `distance`."""
    ao = _node(mat, 'ShaderNodeAmbientOcclusion')
    ao.inputs['Distance'].default_value = distance
    ao.samples = 16
    mask = _ramp(mat, ao.outputs['AO'], (1.0, 1.0, 1.0), (0.0, 0.0, 0.0), stops)
    mix = _node(mat, 'ShaderNodeMix')
    mix.data_type = 'RGBA'
    mix.inputs['B'].default_value = (*hollow, 1.0)
    _link(mat, colour, mix.inputs['A'])
    _link(mat, _bw(mat, mask), mix.inputs['Factor'])
    return mix.outputs['Result']


def _bw(mat, colour):
    node = _node(mat, 'ShaderNodeRGBToBW')
    _link(mat, colour, node.inputs['Color'])
    return node.outputs['Val']


def _bump(mat, height, strength, bsdf):
    node = _node(mat, 'ShaderNodeBump')
    node.inputs['Strength'].default_value = strength
    node.inputs['Distance'].default_value = 0.004
    _link(mat, height, node.inputs['Height'])
    _link(mat, node.outputs['Normal'], bsdf.inputs['Normal'])
