"""Paint regions: the parts of an asset a mod may recolour, marked on its materials and baked into an ID mask.

A material names its region in a custom property, most simply through mark():

    planks = regions.mark(materials.wood("planks"), "primary")

When any material of an asset has one, the pipeline also bakes <name>_regions.png: every texel the flat colour of its
region (COLOURS), at the same UVs and margin as the albedo, snapped so no texel blends two regions; texels no island
reaches stay black. Materials without a region fall into "base". The manifest then lists each region's name, mask
colour, share of the baked texels (UV islands and their margins) and median baked albedo colour (codex/tools/recolour.py
works from these).
"""
import bpy
import numpy as np

from . import bake, materials

PROPERTY = "region"
FAMILY = "paint_family"       # the codex paint family a material is painted as (codex/data/paint.json: "metal.iron")
BASE = "base"
EMPTY = "#000000"
KEY = "workshop_regions"      # the mask image keeps {region: colour} here for table()
FAMILIES = "workshop_region_families"   # and {region: "family,family"}
COLOURS = {
    "base": "#808080", "primary": "#e6194b", "secondary": "#3cb44b", "trim": "#4363d8", "metal": "#42d4f4",
    "leather": "#f58231", "cloth": "#911eb4", "skin": "#ffe119", "glow": "#f032e6", "bone": "#fabed4",
    "wood": "#9a6324", "stone": "#a9a9a9", "fur": "#469990", "rope": "#aaffc3", "gem": "#dcbeff",
}
SPARE = ("#800000", "#808000", "#000075", "#bfef45", "#fffac8", "#e6beff", "#ffd8b1", "#008080")


def mark(material, region, family=None):
    """Puts the material in a paint region (a short name: primary, secondary, trim, metal, leather ...) and, when
    given, names the codex paint family it is painted as (the style check compares the region with it); returns it."""
    material[PROPERTY] = region
    if family:
        material[FAMILY] = family
    return material


def region_of(material):
    return str(material.get(PROPERTY, BASE)) if material is not None else BASE


def present(obj):
    """True when any material on the object names a region."""
    return any(PROPERTY in m for m in _materials(obj))


def palette(names):
    """{region: '#rrggbb'} for these region names: the fixed colour of a known name, else the next spare one."""
    spare = [c for c in SPARE if c not in COLOURS.values()]
    colours = {}
    for name in sorted(set(names)):
        if name in COLOURS:
            colours[name] = COLOURS[name]
        else:
            colours[name] = spare.pop(0) if spare else _grey(len(colours))
    return colours


def bake_mask(obj, name, size):
    """Bakes the object's regions into a new Non-Color image `name` (flat colours, black where nothing is baked)."""
    colours = palette(region_of(m) for m in _materials(obj))
    image = bake.new_image(name, size, data=True)
    restores = [_emit_flat(m, _rgb(colours[region_of(m)])) for m in _materials(obj)]
    bake._bake(obj, image, 1, type='EMIT')      # the albedo's own bake call: same UVs, same margin
    for restore in restores:
        restore()
    _snap(image, [EMPTY] + list(colours.values()))
    image[KEY] = colours
    image[FAMILIES] = {region: _families(obj, region) for region in colours}
    return image


def table(mask, albedo):
    """[{name, colour, share, albedo, families}] for a baked mask: each region's share of the baked texels (islands
    and margins), the median colour of the baked albedo under it (sRGB hex) and the paint families its materials
    name, largest region first."""
    colours, families = mask[KEY].to_dict(), mask[FAMILIES].to_dict()
    ids, paint = _bytes(mask), _bytes(albedo)
    covered = np.any(ids != 0, axis=1)
    total = max(int(covered.sum()), 1)
    rows = []
    for name, colour in colours.items():
        hit = np.all(ids == np.array(_bytes_of(colour)), axis=1)
        median = np.median(paint[hit], axis=0) if hit.any() else None
        rows.append({"name": name, "colour": colour, "share": round(float(hit.sum()) / total, 4),
                     "albedo": _hex(median) if median is not None else "",
                     "families": [f for f in families.get(name, "").split(",") if f]})
    return sorted(rows, key=lambda row: (-row["share"], row["name"]))


def _families(obj, region):
    """The paint families the region's materials name, comma separated ('' for none)."""
    named = {str(m.get(FAMILY) or m.get("family") or "") for m in _materials(obj) if region_of(m) == region}
    return ",".join(sorted(named - {""}))


def _materials(obj):
    return list({slot.material for slot in obj.material_slots if slot.material})


def _emit_flat(mat, rgb):
    """Routes a flat emission colour into the material's output; returns the function that undoes it."""
    tree, out = mat.node_tree, materials.output(mat)
    previous = out.inputs['Surface'].links[0].from_socket if out.inputs['Surface'].links else None
    emit = tree.nodes.new('ShaderNodeEmission')
    emit.inputs['Color'].default_value = (*rgb, 1.0)
    tree.links.new(emit.outputs['Emission'], out.inputs['Surface'])

    def restore():
        tree.nodes.remove(emit)
        if previous is not None:
            tree.links.new(previous, out.inputs['Surface'])
    return restore


def _snap(image, colours):
    """Sets every pixel to the nearest of the colours, so the mask holds nothing but flat region colours."""
    pixels = np.empty(len(image.pixels), np.float32)
    image.pixels.foreach_get(pixels)
    rgba = pixels.reshape(-1, 4)
    targets = np.array([_rgb(c) for c in colours], np.float32)
    best, nearest = np.full(len(rgba), np.inf, np.float32), np.zeros(len(rgba), np.int32)
    for i, target in enumerate(targets):
        distance = ((rgba[:, :3] - target) ** 2).sum(axis=1)
        closer = distance < best
        best[closer], nearest[closer] = distance[closer], i
    rgba[:, :3], rgba[:, 3] = targets[nearest], 1.0
    image.pixels.foreach_set(rgba.ravel())
    image.update()


def _bytes(image):
    """The image's RGB as (n, 3) bytes, as they are written to the PNG."""
    pixels = np.empty(len(image.pixels), np.float32)
    image.pixels.foreach_get(pixels)
    return np.round(pixels.reshape(-1, 4)[:, :3] * 255.0).astype(np.int32)


def _rgb(colour):
    return tuple(v / 255.0 for v in _bytes_of(colour))


def _bytes_of(colour):
    return tuple(int(colour[i:i + 2], 16) for i in (1, 3, 5))


def _hex(rgb):
    return "#" + "".join(f"{int(round(float(v))):02x}" for v in rgb)


def _grey(index):
    level = 40 + (index * 37) % 180
    return f"#{level:02x}{level:02x}{level:02x}"
