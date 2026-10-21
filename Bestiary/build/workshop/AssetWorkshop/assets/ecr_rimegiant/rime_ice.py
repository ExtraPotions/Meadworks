"""Rime ice and snow for the Rime Giant's parts: meshes built face by face with a colour on every face corner (a
faceted look the pipeline bakes into the atlas), the ice and snow colours, crystals, and the one material that reads
the colours and adds frost.

Colours are given as the game's painters would pick them (sRGB hex) and stored linear. The ice is muted pale cyan-grey
like the game's own ice (the Barka's): DEEP blue-grey towards the body and on the undersides, MID and PALE on the faces
that catch the light, soft near-white FROST on rims and crystal tips, with dark painted CRACK lines. The snow is white
in patches over grey RIME, like the snow on the game's mountain rocks. `Builder.finish` can also shade every facet by
which way it faces, so the ice reads as a few big facets from far off.
"""
import math
import random

import bmesh
import bpy
from mathutils import Vector

COLOUR = "rime_colour"


def srgb(hex_colour):
    """A painter's colour (sRGB hex) as linear RGBA."""
    def channel(c):
        c /= 255.0
        return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4
    return tuple(channel(int(hex_colour[i:i + 2], 16)) for i in (0, 2, 4)) + (1.0,)


DEEP = srgb("3A5C6E")
MID = srgb("5F8FA6")
PALE = srgb("98C6D4")
FROST = srgb("DDEFF4")
CRACK = srgb("4F6878")
SNOW = srgb("EEF0F2")
SNOW_SHADE = srgb("A3AEB8")
RIME = srgb("8E99A3")
RIME_DARK = srgb("6F7B85")


def mix(a, b, t):
    return tuple(x + (y - x) * t for x, y in zip(a, b))


RAMP = ((0.0, MID), (0.5, PALE), (1.0, mix(PALE, FROST, 0.55)))
SNOW_RAMP = ((0.2, mix(SNOW_SHADE, SNOW, 0.35)), (0.6, mix(SNOW_SHADE, SNOW, 0.8)), (0.85, SNOW))
CRAG_RAMP = ((0.3, RIME_DARK), (0.6, RIME), (0.76, SNOW))   # grey rime on the sides, snow on the tops


def ramp(t, stops=RAMP):
    """The colour at t along the stops, clamped at the ends."""
    if t <= stops[0][0]:
        return stops[0][1]
    for (t0, c0), (t1, c1) in zip(stops, stops[1:]):
        if t <= t1:
            return mix(c0, c1, (t - t0) / (t1 - t0))
    return stops[-1][1]


class Builder:
    """One mesh built face by face; every corner of a face gets its own colour."""

    def __init__(self, convert=None):
        self.bm = bmesh.new()
        self.colour = self.bm.loops.layers.float_color.new(COLOUR)
        self.convert = convert or (lambda p: tuple(p))

    def vert(self, point):
        return self.bm.verts.new(self.convert(point))

    def face(self, verts, colours):
        face = self.bm.faces.new(verts)
        for loop, colour in zip(face.loops, colours):
            loop[self.colour] = colour
        return face

    def hull(self, points, colour):
        """The convex hull of the points (a faceted chunk), all of it one colour until shaded."""
        verts = [self.vert(p) for p in points]
        result = bmesh.ops.convex_hull(self.bm, input=verts)
        bmesh.ops.delete(self.bm, geom=[v for v in result['geom_interior'] if isinstance(v, bmesh.types.BMVert)], context='VERTS')
        for face in (g for g in result['geom'] if isinstance(g, bmesh.types.BMFace)):
            for loop in face.loops:
                loop[self.colour] = colour

    def shade(self, light, weight, jitter, seed, stops=None):
        """Blends every facet's colour towards the ramp colour for how squarely it faces `light` (source axes)."""
        rng = random.Random(seed)
        light = Vector(self.convert(light)).normalized()
        for face in self.bm.faces:
            tone = ramp((face.normal.dot(light) + 1.0) / 2.0 + rng.uniform(-jitter, jitter), stops or RAMP)
            for loop in face.loops:
                loop[self.colour] = mix(loop[self.colour], tone, weight)

    def close(self):
        """Merges touching corners and turns every face outwards (every piece is closed)."""
        bmesh.ops.remove_doubles(self.bm, verts=self.bm.verts, dist=1e-5)
        bmesh.ops.recalc_face_normals(self.bm, faces=self.bm.faces)
        self.bm.normal_update()

    def turn_up(self):
        """Turns an open sheet (close() cannot tell its outside) so that, taken together, it faces up."""
        if sum(face.normal.z * face.calc_area() for face in self.bm.faces) < 0:
            bmesh.ops.reverse_faces(self.bm, faces=self.bm.faces[:])
            self.bm.normal_update()

    def finish(self, name, material):
        """The mesh as an object in the scene."""
        mesh = bpy.data.meshes.new(name)
        self.bm.to_mesh(mesh)
        self.bm.free()
        obj = bpy.data.objects.new(name, mesh)
        bpy.context.scene.collection.objects.link(obj)
        mesh.materials.append(material)
        return obj


def crystal(builder, base, axis, length, radius, twist=0.0, sides=6, root=DEEP):
    """A pointed ice prism from `base` along `axis`: its root colour at the base, pale up the sides, a frosted tip."""
    axis = Vector(axis).normalized()
    side_a = axis.orthogonal().normalized()
    side_b = axis.cross(side_a)
    base = Vector(base)

    def ring(centre, r):
        return [builder.vert(centre + (side_a * math.cos(twist + 2 * math.pi * k / sides)
                                       + side_b * math.sin(twist + 2 * math.pi * k / sides)) * r) for k in range(sides)]

    bottom, top = ring(base, radius * 0.8), ring(base + axis * length * 0.66, radius)
    tip = builder.vert(base + axis * length)
    builder.face(list(reversed(bottom)), [root] * sides)
    for k in range(sides):
        n = (k + 1) % sides
        builder.face([bottom[k], bottom[n], top[n], top[k]], [root, root, PALE, PALE])
        builder.face([top[k], top[n], tip], [PALE, PALE, FROST])


def chunk(builder, centre, radii, rng, colour=PALE, points=14):
    """A faceted lump of ice: the hull of points scattered over an ellipsoid with these half-sizes (source axes)."""
    scattered = []
    for _ in range(points):
        direction = Vector((rng.gauss(0, 1), rng.gauss(0, 1), rng.gauss(0, 1))).normalized()
        reach = rng.uniform(0.75, 1.0)
        scattered.append(Vector(centre) + Vector(tuple(d * r * reach for d, r in zip(direction, radii))))
    builder.hull(scattered, colour)


def crag(builder, centre, normal, size, rng, colour, points=10):
    """A craggy lump clinging to a surface: the hull of points over an ellipsoid squashed along the surface's normal."""
    normal = Vector(normal).normalized()
    side_a = normal.orthogonal().normalized()
    side_b = normal.cross(side_a)
    scattered = []
    for _ in range(points):
        d = Vector((rng.gauss(0, 1), rng.gauss(0, 1), rng.gauss(0, 1))).normalized()
        reach = rng.uniform(0.75, 1.0) * size
        scattered.append(Vector(centre) + (side_a * d.x + side_b * d.y * 0.85 + normal * d.z * 0.6) * reach)
    builder.hull(scattered, colour)


def material(name, snow=False):
    """The corner colours, painted over: ice gets soft dark crack lines, pale frost patches and near-white highlights
    along its ridges (where the mesh is convex), snow lies in white patches over grey rime. Low-frequency throughout,
    like the game's hand-painted textures; the cracks (or the snow's patch edges) are the only bumps in the normal map."""
    mat = bpy.data.materials.new(name)
    nodes, links = mat.node_tree.nodes, mat.node_tree.links
    bsdf = next(n for n in nodes if n.bl_idname == 'ShaderNodeBsdfPrincipled')
    bsdf.inputs['Roughness'].default_value = 0.5
    colour = nodes.new('ShaderNodeAttribute')
    colour.attribute_name = COLOUR
    painted, height = (_snow_paint if snow else _ice_paint)(nodes, colour.outputs['Color'])
    links.new(painted, bsdf.inputs['Base Color'])
    bump = nodes.new('ShaderNodeBump')
    bump.inputs['Strength'].default_value = 0.45
    bump.inputs['Distance'].default_value = 0.03
    links.new(height, bump.inputs['Height'])
    links.new(bump.outputs['Normal'], bsdf.inputs['Normal'])
    return mat


def _ice_paint(nodes, colour):
    """Ice: frost patches and ridge highlights lighten it, two crack networks darken it; the cracks are the bumps."""
    cracks = _cracks(nodes, 1.4, 0.035, 0.55)
    fine = _cracks(nodes, 3.2, 0.02, 0.2)
    frost = _scaled(nodes, _ramped(nodes, _noise(nodes, 1.2, 2.0, 0.0).outputs['Fac'], 0.55, 0.75), 0.3)
    ridges = _scaled(nodes, _ramped(nodes, nodes.new('ShaderNodeNewGeometry').outputs['Pointiness'], 0.5, 0.6), 0.6)
    lit = _mix(nodes, _mix(nodes, colour, FROST, frost), FROST, ridges)
    return _mix(nodes, _mix(nodes, lit, CRACK, cracks), CRACK, fine), _invert(nodes, cracks)


def _snow_paint(nodes, colour):
    """Snow: grey rime shows through wherever a slow noise leaves it bare; the patches' edges are the bumps."""
    patches = _ramped(nodes, _noise(nodes, 1.1, 3.0, 0.4).outputs['Fac'], 0.44, 0.54)
    bare = _invert(nodes, patches)
    return _mix(nodes, colour, RIME, _scaled(nodes, bare, 0.4)), patches


def _socket(sockets, identifier):
    """A Mix node has a socket of each type under one name; the identifier tells them apart."""
    return next(s for s in sockets if s.identifier == identifier)


def _coords(nodes, wobble):
    """Object coordinates (the part's own metres), bent by a slow noise so painted lines wander like brush strokes."""
    coords = nodes.new('ShaderNodeTexCoord').outputs['Object']
    if wobble <= 0:
        return coords
    noise = nodes.new('ShaderNodeTexNoise')
    noise.inputs['Scale'].default_value = 1.5
    noise.inputs['Detail'].default_value = 2.0
    nodes.id_data.links.new(coords, noise.inputs['Vector'])
    bend = nodes.new('ShaderNodeVectorMath')
    bend.operation = 'MULTIPLY_ADD'
    nodes.id_data.links.new(noise.outputs['Color'], bend.inputs[0])
    bend.inputs[1].default_value = (wobble, wobble, wobble)
    nodes.id_data.links.new(coords, bend.inputs[2])
    return bend.outputs['Vector']


def _noise(nodes, scale, detail, wobble):
    noise = nodes.new('ShaderNodeTexNoise')
    noise.inputs['Scale'].default_value = scale
    noise.inputs['Detail'].default_value = detail
    nodes.id_data.links.new(_coords(nodes, wobble), noise.inputs['Vector'])
    return noise


def _cracks(nodes, scale, width, strength):
    """A soft painted crack network: 1 on a line, fading to 0 `width` away from it, times `strength`."""
    cells = nodes.new('ShaderNodeTexVoronoi')
    cells.feature = 'DISTANCE_TO_EDGE'
    cells.inputs['Scale'].default_value = scale
    nodes.id_data.links.new(_coords(nodes, 0.18), cells.inputs['Vector'])
    line = _invert(nodes, _ramped(nodes, cells.outputs['Distance'], 0.0, width))
    return _scaled(nodes, line, strength)


def _ramped(nodes, value, low, high):
    """0 below `low`, 1 above `high`, smooth between."""
    ramp = nodes.new('ShaderNodeMapRange')
    ramp.interpolation_type = 'SMOOTHSTEP'
    ramp.inputs['From Min'].default_value, ramp.inputs['From Max'].default_value = low, high
    nodes.id_data.links.new(value, ramp.inputs['Value'])
    return ramp.outputs['Result']


def _invert(nodes, value):
    node = nodes.new('ShaderNodeMath')
    node.operation = 'SUBTRACT'
    node.inputs[0].default_value = 1.0
    nodes.id_data.links.new(value, node.inputs[1])
    return node.outputs['Value']


def _scaled(nodes, value, factor):
    node = nodes.new('ShaderNodeMath')
    node.operation = 'MULTIPLY'
    node.inputs[1].default_value = factor
    nodes.id_data.links.new(value, node.inputs[0])
    return node.outputs['Value']


def _mix(nodes, colour, towards, factor):
    """colour blended towards a fixed colour by the factor socket."""
    node = nodes.new('ShaderNodeMix')
    node.data_type = 'RGBA'
    nodes.id_data.links.new(colour, _socket(node.inputs, 'A_Color'))
    _socket(node.inputs, 'B_Color').default_value = towards
    nodes.id_data.links.new(factor, _socket(node.inputs, 'Factor_Float'))
    return _socket(node.outputs, 'Result_Color')
