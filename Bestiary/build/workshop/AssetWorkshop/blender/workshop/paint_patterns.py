"""The patterns only some families have, each drawn over the colour so far: plank seams, bark fissures, horn growth
bands, strands (straw, fur), a woven cloth checker, a rope's twist, cracks, veins, diagonal chisel hatching (grausten)
and the fat marbling of meat. `draw(mat, colour, spec)` draws the spec's pattern, if it has one."""
import math

from . import paint_nodes as nodes
from . import paint_layers as layers

AXES = "XYZ"


def draw(mat, colour, spec):
    pattern = spec.get("pattern")
    if not pattern:
        return colour
    name, size = pattern[:2]
    spec = dict(spec, strength=pattern[2]) if len(pattern) > 2 else spec
    return globals()["_" + name](mat, colour, spec, size)


def along(spec, factor):
    """A stretch that draws patterns out along the spec's axis by 1/factor."""
    axis = spec.get("axis", "Z")
    return tuple(factor if a == axis else 1.0 for a in AXES)


def _across(spec):
    """The axis plank seams repeat along: the first one that is not the grain axis, Z before the others."""
    axis = spec.get("axis", "X")
    return spec.get("across") or next(a for a in "ZYX" if a != axis)


def _dark(spec, factor=0.45):
    return tuple(c * factor for c in spec["tones"][0])


def _light(spec, factor=1.25):
    return tuple(min(1.0, c * factor) for c in spec["tones"][2])


def _seams(mat, colour, spec, width):
    """Planks `width` metres wide: each plank a shade lighter or darker, a dark line a texel wide between them."""
    place = nodes.math(mat, 'DIVIDE', nodes.axis(mat, _across(spec)), width)
    plank = nodes.node(mat, 'ShaderNodeTexWhiteNoise', noise_dimensions='1D')
    nodes.link(mat, nodes.math(mat, 'FLOOR', place), plank.inputs['W'])
    colour = layers.scaled(mat, colour, plank.outputs['Value'], 0.1, 0.5)
    fraction = nodes.math(mat, 'PINGPONG', place, 0.5)          # 0 at a seam, 0.5 mid-plank
    line = nodes.mask(mat, nodes.math(mat, 'MULTIPLY', fraction, width), 1.2 / spec["density"], 0.4 / spec["density"])
    return nodes.mix(mat, colour, _dark(spec, 0.6), nodes.math(mat, 'MULTIPLY', line, 0.6))


def _fissures(mat, colour, spec, size):
    """Bark: long cells drawn out along the trunk, dark cracks between raised ridges."""
    cells = nodes.voronoi(mat, 1.0 / size, vector=nodes.coords(mat, along(spec, 0.12)))
    crack = nodes.mask(mat, cells, 0.12, 0.02)
    ridge = layers.scaled(mat, colour, nodes.mask(mat, cells, 0.05, 0.4), 0.15, 0.5)
    return nodes.mix(mat, ridge, _dark(spec, 0.5), nodes.math(mat, 'MULTIPLY', crack, 0.85))


def _bands(mat, colour, spec, spacing):
    """Horn: growth bands across the axis, lighter and darker by 12 % (the spec's pattern strength)."""
    wave = nodes.node(mat, 'ShaderNodeTexWave', wave_type='BANDS', bands_direction=spec.get("axis", "Z"))
    wave.inputs['Scale'].default_value = 1.0 / (spacing * 6.28)
    wave.inputs['Distortion'].default_value = 3.0
    nodes.link(mat, nodes.coords(mat), wave.inputs['Vector'])
    return layers.scaled(mat, colour, wave.outputs['Fac'], spec.get("strength", 0.12), 0.5)


def _strands(mat, colour, spec, width):
    """Straw and fur: strands `width` metres across drawn along the axis, light and dark by 25 % (the spec's pattern
    strength)."""
    fac = nodes.noise(mat, 1.0 / width, 1.0, nodes.coords(mat, along(spec, 0.04), (1.7, 4.2, 0.3)))
    return layers.scaled(mat, colour, fac, spec.get("strength", 0.25), 0.12)


def _weave(mat, colour, spec, thread):
    """Cloth: a checker of threads `thread` metres apart (about two texels), 6 % either way."""
    checker = nodes.node(mat, 'ShaderNodeTexChecker')
    checker.inputs['Scale'].default_value = 1.0 / (2.0 * thread)
    checker.inputs['Color1'].default_value = (1.0, 1.0, 1.0, 1.0)
    checker.inputs['Color2'].default_value = (0.0, 0.0, 0.0, 1.0)
    nodes.link(mat, nodes.coords(mat), checker.inputs['Vector'])
    return layers.scaled(mat, colour, checker.outputs['Fac'], 0.06, 0.5)


def _twist(mat, colour, spec, pitch):
    """Rope: three strands wound round the axis every `pitch` metres, dark between strands."""
    x, y = [nodes.axis(mat, a) for a in AXES if a != spec.get("axis", "Z")][:2]
    turn = nodes.math(mat, 'MULTIPLY', nodes.math(mat, 'ARCTAN2', y, x), 3.0 / (2 * math.pi))
    phase = nodes.math(mat, 'ADD', turn, nodes.math(mat, 'DIVIDE', nodes.axis(mat, spec.get("axis", "Z")), pitch))
    strand = nodes.math(mat, 'PINGPONG', phase, 0.5)
    return nodes.mix(mat, colour, _dark(spec, 0.6), nodes.mask(mat, strand, 0.12, 0.0))


def _cracks(mat, colour, spec, size):
    """Thin cracks along cell edges about `size` metres apart, only some of them; dark on stone, light on ice."""
    wobble = nodes.coords(mat, (1, 1, 1), (5.1, 2.7, 8.3))
    edge = nodes.voronoi(mat, 1.0 / size, vector=wobble)
    line = nodes.mask(mat, edge, 1.2 / (spec["density"] * size), 0.2 / (spec["density"] * size))
    keep = nodes.mask(mat, nodes.noise(mat, 1.5 / size, 1.0, nodes.coords(mat, offset=(2.2, 9.1, 4.4))), 0.5, 0.56)
    paint = _light(spec, 1.4) if spec["family"] == "crystal.ice" else _dark(spec, 0.5)
    return nodes.mix(mat, colour, paint, nodes.math(mat, 'MULTIPLY', line, keep))


def _veins(mat, colour, spec, size):
    """Marble and leaves: thin pale veins where a warped noise crosses its middle."""
    fac = nodes.noise(mat, 1.0 / size, 3.0, nodes.coords(mat), 0.55, distortion=1.5)
    distance = nodes.math(mat, 'ABSOLUTE', nodes.math(mat, 'SUBTRACT', fac, 0.5))
    vein = nodes.mask(mat, distance, 0.012, 0.003)
    return nodes.mix(mat, colour, _light(spec, 1.8), nodes.math(mat, 'MULTIPLY', vein, 0.8))


def _hatch(mat, colour, spec, size):
    """Grausten: short diagonal chisel strokes, lighter and darker by 12 % (the spec's pattern strength)."""
    fac = nodes.noise(mat, 1.0 / size, 1.0, nodes.coords(mat, (1.0, 0.08, 1.0), rotation=(0.0, 0.0, 0.785)))
    return layers.scaled(mat, colour, fac, spec.get("strength", 0.12), 0.105)


def _marbling(mat, colour, spec, size):
    """Meat: pale streaks of fat through the red."""
    fac = nodes.noise(mat, 1.0 / size, 2.0, nodes.coords(mat, along(spec, 0.4)), 0.5, distortion=2.0)
    fat = nodes.mask(mat, fac, 0.58, 0.64)
    return nodes.mix(mat, colour, _light(spec, 1.35), nodes.math(mat, 'MULTIPLY', fat, 0.7))
