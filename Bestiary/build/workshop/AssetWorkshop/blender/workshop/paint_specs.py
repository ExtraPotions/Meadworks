"""What each paint recipe is made of: the measured numbers from the codex (codex/data/paint.json and palette.json:
tones, texel density, blotch size, per-texel noise, normal strength) and, per family, the choices those numbers cannot
make (grain direction, pattern, how much stain, edge wear, hollows, light from above), set by looking at the game's
textures. `spec(family, overrides)` merges the two with a caller's overrides."""
import functools
import json
import math
import os

CODEX = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "codex", "data"))
BAND_PX = {"4-8": 6.0, "8-16": 12.0, "16-32": 24.0, "32-64": 48.0, "64+": 96.0}
BLOTCH_CAP = 24.0       # texels: bigger blotches than this leave an item-sized part one flat colour

W, M, B, L, C, S, G, P, K = "wood", "metal", "bone", "leather", "cloth", "stone", "gem", "primary", "skin"
FAMILIES = {
    "wood.planks": dict(fn="wood_planks", region=W, rough=0.85, axis="X", along=0.3, grain=(0.08, 0.05, 0.06),
                        pattern=("seams", 0.2), edges=0.3, hollows=0.7),
    "wood.dark": dict(fn="wood_dark", region=W, rough=0.85, axis="X", along=0.3, grain=(0.04, 0.05, 0.06),
                      tones_from=["DarkWoodBeams_d.png", "DarkWoodGate_d.png", "DarkWoodChair_d.png"],
                      edges=0.25, hollows=0.4, jitter_scale=1.0, blotch_scale=2.5, contrast=1.5),
    "wood.logs": dict(fn="wood_logs", region=W, rough=0.85, axis="Z", along=0.25, grain=(0.12, 0.04, 0.05),
                      edges=0.3, hollows=0.6, jitter_scale=1.8),
    "wood.bark": dict(fn="wood_bark", region=W, rough=0.95, axis="Z", along=0.35, pattern=("fissures", 0.25),
                      hollows=0.3, jitter_scale=3.0, tones_from=["beech_bark.png", "oak_bark.png", "PineTree_log_d.png",
                                                                  "olivetree_trunk01.png"]),
    "wood.fine": dict(fn="wood_fine", region=W, rough=0.7, axis="X", along=0.35, grain=(0.04, 0.06, 0.08),
                      edges=0.3, hollows=0.4, jitter_scale=2.0),
    "metal.iron": dict(fn="iron", region=M, rough=0.45, metal=0.8, grain=(0.06, 0.03, 0.2), edges=0.55, hollows=0.8,
                       jitter_scale=1.5, neutral=True),
    "metal.bronze": dict(fn="bronze", region=M, rough=0.45, metal=0.8, edges=0.5, hollows=0.8, jitter_scale=1.3),
    "metal.copper": dict(fn="copper", region=M, rough=0.45, metal=0.8, edges=0.5, hollows=0.8, blotch_scale=0.3,
                         jitter_scale=3.5, tones_from=["anvil.png"]),
    "metal.tin": dict(fn="tin", region=M, rough=0.4, metal=0.8, edges=0.5, hollows=0.6, blotch_scale=0.5,
                      jitter_scale=1.4, neutral=True),
    "metal.silver": dict(fn="silver", region=M, rough=0.35, metal=0.8, edges=0.6, hollows=0.6, jitter_scale=1.2,
                         neutral=True,
                         tones_from=["Silver_shield_d.png", "SilverArmourChest_d.png", "silver_ore_d.png"]),
    "metal.blackmetal": dict(fn="blackmetal", region=M, rough=0.4, metal=0.8, edges=0.6, hollows=0.5,
                             jitter_scale=0.6, neutral=True),
    "metal.gold": dict(fn="gold", region=M, rough=0.35, metal=0.8, edges=0.6, hollows=0.8, jitter_scale=1.2,
                       tones_from=["Valheim_Crown_d.png", "coin_pile_decal.png"]),
    "metal.flametal": dict(fn="flametal", region=M, rough=0.4, metal=0.8, edges=0.4, hollows=0.7, jitter_scale=0.6,
                           patches=[("ember", 0.06, 0.08), ("teal", 0.06, 0.1)]),
    "bone.bone": dict(fn="bone", region=B, rough=0.7, axis="Z", along=0.5, edges=0.35, hollows=0.2, jitter_scale=3.0,
                      tones_from=["Skeleton_d.tga", "SpineSnap_d.png", "Boneshield_d.png", "bonemass_bonebone_d.png"],
                      patches=[("stain", 0.08, 0.06)]),
    "bone.antler": dict(fn="antler", region=B, rough=0.7, axis="Z", along=0.4, grain=(0.06, 0.04, 0.1), edges=0.35,
                        hollows=0.3, jitter_scale=1.5, blotch_scale=0.2, contrast=1.3),
    "bone.horn": dict(fn="horn", region=B, rough=0.5, axis="Z", pattern=("bands", 0.2, 0.3), edges=0.3,
                      hollows=0.5, jitter_scale=2.5),
    "bone.teeth": dict(fn="teeth", region=B, rough=0.5, axis="Z", along=0.6, edges=0.3, hollows=0.3, jitter=0.0,
                       blotch_scale=0.6, smooth=True),
    "leather.leather": dict(fn="leather", region=L, rough=0.75, edges=0.4, hollows=0.8, jitter_scale=0.8),
    "leather.hide": dict(fn="hide", region=L, rough=0.85, edges=0.2, hollows=0.6, jitter_scale=1.2,
                         tones_from=["hide01.png", "hide02.png", "bearhide_d.png", "Tanningrack_d.png"]),
    "leather.fur": dict(fn="fur", region="fur", rough=0.95, axis="Z", pattern=("strands", 0.012, 0.12), hollows=0.5,
                        top=0.12, jitter_scale=0.2, blotch_scale=0.7),
    "cloth.linen": dict(fn="linen", region=C, rough=0.9, pattern=("weave", 0.006), hollows=0.6, jitter_scale=2.0,
                        blotch_scale=3.0, contrast=1.4,
                        tones_from=["sail_white.tga", "TraderTent_d.png", "NordCape_d.png"]),
    "cloth.rope": dict(fn="rope", region="rope", rough=0.9, axis="Z", pattern=("twist", 0.03), hollows=0.5),
    "stone.stone": dict(fn="stone", region=S, rough=0.9, pattern=("cracks", 0.6), edges=0.35, hollows=0.8,
                        patches=[("moss", 0.0, 0.3)]),
    "stone.marble": dict(fn="marble", region=S, rough=0.3, pattern=("veins", 0.5), edges=0.3, hollows=0.6,
                         jitter_scale=0.7, neutral=True),
    "stone.grausten": dict(fn="grausten", region=S, rough=0.9, pattern=("hatch", 0.012, 0.16), edges=0.3,
                           hollows=0.8, jitter_scale=1.8, blotch_scale=0.5),
    "thatch.straw": dict(fn="thatch", region=P, rough=0.95, axis="Z", pattern=("strands", 0.004, 0.5),
                         hollows=0.5, jitter_scale=3.0),
    "crystal.crystal": dict(fn="crystal", region=G, rough=0.1, edges=0.15, hollows=0.2, jitter=0.0,
                            smooth=True, blotch_scale=2.0, contrast=1.4, tones_from=["battleaxe_crystal_d.png"]),
    "crystal.ice": dict(fn="ice", region=G, rough=0.15, pattern=("cracks", 0.5), edges=0.5, hollows=0.3),
    "crystal.obsidian": dict(fn="obsidian", region=S, rough=0.15, grain=(0.12, 0.08, 0.3), edges=0.6, hollows=0.5,
                             blotch_scale=0.25, jitter_scale=1.5, neutral=True),
    "chitin.chitin": dict(fn="chitin", region=P, rough=0.4, edges=0.5, hollows=0.6, jitter_scale=1.8, blotch_scale=0.5,
                          tones_from=["seeker_d.png", "seekerBrute_d.png", "carapacearmor_d.png", "Carapace_d.png"]),
    "skin.skin": dict(fn="skin", region=K, rough=0.8, hollows=0.3, top=0.16, jitter_scale=2.2, blotch_scale=0.6,
                      tones_from=["Draugr_d.png", "greydrawrf_diffuse.png", "goblin_d.png", "neck_d.png",
                                  "Rotvalta_d.png"]),
    "skin.flesh": dict(fn="flesh", region=K, rough=0.5, pattern=("marbling", 0.06), hollows=0.6, jitter_scale=1.6,
                       blotch_scale=0.5),
    "veg.mushroom": dict(fn="mushroom", region=P, rough=0.7, edges=0.2, hollows=0.6, jitter_scale=0.1,
                         blotch_scale=0.25, tones_from=["Boletus_edulis_d.png"]),
    "veg.leaves": dict(fn="leaves", region=P, rough=0.8, pattern=("veins", 0.3), hollows=0.6, jitter_scale=0.3),
    "veg.moss": dict(fn="moss", region=P, rough=0.95, hollows=0.5, blotch_scale=0.3, jitter_scale=1.6),
}
COLOURS = {"rust": "#6b3a1e", "verdigris": "#4f7a62", "ember": "#b8663a", "teal": "#3f8a8c", "stain": "#6a4a2c",
           "grime": "#2e2218", "moss": "#4d5a2a"}
PRESETS = {    # tones taken from one game texture: preset="troll" etc. (dye= is the same for cloth)
    "cloth.linen": {"red": "jutecarpet_d.png", "blue": "jutecarpet_blue_d.png", "undyed": "sail_white.tga",
                    "brown": "ClothesCasual4_d.png", "tent": "TraderTent_d.png", "ship": "ShipTent_d.png"},
    "skin.skin": {"troll": "troll_diffuse.png", "draugr": "Draugr_d.png", "greydwarf": "greydrawrf_diffuse.png",
                  "goblin": "GoblinBrute_d.png", "human": "PlayerCharacter_01.png", "jotun": "Jotnar_d.png",
                  "neck": "neck_d.png", "frost": "frosttroll_d.png", "fenring": "Fenring_d.png"},
    "crystal.crystal": {"lilac": "battleaxe_crystal_d.png", "red": "Proustite_d.png", "gem": "Gemstones_d.png"},
    "veg.mushroom": {"boletus": "Boletus_edulis_d.png", "toadstool": "bzerkermushroom_d.png",
                     "mistlands": "MistlandsShrooms_d.png"},
    "leather.hide": {"deer": "hide02.png", "bear": "bearhide_d.png", "troll": "troll_hide.png",
                     "seal": "Sealrug_d.png", "moose": "moose_rug_d.png"},
    "leather.fur": {"wolf": "rug_wolf.png", "lox": "LoxCape_D.png", "bear": "BjornRug_d.png"},
    "metal.gold": {"crown": "Valheim_Crown_d.png", "coins": "coin_pile_decal.png"},
}


@functools.lru_cache(maxsize=None)
def _data(name):
    try:
        with open(os.path.join(CODEX, name + ".json"), encoding="utf-8") as handle:
            return json.load(handle)
    except (OSError, ValueError):
        return {}


def spec(family, overrides=None):
    """The full recipe of one family: measured numbers, the family's choices, then the caller's overrides."""
    found = dict(_measured(family))
    found.update(FAMILIES[family])
    found["family"] = family
    found["tones"] = _tones(family, found)
    for key, value in (overrides or {}).items():
        found[key] = value
    preset = found.pop("preset", None) or found.pop("dye", None)
    if preset:
        found["tones"] = _sample_tones(family, [PRESETS[family][preset]]) or found["tones"]
    _adjust_tones(found)
    found.setdefault("blotch_m", found["blotch_px"] * found.get("blotch_scale", 1.0) / found["density"])
    return found


def _adjust_tones(found):
    """Light from above (tones raised to keep the mean), greys for neutral metals, then a caller's tint."""
    if found.get("top"):
        lift = 1.0 / (1.0 - found["top"] / 2.0)
        found["tones"] = tuple(tuple(min(1.0, c * lift) for c in tone) for tone in found["tones"])
    if found.get("neutral"):
        found["tones"] = neutral(found["tones"])
    if "tint" in found:
        found["tones"] = tinted(found["tones"], found.pop("tint"))


def _measured(family):
    """Density, blotch size (texels: the game paints blotches to the texture, so a recipe turns them into metres at
    the asset's own density), jitter and relief strength from paint.json (fallbacks when it is missing)."""
    stats = _data("paint").get("families", {}).get(family, {}).get("stats", {})
    density = _median(stats, "texel_density", 45.0)
    bands = stats.get("bands", {})
    return {"density": density, "blotch_px": _blotch_px(bands), "jitter": _jitter(stats, bands),
            "relief": max(0.0, min(_median(stats, "normal_strength", 3.0), 6.0))}


def _median(stats, key, fallback):
    value = (stats.get(key) or {}).get("median")
    return fallback if value is None else value


def _blotch_px(bands):
    """The coarse blotch size in texels: the variance-weighted geometric mean of the bands from 4 px up."""
    weights = {k: bands.get(k, 0.0) for k in BAND_PX}
    total = sum(weights.values())
    if total <= 0:
        return 16.0
    return min(BLOTCH_CAP, math.exp(sum(w * math.log(BAND_PX[k]) for k, w in weights.items()) / total))


def _jitter(stats, bands):
    """Per-texel multiplier amplitude giving the finest band its share of the value variance."""
    value = stats.get("value_p50", {}).get("median") or 0.45
    span = stats.get("value_span", {}).get("median") or 0.2
    fine = bands.get("fine", 0.15)
    return round(min(0.35, math.sqrt(3.0 * fine) * (span / 2.56) / value), 3)


def _tones(family, found):
    """Dark, mid and light linear colours: the family's own choice, its chosen samples, or the whole family."""
    if "tones" in found and isinstance(found["tones"][0], str):
        return tuple(hex_to_linear(h) for h in found["tones"])
    chosen = _sample_tones(family, found.get("tones_from"))
    if chosen:
        return chosen
    tones = _data("palette").get("families", {}).get(family, {}).get("tones")
    if tones:
        return tuple(tuple(tones[p]["linear"]) for p in ("p10", "p50", "p90"))
    return ((0.1, 0.1, 0.1), (0.2, 0.2, 0.2), (0.35, 0.35, 0.35))


def _sample_tones(family, names):
    """Per-channel median (linear) of the named samples' 10/50/90 % tones."""
    sources = _data("palette").get("families", {}).get(family, {}).get("sources", [])
    picked = [s["tones"] for s in sources if names and s["texture"] in names]
    if not picked:
        return None
    return tuple(tuple(_median_of([hex_to_linear(t[i])[c] for t in picked]) for c in range(3)) for i in range(3))


def _median_of(values):
    ordered = sorted(values)
    middle = len(ordered) // 2
    return ordered[middle] if len(ordered) % 2 else (ordered[middle - 1] + ordered[middle]) / 2


def hex_to_linear(text):
    """'#rrggbb' (sRGB) -> linear (r, g, b)."""
    values = [int(text[i:i + 2], 16) / 255.0 for i in (1, 3, 5)]
    return tuple(v / 12.92 if v <= 0.04045 else ((v + 0.055) / 1.055) ** 2.4 for v in values)


def tinted(tones, tint):
    """The tones re-coloured to a tint (linear rgb or '#hex') keeping each tone's luminance: a dye or recolour that
    keeps the painted light and grime. A tone the tint's hue cannot reach at its luminance (a light tone of a deep
    blue) is moved towards grey until it fits, so the tones never clip into one flat colour."""
    colour = hex_to_linear(tint) if isinstance(tint, str) else tuple(tint)
    base = max(_luminance(colour), 1e-4)
    return tuple(_fit(tuple(c * _luminance(t) / base for c in colour), _luminance(t)) for t in tones)


def neutral(tones):
    """The tones as greys of the same luminance (metals the game paints grey and colours by light only)."""
    return tuple((_luminance(t),) * 3 for t in tones)


def _fit(rgb, luminance):
    """rgb pulled towards the grey of `luminance` just enough that no channel passes 1."""
    top = max(rgb)
    if top <= 1.0:
        return rgb
    keep = (1.0 - luminance) / max(top - luminance, 1e-6)
    return tuple(luminance + (c - luminance) * keep for c in rgb)


def _luminance(rgb):
    return 0.2126 * rgb[0] + 0.7152 * rgb[1] + 0.0722 * rgb[2]
