"""Paint recipes that make a part look painted by the game's own artists, one per material family measured in the codex
(AssetWorkshop/codex/paint.md): Principled BSDF node setups the pipeline's bake reads (Base Color through the emission
pass, Normal from the bump), built from the codex's numbers.

    from workshop import paint
    haft = paint.wood_logs("haft", axis="Z")                  # grain along the haft
    blade = paint.iron("blade")
    grip = paint.leather("grip", region="trim")               # paint region for recolouring
    cloth = paint.linen("banner", dye="red")                  # or tint="#3a5a8a": any colour, same light and grime
    paint.make("stone.stone", "wall", patches=[("moss", 0.2, 0.3)])

Every recipe stacks, in order: blotches in the family's measured dark, mid and light tones (the game's 10/50/90 %
luminance tones) at its measured blotch size; grain streaks; the family's pattern (seams, fissures, strands, weave,
cracks, veins...); stains, rust or grime patches; per-texel jitter (the game's finest band); a bump from the painted
value itself (the game's normal maps are the albedo's own relief); worn hard edges; dark hollows; light from above
(creature skin and fur only: the game's pieces carry none). Each material gets custom properties `region` (the paint
region the recolour tool masks by) and `paint_family`.

Overrides (any recipe): tones=(dark, mid, light) linear rgb, tint="#rrggbb" or (r, g, b), preset (tones of one
game texture: paint_specs.PRESETS, e.g. skin preset="troll", cloth dye="red"), region, axis ('X', 'Y', 'Z':
grain direction), blotch_m, density (px per metre of the asset: sets the
texel jitter and bump), jitter, relief, edges, hollows, top, patches=[(colour name or '#hex', cover, size_m)],
pattern=(name, size) or None, grain=(amount, size_m, stretch) or None, rough, metal.
"""
from . import paint_layers as layers
from . import paint_nodes as nodes
from . import paint_patterns as patterns
from . import paint_specs as specs


def make(family, name=None, **overrides):
    """The recipe of one family ('wood.planks', 'metal.iron' ...) as a new material."""
    spec = specs.spec(family, overrides)
    mat, bsdf = nodes.new_material(name or spec["fn"], spec.get("rough", 0.8), spec.get("metal", 0.0))
    coat = _paint(mat, spec)
    layers.relief(mat, bsdf, coat, spec["relief"], spec["density"])
    colour = layers.jitter(mat, coat, spec["jitter"] * spec.get("jitter_scale", 1.0), 1.0 / spec["density"])
    nodes.link(mat, _finish(mat, colour, spec), bsdf.inputs['Base Color'])
    mat["region"] = spec["region"]
    mat["paint_family"] = family
    return mat


def _paint(mat, spec):
    """The painted coat before light and texel jitter: blotches, grain, pattern, patches. The bump reads this coat:
    the bake's bump cannot see a per-texel step or a ray-traced hollow (see paint_normal for the game's way)."""
    colour = layers.blotches(mat, spec["tones"], spec["blotch_m"], patterns.along(spec, spec.get("along", 1.0)),
                             spec.get("contrast", 1.0), smooth=spec.get("smooth", False))
    if spec.get("grain"):
        amount, size, stretch = spec["grain"]
        colour = layers.grain(mat, colour, amount, size, patterns.along(spec, stretch))
    colour = patterns.draw(mat, colour, spec)
    for paint, cover, size in spec.get("patches", []):
        tone = specs.hex_to_linear(specs.COLOURS.get(paint, paint)) if isinstance(paint, str) else paint
        colour = layers.patches(mat, colour, tone, cover, size, offset=(len(paint) * 1.7, 4.4, 2.9))
    return colour


def _finish(mat, colour, spec):
    """Light painted in: worn hard edges, dark hollows, light from above."""
    dark, _, light = spec["tones"]
    worn = tuple(min(1.0, c * 1.3) for c in light)
    colour = layers.worn_edges(mat, colour, worn, spec.get("edges", 0.0), 1.5 / spec["density"])
    colour = layers.hollows(mat, colour, tuple(c * 0.35 for c in dark), spec.get("hollows", 0.0),
                            6.0 / spec["density"])
    return layers.from_above(mat, colour, spec.get("top", 0.0))


def _recipe(family):
    def recipe(name=None, **overrides):
        return make(family, name, **overrides)
    recipe.__name__ = specs.FAMILIES[family]["fn"]
    recipe.__doc__ = f"The game's {family} paint (codex paint.md); overrides as in the module doc."
    return recipe


for _family, _choices in specs.FAMILIES.items():
    globals()[_choices["fn"]] = _recipe(_family)

FAMILY_OF = {choices["fn"]: family for family, choices in specs.FAMILIES.items()}
