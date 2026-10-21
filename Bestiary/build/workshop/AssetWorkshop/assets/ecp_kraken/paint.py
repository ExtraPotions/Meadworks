"""The kraken's skin as a Cycles node tree on the bake mesh, baked onto the game mesh's atlas.

Colours come in as sRGB hex (as painted), mixed by the corner attributes the geometry writes (under, dorsal, tip, rim,
pit, beak, amber, gloss, inner, lip, lid, barn, crater, mantle; a missing one reads 0) and by noise in object space,
which is Blender world space here: mottling in crimson-brown and dark purple, darker blotches, pale scars and old
sucker rings on the upper side, a pale cream-pink underside, grain like the game's painted textures, ambient occlusion
from the mesh itself. Two outputs: an emission of the colour (the albedo bake) and a BSDF whose normal carries the
skin's bumps and folds (the normal bake).
"""
import bpy


def srgb(hex_colour):
    def channel(c):
        c /= 255.0
        return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4
    return tuple(channel(int(hex_colour[i:i + 2], 16)) for i in (0, 2, 4)) + (1.0,)


RED, PURPLE, DARK, LIGHT = srgb("50231d"), srgb("2f1629"), srgb("170a10"), srgb("6e372c")
DEEP = srgb("221020")
UNDER, UNDER_2 = srgb("b98f7c"), srgb("9c6c5c")
SCAR = srgb("ad8c80")
RIM, PIT = srgb("e2c6b0"), srgb("3b1719")
BEAK, AMBER, GLOSS = srgb("1f140f"), srgb("6e4727"), srgb("a8927e")
INNER, LIP, LID = srgb("330d11"), srgb("7e4640"), srgb("190b0d")
BARN, BARN_DARK, CRATER = srgb("a59c88"), srgb("6d6558"), srgb("342d27")


class Tree:
    """Small helpers over a material's node tree; each returns an output socket."""

    def __init__(self, mat):
        self.tree = mat.node_tree
        self.tree.nodes.clear()
        self.co = self.node('ShaderNodeTexCoord').outputs['Object']

    def node(self, kind, **props):
        node = self.tree.nodes.new(kind)
        for key, value in props.items():
            setattr(node, key, value)
        return node

    def link(self, socket, target):
        if isinstance(socket, (int, float)):
            target.default_value = socket
        elif isinstance(socket, tuple):
            target.default_value = socket
        else:
            self.tree.links.new(socket, target)

    def attr(self, name):
        return self.node('ShaderNodeAttribute', attribute_type='GEOMETRY', attribute_name=name).outputs['Fac']

    def noise(self, scale, detail=4.0, roughness=0.55, distortion=0.0, vector=None):
        n = self.node('ShaderNodeTexNoise')
        n.inputs['Scale'].default_value, n.inputs['Detail'].default_value = scale, detail
        n.inputs['Roughness'].default_value, n.inputs['Distortion'].default_value = roughness, distortion
        self.link(vector or self.co, n.inputs['Vector'])
        return n.outputs['Fac']

    def voronoi(self, scale, feature='F1'):
        v = self.node('ShaderNodeTexVoronoi', feature=feature)
        v.inputs['Scale'].default_value = scale
        self.link(self.co, v.inputs['Vector'])
        return v.outputs['Distance']

    def wave(self, scale, direction, distortion, detail=2.0):
        w = self.node('ShaderNodeTexWave', wave_type='BANDS', bands_direction=direction)
        w.inputs['Scale'].default_value, w.inputs['Distortion'].default_value = scale, distortion
        w.inputs['Detail'].default_value = detail
        self.link(self.co, w.inputs['Vector'])
        return w.outputs['Fac']

    def remap(self, value, a, b, c=0.0, d=1.0, smooth=True):
        m = self.node('ShaderNodeMapRange', interpolation_type='SMOOTHSTEP' if smooth else 'LINEAR', clamp=True)
        self.link(value, m.inputs['Value'])
        for name, v in (('From Min', a), ('From Max', b), ('To Min', c), ('To Max', d)):
            m.inputs[name].default_value = v
        return m.outputs['Result']

    def math(self, op, a, b=0.0):
        m = self.node('ShaderNodeMath', operation=op)
        self.link(a, m.inputs[0])
        self.link(b, m.inputs[1])
        return m.outputs[0]

    def mix(self, a, b, factor, blend='MIX'):
        m = self.node('ShaderNodeMix', data_type='RGBA', blend_type=blend)
        sockets = {s.identifier: s for s in m.inputs}
        self.link(factor, sockets['Factor_Float'])
        self.link(a, sockets['A_Color'])
        self.link(b, sockets['B_Color'])
        return next(s for s in m.outputs if s.identifier == 'Result_Color')

    def position_z(self):
        """Blender Z (Unity Y) of the shading point."""
        sep = self.node('ShaderNodeSeparateXYZ')
        self.link(self.co, sep.inputs['Vector'])
        return sep.outputs['Z']

    def up(self):
        """World Z of the shading normal."""
        sep = self.node('ShaderNodeSeparateXYZ')
        self.link(self.node('ShaderNodeNewGeometry').outputs['Normal'], sep.inputs['Vector'])
        return sep.outputs['Z']


def material(name, head):
    """The bake material: returns it with its two surface sockets in mat['...'] order (emission, bsdf)."""
    mat = bpy.data.materials.new(name)
    t = Tree(mat)
    colour, height = _colour(t, head), _height(t, head)
    colour = _light(t, colour, head)
    emit = t.node('ShaderNodeEmission')
    t.link(colour, emit.inputs['Color'])
    bsdf = t.node('ShaderNodeBsdfPrincipled')
    t.link(colour, bsdf.inputs['Base Color'])
    bump = t.node('ShaderNodeBump')
    bump.inputs['Strength'].default_value, bump.inputs['Distance'].default_value = 0.55, 0.02
    t.link(height, bump.inputs['Height'])
    t.link(bump.outputs['Normal'], bsdf.inputs['Normal'])
    out = t.node('ShaderNodeOutputMaterial')
    t.link(bsdf.outputs['BSDF'], out.inputs['Surface'])
    mat["kraken_outputs"] = True
    return mat


def use_output(mat, which):
    """Routes 'emit' (the albedo bake) or 'bsdf' (the normal bake) to the material output."""
    nodes = mat.node_tree.nodes
    out = next(n for n in nodes if n.bl_idname == 'ShaderNodeOutputMaterial')
    kind = 'ShaderNodeEmission' if which == 'emit' else 'ShaderNodeBsdfPrincipled'
    source = next(n for n in nodes if n.bl_idname == kind)
    mat.node_tree.links.new(source.outputs[0], out.inputs['Surface'])


def _colour(t, head):
    big, mid = t.noise(0.55, 3, 0.5), t.noise(2.2, 6, 0.6, 0.3)
    col = t.mix(RED, PURPLE, t.remap(big, 0.36, 0.64))
    col = t.mix(col, LIGHT, t.math('MULTIPLY', t.remap(mid, 0.42, 0.30), 0.55))
    col = t.mix(col, DARK, t.math('MULTIPLY', t.remap(mid, 0.54, 0.66), 0.85))
    spots = t.remap(t.voronoi(9.0), 0.16, 0.06)
    col = t.mix(col, DARK, t.math('MULTIPLY', spots, t.math('MULTIPLY', t.remap(big, 0.42, 0.58), 0.6)))
    cells = t.remap(t.voronoi(5.5, 'DISTANCE_TO_EDGE'), 0.05, 0.0)
    col = t.mix(col, DARK, t.math('MULTIPLY', cells, 0.35))
    dorsal = t.math('ADD', t.attr('dorsal'), t.math('MULTIPLY', t.attr('mantle'), 0.9))
    tint = t.math('MAXIMUM', t.math('MULTIPLY', dorsal, 0.5), t.math('MULTIPLY', t.attr('tip'), 0.45))
    col = t.mix(col, DEEP, tint)
    scars = t.math('MULTIPLY', _scars(t), t.math('SUBTRACT', 1.0, t.attr('under')))
    if head:   # fewer on the column, which is mostly under water
        scars = t.math('MULTIPLY', scars, t.remap(t.position_z(), -3.5, 1.2, 0.0, 1.0))
    col = t.mix(col, SCAR, scars)
    under = t.remap(t.math('ADD', t.attr('under'), t.math('MULTIPLY', t.math('SUBTRACT', mid, 0.5), 0.6)), 0.33, 0.62)
    col = t.mix(col, t.mix(UNDER, UNDER_2, t.remap(big, 0.3, 0.7)), under)
    col = t.mix(col, LIP, t.math('MULTIPLY', t.attr('lip'), 0.75))
    col = t.mix(col, RIM, t.attr('rim'))
    col = t.mix(col, PIT, t.attr('pit'))
    col = t.mix(col, LID, t.attr('lid'))
    col = t.mix(col, t.mix(BARN, BARN_DARK, t.remap(t.noise(22.0, 3), 0.35, 0.65)), t.attr('barn'))
    col = t.mix(col, CRATER, t.attr('crater'))
    beak = t.mix(t.mix(BEAK, AMBER, t.attr('amber')), GLOSS, t.math('MULTIPLY', t.attr('gloss'), 0.7))
    col = t.mix(col, beak, t.attr('beak'))
    return t.mix(col, INNER, t.attr('inner'))


def _scars(t):
    """Pale healed lines, and a few rings left by the suckers of something it fought."""
    lines = t.remap(t.wave(0.9, 'DIAGONAL', 7.0, 3.0), 0.935, 0.985)
    lines = t.math('MULTIPLY', lines, t.remap(t.noise(0.32, 2), 0.57, 0.65))
    ring = t.remap(t.math('ABSOLUTE', t.math('SUBTRACT', t.voronoi(1.7), 0.3)), 0.035, 0.012)
    ring = t.math('MULTIPLY', ring, t.remap(t.noise(0.45, 2), 0.63, 0.7))
    return t.math('MULTIPLY', t.math('MAXIMUM', lines, ring), 0.8)


def _light(t, col, head):
    """Grain like the game's painted textures, a painted top light on the head, ambient occlusion from the mesh."""
    col = t.mix(col, t.remap(t.noise(38.0, 2), 0.3, 0.7, 0.86, 1.1, smooth=False), 1.0, blend='MULTIPLY')
    if head:
        col = t.mix(col, t.remap(t.up(), -1.0, 1.0, 0.8, 1.08, smooth=False), 1.0, blend='MULTIPLY')
    ao = t.node('ShaderNodeAmbientOcclusion', samples=16, only_local=True)
    ao.inputs['Distance'].default_value = 0.9 if head else 0.35
    shade = t.remap(ao.outputs['AO'], 0.0, 1.0, 0.35, 1.0, smooth=False)
    return t.mix(col, shade, 1.0, blend='MULTIPLY')


def _height(t, head):
    """Bumps: skin grain everywhere, folds across the tentacle, papillae on the mantle, folds under the eyes."""
    fine, grain = t.noise(9.0, 4), t.noise(28.0, 2)
    h = t.math('ADD', t.math('MULTIPLY', fine, 0.5), t.math('MULTIPLY', grain, 0.25))
    if head:
        papillae = t.math('MULTIPLY', t.remap(t.voronoi(6.5), 0.35, 0.0), t.attr('mantle'))
        folds = t.math('MULTIPLY', t.wave(3.2, 'Z', 5.0), t.math('SUBTRACT', 1.0, t.attr('mantle')))
        h = t.math('ADD', h, t.math('ADD', t.math('MULTIPLY', papillae, 0.8), t.math('MULTIPLY', folds, 0.35)))
    else:
        folds = t.wave(2.6, 'Y', 3.0)
        h = t.math('ADD', h, t.math('MULTIPLY', folds, t.math('SUBTRACT', 0.45, t.math('MULTIPLY', t.attr('under'), 0.3))))
    h = t.math('ADD', h, t.math('MULTIPLY', t.noise(30.0, 3), t.math('MULTIPLY', t.attr('barn'), 1.5)))
    return t.math('MULTIPLY', h, t.math('SUBTRACT', 1.0, t.attr('beak')))
