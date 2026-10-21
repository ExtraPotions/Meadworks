"""The layers a paint recipe stacks, each a function taking the colour so far and returning the new colour socket:
blotches in the family's tones, grain streaks, accent patches, stains, smoky grime, per-texel jitter, worn edges,
dark hollows and light from above. Sizes are in metres (object coordinates are world metres after the join).

Blender's noise was measured by baking it (codex paint.md): at detail 5 and roughness 0.7 its Fac has 10/50/90 %
points 0.422/0.498/0.574 and falls to half correlation at 0.22/scale metres; NOISE holds those numbers so a ramp can
put the family's 10 and 90 % tones exactly on the noise's 10 and 90 % points. Roughness 0.7 gives every octave about
the same share of the variation, as the game's paint has; the noise starts `reach` times the family's blotch size up,
so the coat has the game's broad washes as well as its blotches.
"""
from . import paint_nodes as nodes

NOISE = {"detail": 5.0, "roughness": 0.7, "half_spread": 0.076, "correlation": 0.22, "reach": 1.5}
GRAIN = {"detail": 2.0, "roughness": 0.5, "half_spread": 0.105, "correlation": 0.31}


def scale_for(size_m, table=NOISE):
    """The noise scale whose blotches are size_m across (a blotch spans about two correlation lengths)."""
    return 2.0 * table["correlation"] / max(size_m, 1e-4)


SMOOTH = {"detail": 2.0, "roughness": 0.7, "half_spread": 0.097, "correlation": 0.25}


def blotches(mat, tones, size_m, stretch=(1, 1, 1), contrast=1.0, offset=(0, 0, 0), smooth=False):
    """The base coat: soft blotches size_m across, the family's dark, mid and light tones on the noise's 10, 50 and
    90 % points (so a tenth of the surface is at or below the dark tone). contrast above 1 puts them closer to the
    middle, for a part too small to hold the family's biggest blotches (its whole spread shows anyway). smooth leaves
    out the small octaves (teeth, crystal: soft gradients with no mottling)."""
    table = SMOOTH if smooth else NOISE
    vector = nodes.coords(mat, stretch, offset)
    fac = nodes.noise(mat, scale_for(size_m * NOISE["reach"], table), table["detail"], vector, table["roughness"])
    spread = table["half_spread"] / contrast
    dark, mid, light = tones
    return nodes.ramp(mat, fac, [(0.5 - spread, dark), (0.5, mid), (0.5 + spread, light)])


def scaled(mat, colour, fac, amount, spread, centre=0.5):
    """colour times (1 + amount) where fac is `spread` above centre, times (1 - amount) as far below, linearly."""
    factor = nodes.math(mat, 'MULTIPLY_ADD', fac, amount / spread)
    factor.node.inputs[2].default_value = 1.0 - centre * amount / spread
    scale = nodes.node(mat, 'ShaderNodeVectorMath', operation='SCALE')
    nodes.link(mat, colour, scale.inputs[0])
    nodes.link(mat, nodes.math(mat, 'MAXIMUM', factor, 0.0), scale.inputs['Scale'])
    return scale.outputs['Vector']


def grain(mat, colour, amount, size_m, stretch, offset=(3.1, 7.7, 1.3), rotation=(0, 0, 0)):
    """Streaks: a noise drawn out by `stretch` (wood grain, straw, strands) lightening and darkening by amount."""
    if amount <= 0:
        return colour
    fac = nodes.noise(mat, scale_for(size_m, GRAIN), GRAIN["detail"], nodes.coords(mat, stretch, offset, rotation),
                      GRAIN["roughness"])
    return scaled(mat, colour, fac, amount, GRAIN["half_spread"])


def patches(mat, colour, paint, cover, size_m, softness=0.06, offset=(9.3, 2.1, 5.7), stretch=(1, 1, 1),
            strength=1.0):
    """`paint` (a colour) over about `cover` (0 to 1) of the surface in soft-edged patches size_m across: stains,
    rust, moss, a second hue."""
    if cover <= 0:
        return colour
    fac = nodes.noise(mat, scale_for(size_m), 2.0, nodes.coords(mat, stretch, offset), 0.5)
    start = 0.5 + 0.082 * _normal_quantile(1.0 - cover)      # Fac's standard deviation is 0.082 at detail 2
    weight = nodes.math(mat, 'MULTIPLY', nodes.mask(mat, fac, start - softness / 2, start + softness / 2), strength)
    return nodes.mix(mat, colour, paint, weight)


def _normal_quantile(p):
    """Rough standard-normal quantile (the noise's Fac is near normal): enough to place a coverage threshold."""
    table = [(0.05, -1.64), (0.1, -1.28), (0.2, -0.84), (0.3, -0.52), (0.4, -0.25), (0.5, 0.0), (0.6, 0.25),
             (0.7, 0.52), (0.8, 0.84), (0.9, 1.28), (0.95, 1.64)]
    p = min(max(p, 0.05), 0.95)
    for (p0, z0), (p1, z1) in zip(table, table[1:]):
        if p0 <= p <= p1:
            return z0 + (z1 - z0) * (p - p0) / (p1 - p0)
    return 0.0


def jitter(mat, colour, amount, cell_m):
    """Per-texel jitter: every cube of cell_m metres (one texel at the asset's density) a little lighter or darker,
    the pixel-level noise hand-painted textures carry (the game's finest band holds 5 to 40 % of the variation)."""
    if amount <= 0:
        return colour
    return scaled(mat, colour, nodes.white(mat, cell_m), amount, 0.5)


def worn_edges(mat, colour, light, amount, radius):
    """Hard convex edges lifted towards `light`: bevel-normal edges that are not in a hollow (AO near 1). A smooth
    thin tube has no hard edges, so it is not lifted all over the way pointiness lifts it."""
    if amount <= 0:
        return colour
    edge = nodes.mask(mat, nodes.edge_strength(mat, radius), 0.05, 0.35)
    open_air = nodes.mask(mat, nodes.occlusion(mat, radius * 3.0, 8), 0.75, 0.95)
    weight = nodes.math(mat, 'MULTIPLY', nodes.math(mat, 'MULTIPLY', edge, open_air), amount)
    return nodes.mix(mat, colour, light, weight)


def hollows(mat, colour, dark, amount, distance):
    """Crevices within `distance` metres darkened towards `dark` (the game paints gaps near black)."""
    if amount <= 0:
        return colour
    shade = nodes.mask(mat, nodes.occlusion(mat, distance, 16), 0.95, 0.35)
    return nodes.mix(mat, colour, dark, nodes.math(mat, 'MULTIPLY', shade, amount))


def from_above(mat, colour, amount):
    """Light from above: faces looking up kept, faces looking down darkened by amount (the game's creatures are
    about 25 % darker underneath; its pieces are not lit this way)."""
    if amount <= 0:
        return colour
    up = nodes.math(mat, 'MULTIPLY_ADD', nodes.axis(mat, 'Z', nodes.geometry(mat).outputs['Normal']), 0.5)
    up.node.inputs[2].default_value = 0.5                          # normal z -1..1 -> 0..1
    shade = nodes.ramp(mat, up, [(0.0, (1 - amount,) * 3), (0.8, (1.0, 1.0, 1.0))])
    return nodes.mix(mat, colour, shade, 1.0, 'MULTIPLY')


def relief(mat, bsdf, colour, strength, density):
    """The normal as the game makes it: the painted value read as height (light raised, dark sunk). The game's normal
    maps follow the albedo's slope at `strength` normal units per unit of value per texel, so the bump distance is
    strength / density metres. Value is taken as luminance to the power 1/2.2 (near sRGB value)."""
    if strength <= 0:
        return
    value = nodes.math(mat, 'POWER', nodes.bw(mat, colour), 1 / 2.2)
    nodes.link(mat, nodes.bump(mat, value, strength / density), bsdf.inputs['Normal'])
