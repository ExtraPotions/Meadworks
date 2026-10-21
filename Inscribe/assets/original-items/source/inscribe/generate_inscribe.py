# Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
import hashlib
import json
import math
from pathlib import Path


ROOT = Path(__file__).resolve().parent
MODELS = ROOT / "models"


def material(color, roughness=0.8, metallic=0.0, emission=0.0):
    rgb = [int(color[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    return {"color": rgb + [1.0], "roughness": roughness,
            "metallic": metallic, "emission": rgb, "emissionStrength": emission}


PALETTE = {
    "stone": material("656059"), "stone_light": material("817B6D"),
    "stone_dark": material("48453F"), "cut": material("242723"),
    "ivory": material("C5BD9E"), "ivory_light": material("DDD1AE"),
    "ivory_dark": material("AFA78C"), "moss": material("535D32"),
    "obsidian": material("302C2B", 0.5), "obsidian_light": material("4A4140", 0.5),
    "obsidian_dark": material("211F20", 0.6),
    "steel": material("8D9191", 0.48, 0.55), "steel_edge": material("BBC0BB", 0.4, 0.65),
    "brass": material("A9813D", 0.58, 0.5), "brass_dark": material("74572E", 0.7, 0.45),
    "leather": material("493225"), "leather_light": material("72533C"),
    "parchment": material("C7AA77"), "parchment_light": material("DFC393"),
    "parchment_dark": material("A78A5C"), "rope": material("84704B"),
    "eye_blue": material("6CB3CA", 0.72, emission=0.28),
    "rune_violet": material("9B7BC3", 0.73, emission=0.26),
    "rune_gold": material("D3AA55", 0.74, emission=0.25),
    "rune_pale": material("68502D", 0.8),
    "rune_red": material("AF5140", 0.8, emission=0.20),
    "seal_1": material("4F7946"), "seal_2": material("387985"),
    "seal_3": material("795092"), "seal_4": material("C1903C", 0.65, 0.25),
}


class Model:
    def __init__(self, ident, title, prefabs, note):
        self.data = {"schemaVersion": 1, "id": ident, "product": "Inscribe",
                     "displayName": title, "prefabNames": prefabs, "units": "meters",
                     "coordinateSystem": "Unity Y-up; front -Z",
                     "designNote": note, "materials": dict(PALETTE), "parts": [],
                     "icon": {"cameraPosition": [0.33, 0.22, -0.8],
                              "cameraTarget": [0, 0, 0], "orthographicSize": 0.12}}

    def mesh(self, name, mat, vertices, triangles):
        self.data["parts"].append({"name": name, "material": mat,
                                  "vertices": [[round(c, 7) for c in v] for v in vertices],
                                  "triangles": [i for triangle in triangles for i in triangle]})

    def save(self):
        used = {p["material"] for p in self.data["parts"]}
        self.data["materials"] = {k: v for k, v in self.data["materials"].items() if k in used}
        points = [v for p in self.data["parts"] for v in p["vertices"]]
        self.data["bounds"] = {"min": [min(p[a] for p in points) for a in range(3)],
                               "max": [max(p[a] for p in points) for a in range(3)]}
        extent = max(self.data["bounds"]["max"][a] - self.data["bounds"]["min"][a]
                     for a in range(3))
        self.data["icon"]["orthographicSize"] = round(extent * 0.7, 5)
        MODELS.mkdir(parents=True, exist_ok=True)
        dest = MODELS / (self.data["id"] + ".json")
        dest.write_text(json.dumps(self.data, separators=(",", ":")) + "\n", encoding="utf-8")
        return {"id": self.data["id"], "displayName": self.data["displayName"],
                "prefabNames": self.data["prefabNames"], "file": "models/" + dest.name,
                "triangleCount": sum(len(p["triangles"]) // 3 for p in self.data["parts"]),
                "sha256": hashlib.sha256(dest.read_bytes()).hexdigest().upper()}


def extrude(m, name, mat, xy, zfront, zback):
    # Glyphs and concave cuts use ear clipping so the open hoof stays open.
    area = sum(xy[i][0] * xy[(i + 1) % len(xy)][1] - xy[(i + 1) % len(xy)][0] * xy[i][1]
               for i in range(len(xy)))
    xy = list(xy if area > 0 else reversed(xy))
    n = len(xy)
    verts = [(x, y, zfront) for x, y in xy] + [(x, y, zback) for x, y in xy]
    faces = []
    remaining = list(range(n))

    def cross(a, b, c):
        return (b[0] - a[0]) * (c[1] - a[1]) - (b[1] - a[1]) * (c[0] - a[0])

    while len(remaining) > 3:
        for k, b in enumerate(remaining):
            a, c = remaining[k - 1], remaining[(k + 1) % len(remaining)]
            if cross(xy[a], xy[b], xy[c]) <= 1e-12:
                continue
            blocked = False
            for p in remaining:
                if p in (a, b, c):
                    continue
                if all(cross(xy[u], xy[v], xy[p]) >= -1e-12 for u, v in ((a, b), (b, c), (c, a))):
                    blocked = True
                    break
            if not blocked:
                faces.extend([(c, b, a), (a + n, b + n, c + n)])
                remaining.pop(k)
                break
        else:
            raise ValueError(f"Cannot triangulate {name}")
    a, b, c = remaining
    faces.extend([(c, b, a), (a + n, b + n, c + n)])
    for a in range(n):
        b = (a + 1) % n
        faces.extend([(a, b, b + n), (a, b + n, a + n)])
    m.mesh(name, mat, verts, faces)


def bar(m, name, mat, a, b, width, z=-0.026, depth=0.001):
    dx, dy = b[0] - a[0], b[1] - a[1]
    length = math.hypot(dx, dy)
    if length < 1e-8:
        return
    nx, ny = -dy * width / length / 2, dx * width / length / 2
    extrude(m, name, mat, [(a[0] + nx, a[1] + ny), (b[0] + nx, b[1] + ny),
                           (b[0] - nx, b[1] - ny), (a[0] - nx, a[1] - ny)], z, z + depth)


def glyph(m, name, mat, points, width=0.0028, z=-0.02125):
    for i in range(len(points) - 1):
        bar(m, f"{name}_cut_{i}", "cut", points[i], points[i + 1], width * 1.75, z + 0.00035)
        bar(m, f"{name}_inlay_{i}", mat, points[i], points[i + 1], width, z)


def arc(cx, cy, rx, ry, start, end, steps=16):
    return [(cx + rx * math.cos(start + (end - start) * i / steps),
             cy + ry * math.sin(start + (end - start) * i / steps)) for i in range(steps + 1)]


def rock(m, name, xy, mat="stone", depth=0.041, zcenter=0.0, inset=0.84):
    n = len(xy)
    front, back = zcenter - depth / 2, zcenter + depth / 2
    ring = [(x, y, zcenter - depth * 0.12) for x, y in xy]
    bevel = [(x * inset, y * inset, front) for x, y in xy]
    vertices = ring + bevel + [(x * 0.9, y * 0.9, back) for x, y in xy] + [(0, 0, front)]
    for i in range(n):
        j = (i + 1) % n
        shade = mat + ("_light" if i % 5 == 0 else "_dark" if i % 4 == 0 else "")
        if shade not in m.data["materials"]:
            shade = mat
        faces = [(i, j + n, j), (i, i + n, j + n),
                 (i + n, 3 * n, j + n), (i, j, j + 2 * n),
                 (i, j + 2 * n, i + 2 * n)]
        m.mesh(f"{name}_facet_{i}", shade, vertices, faces)
    m.mesh(name + "_back", mat, vertices, [(2 * n, 2 * n + i, 2 * n + i + 1) for i in range(1, n - 1)])


def cylinder_x(m, name, mat, x0, x1, radius, center=(0, 0), segments=12, inner=0.0):
    cy, cz = center
    verts = [(x, cy + radius * math.sin(2 * math.pi * i / segments),
              cz + radius * math.cos(2 * math.pi * i / segments))
             for x in (x0, x1) for i in range(segments)]
    faces = []
    for i in range(segments):
        j = (i + 1) % segments
        faces.extend([(i, j + segments, j), (i, i + segments, j + segments)])
    if inner > 0:
        verts += [(x, cy + inner * math.sin(2 * math.pi * i / segments),
                   cz + inner * math.cos(2 * math.pi * i / segments))
                  for x in (x0, x1) for i in range(segments)]
        for i in range(segments):
            j = (i + 1) % segments
            faces.extend([(i, 2 * segments + i, 2 * segments + j), (i, 2 * segments + j, j),
                          (segments + i, segments + j, 3 * segments + j),
                          (segments + i, 3 * segments + j, 3 * segments + i),
                          (2 * segments + i, 3 * segments + j, 3 * segments + i),
                          (2 * segments + i, 2 * segments + j, 3 * segments + j)])
    else:
        faces += [(0, j, j + 1) for j in range(1, segments - 1)]
        faces += [(segments, segments + j + 1, segments + j) for j in range(1, segments - 1)]
    m.mesh(name, mat, verts, faces)


def rune(ident, title, shape, mat, base="stone"):
    m = Model(ident, title, ["ECF_" + ident.title()], "Approved chipped tablet silhouette and engraved glyph; restrained inlay emission.")
    rock(m, "chipped_tablet", shape, base)
    if ident == "awakening":
        glyph(m, "eye_upper", mat, [(-.035, 0), (-.021, .014), (0, .02), (.021, .014), (.035, 0)])
        glyph(m, "eye_lower", mat, [(-.035, 0), (-.021, -.014), (0, -.02), (.021, -.014), (.035, 0)])
        extrude(m, "pupil", mat, arc(0, 0, .007, .008, 0, 2 * math.pi, 10)[:-1], -.0214, -.0204)
        for i in range(10):
            a = 2 * math.pi * i / 10
            if abs(math.sin(a)) < .3:
                continue
            glyph(m, "eye_ray" + str(i), mat, [(math.cos(a) * .028, math.sin(a) * .028),
                                                      (math.cos(a) * .041, math.sin(a) * .042)], .002)
    elif ident == "recasting":
        for j, start in enumerate([.35, math.pi + .35]):
            points = arc(0, 0, .029, .034, start, start + 2.25, 14)
            glyph(m, "turn" + str(j), mat, points, .0032)
            x, y = points[-1]
            glyph(m, "arrow" + str(j), mat, [(x - .008, y + .008), (x, y), (x + .007, y + .008)], .0032)
    elif ident == "ascension":
        glyph(m, "ascent_spine", mat, [(0, -.043), (0, .046)], .0032)
        for i, cy in enumerate([-.032, -.008, .020]):
            w = .021 - i * .004
            glyph(m, "ascent_step" + str(i), mat, [(-w, cy - .009), (-w, cy + .002), (0, cy + .014),
                                                         (w, cy + .002), (w, cy - .009)], .0032)
        glyph(m, "ascent_tip", mat, [(-.009, .033), (0, .051), (.009, .033)], .0032)
    elif ident == "cleansing":
        glyph(m, "cleansing_split", mat, [(0, -.054), (0, -.011)], .004)
        glyph(m, "cleansing_split_top", mat, [(0, .011), (0, .054)], .004)
        glyph(m, "cleansing_ring", mat, arc(0, 0, .012, .012, 0, 2 * math.pi, 12), .003)
        for i in range(4):
            a = math.pi * .25 + i * math.pi * .5
            glyph(m, "cleansing_channel" + str(i), "cut", [(math.cos(a) * .016, math.sin(a) * .016),
                                                          (math.cos(a) * .035, math.sin(a) * .038)], .0018)
    else:
        spiral = []
        for i in range(39):
            a = .9 + i * 3.8 * math.pi / 38
            r = .004 + i * .00084
            spiral.append((r * math.cos(a), r * math.sin(a)))
        glyph(m, "coiled_serpent", mat, spiral, .0032)
        glyph(m, "serpent_neck", mat, [spiral[-1], (.018, .038), (.006, .044), (.022, .047), (.031, .042)], .004)
        extrude(m, "serpent_head", mat, [(.013, .039), (.029, .039), (.035, .044), (.025, .048), (.012, .047)], -.0214, -.0204)
        extrude(m, "serpent_eye", "cut", [(.024, .043), (.027, .043), (.027, .045), (.024, .045)], -.0217, -.0214)
    return m


def runes():
    shape = [(-.037, -.054), (.005, -.063), (.038, -.043), (.049, -.012), (.04, .029),
             (.019, .061), (-.014, .055), (-.03, .034), (-.05, -.012)]
    yield rune("awakening", "Awakening Rune", shape, "eye_blue")
    yield rune("recasting", "Recasting Rune", [(x * .92, y * 1.03) for x, y in shape], "rune_violet")
    yield rune("ascension", "Ascension Rune", [(-.034, -.064), (.028, -.064), (.037, -.043),
              (.032, .044), (.01, .071), (-.019, .06), (-.039, .025)], "rune_gold")
    yield rune("cleansing", "Cleansing Rune", [(-.037, -.06), (.032, -.06), (.039, -.041),
              (.037, .043), (.013, .06), (-.021, .055), (-.04, .036)], "rune_pale", "ivory")
    yield rune("serpent", "Serpent Rune", [(x * 1.04, y * 1.02) for x, y in shape], "rune_red", "obsidian")


def chisel():
    m = Model("dvergr_chisel", "Dvergr Chisel", ["ECF_DvergrChisel"], "Square striking cap, brass collar, bound grip, broad worn steel edge.")
    extrude(m, "forged_shank", "steel", [(-.014, -.07), (.014, -.07), (.012, .105), (-.012, .105)], -.012, .012)
    extrude(m, "flared_blade", "steel", [(-.025, -.115), (.025, -.115), (.015, -.055), (-.014, -.055)], -.013, .013)
    extrude(m, "honed_bevel", "steel_edge", [(-.025, -.115), (.025, -.115), (.023, -.104), (-.024, -.104)], -.014, -.0125)
    extrude(m, "wrapped_grip", "leather", [(-.017, -.041), (.017, -.041), (.016, .074), (-.016, .074)], -.017, .017)
    for i in range(9):
        bar(m, "leather_binding_" + str(i), "leather_light", (-.016, -.039 + i * .012), (.016, -.028 + i * .012), .0035, -.018)
    extrude(m, "brass_collar", "brass", [(-.02, .071), (.02, .071), (.02, .085), (-.02, .085)], -.02, .02)
    glyph(m, "collar_carving", "brass_dark", [(-.012, .074), (-.005, .082), (.003, .074), (.011, .082)], .0015, -.021)
    extrude(m, "square_striking_cap", "steel", [(-.018, .084), (.018, .084), (.02, .101), (.014, .112),
             (-.015, .112), (-.021, .102)], -.02, .02)
    bar(m, "cap_edge", "steel_edge", (-.016, .109), (.013, .109), .002, -.0208)
    return m


def scroll(tier):
    m = Model("scroll_tier_" + str(tier), "Enhancement Scroll Tier " + str(tier),
              ["ScrollEnhance_Tier" + str(tier)], "Rolled parchment, exposed spiral ends, bound seal carrying tier-count incisions.")
    cylinder_x(m, "rolled_parchment", "parchment", -.084, .084, .033, inner=.023)
    cylinder_x(m, "inner_parchment", "parchment_dark", -.081, .081, .021, inner=.014)
    cylinder_x(m, "center_parchment", "parchment", -.079, .079, .012, inner=.006)
    for x in [-.085, .085]:
        for i in range(30):
            a = i * math.pi * 3.7 / 30
            b = (i + 1) * math.pi * 3.7 / 30
            r0, r1 = .007 + i * .00077, .007 + (i + 1) * .00077
            verts = [(x, r0 * math.sin(a), r0 * math.cos(a)), (x, r1 * math.sin(b), r1 * math.cos(b)),
                     (x, (r1 + .0017) * math.sin(b), (r1 + .0017) * math.cos(b)),
                     (x, (r0 + .0017) * math.sin(a), (r0 + .0017) * math.cos(a))]
            m.mesh("spiral_end_" + str(x) + "_" + str(i), "parchment_light", verts,
                   [(0, 1, 2), (0, 2, 3), (2, 1, 0), (3, 2, 0)])
    if tier == 1:
        for i in range(3):
            cylinder_x(m, "twine_wrap_" + str(i), "rope", -.014 + i * .006, -.010 + i * .006, .0355, inner=.033)
    else:
        cylinder_x(m, "leather_wrap", "leather", -.019, .019, .0365, inner=.032)
        for x in [-.015, .015]:
            cylinder_x(m, "binding_edge_" + str(x), "leather_light", x - .001, x + .001, .037, inner=.035)
    if tier == 4:
        for x in [-.077, .077]:
            cylinder_x(m, "brass_end_band_" + str(x), "brass", x - .004, x + .004, .035, inner=.031)
            for cy in [-.017, .017]:
                extrude(m, "band_rivet_" + str(x) + str(cy), "brass_dark",
                        arc(x, cy, .002, .002, 0, math.pi * 2, 6)[:-1], -.0315, -.03)
    seal = [(math.cos(2 * math.pi * i / 14) * (.021 if i % 2 else .0225),
             math.sin(2 * math.pi * i / 14) * (.021 if i % 2 else .0225)) for i in range(14)]
    extrude(m, "tier_wax_seal", "seal_" + str(tier), seal, -.042, -.035)
    for i in range(tier):
        x = (i - (tier - 1) / 2) * .0063
        bar(m, "seal_tier_cut_" + str(i), "cut", (x - .001, -.012), (x + .001, .012), .0027, -.0424)
    return m


def gems():
    specs = [
        ("surtr", "Surtr", "943B29", [(-.038, -.039), (.018, -.049), (.04, -.028), (.016, .065), (-.013, .032)]),
        ("ymir", "Ymir", "6E9FB5", [(-.038, -.038), (.028, -.036), (.039, -.009), (.023, .008), (.022, .038), (-.005, .055), (-.037, .025)]),
        ("thor", "Thor", "485D70", [(-.032, -.044), (.029, -.044), (.039, -.022), (.016, .061), (-.031, .028)]),
        ("nidhogg", "Nidhogg", "396349", [(.023, -.052), (-.013, -.038), (-.034, -.012), (-.023, .028), (.025, .061), (.003, .013), (.003, -.018)]),
        ("hel", "Hel", "A9A58F", [(-.036, -.024), (-.007, -.055), (.029, -.04), (.036, -.015), (.021, .009), (.014, .061), (-.023, .035)]),
        ("tyr", "Tyr", "853B35", [(-.039, -.03), (-.02, -.049), (.026, -.047), (.039, .018), (.016, .055), (-.03, .031)]),
        ("freyja", "Freyja", "8A3938", [(-.037, -.027), (-.014, -.052), (.02, -.049), (.04, -.02), (.034, .026), (.009, .05), (-.021, .039), (-.043, .003)]),
        ("odin", "Odin", "654671", [(-.031, -.033), (-.013, -.056), (.021, -.049), (.036, -.017), (.027, .029), (.004, .055), (-.022, .037), (-.038, .002)]),
        ("skadi", "Skadi", "769371", [(-.035, -.055), (.002, -.03), (.038, .056), (-.004, .031)]),
        ("heimdall", "Heimdall", "B18D42", [(-.014, -.051), (.015, -.049), (.048, .01), (.031, .03), (.011, .058), (-.009, .058), (-.029, .031), (-.048, .012)]),
        ("sleipnir", "Sleipnir", "655D53", [(-.033, -.055), (-.034, .015), (-.005, .054), (.026, .061), (.034, .031), (.021, -.055), (.008, -.051), (.013, -.008), (.003, .004), (-.008, -.01), (-.017, -.054)]),
    ]
    for ident, title, color, shape in specs:
        m = Model("gem_" + ident, title + " Gem", ["ECF_Gem" + title],
                  "Approved cut and inclusion are modeled geometry; opaque native material with subdued glow in inclusions only.")
        rgb = [int(color[i:i + 2], 16) for i in (0, 2, 4)]
        for suffix, factor in [("", 1), ("_light", 1.28), ("_dark", .64)]:
            shade = "".join(f"{min(255, int(c * factor)):02X}" for c in rgb)
            m.data["materials"]["gem" + suffix] = material(shade, .53)
        if ident == "nidhogg":
            extrude(m, "curved_fang_cut", "gem", shape, -.022, .024)
            extrude(m, "fang_left_facet", "gem_dark", [(-.013, -.038), (-.034, -.012), (-.023, .028),
                    (.025, .061), (-.012, .021), (-.022, -.011)], -.023, -.021)
            extrude(m, "fang_inner_facet", "gem_light", [(.025, .061), (.003, .013), (.003, -.018),
                    (.023, -.052), (-.006, -.019), (-.006, .012)], -.0234, -.021)
        elif ident == "sleipnir":
            extrude(m, "open_hoof_cut", "gem", shape, -.022, .024)
            for i in range(5):
                a, b = shape[i], shape[(i + 1) % len(shape)]
                bar(m, "hoof_facet" + str(i), "gem_light", a, b, .003, -.0225)
        else:
            rock(m, "faceted_crystal", shape, "gem", .045, inset=.76)
        if ident == "surtr":
            glyph(m, "ember_fracture", "rune_red", [(.009, .05), (.002, .027), (.01, .012), (-.004, -.004),
                                                       (.003, -.021), (.017, -.038)], .0025, -.0231)
            glyph(m, "ember_branch", "rune_gold", [(.002, .027), (-.013, .013), (-.011, -.002)], .0012, -.0233)
        elif ident == "ymir":
            for i, path in enumerate([[(-.025, .023), (-.009, .007), (-.015, -.015), (.004, -.034)],
                                      [(-.009, .007), (.014, .015), (.022, .03)], [(-.015, -.015), (.013, -.018), (.026, -.009)]]):
                glyph(m, "icy_fracture" + str(i), "ivory_light", path, .0018, -.0231)
        elif ident == "thor":
            glyph(m, "storm_inclusion", "rune_gold", [(.012, .046), (-.012, .007), (.009, .012), (-.012, -.037)], .0033, -.0231)
        elif ident == "nidhogg":
            glyph(m, "fang_vein", "moss", [(.017, .046), (-.009, .018), (-.013, -.006), (.006, -.034)], .0024, -.0238)
        elif ident == "hel":
            extrude(m, "dark_half", "obsidian", [(0, -.048), (.026, -.037), (.031, -.016), (.017, .008),
                    (.011, .052), (-.003, .022), (.002, -.003)], -.023, -.0205)
            glyph(m, "split_seam", "ivory_light", [(0, -.046), (-.007, -.026), (.002, -.003), (-.003, .022), (.011, .052)], .0017, -.0236)
        elif ident == "tyr":
            extrude(m, "blade_ridge_left", "steel", [(-.007, -.049), (.002, .048), (.013, .062), (.014, -.008)], -.028, -.021)
            extrude(m, "blade_ridge_right", "steel_edge", [(-.007, -.049), (.014, -.008), (.013, .062), (.02, .026)], -.0283, -.022)
        elif ident == "freyja":
            for i, path in enumerate([[(.004, -.038), (-.003, -.013), (.004, .012), (-.001, .039)],
                                      [(-.003, -.013), (-.023, -.001), (-.027, .014)],
                                      [(.004, .012), (.021, .026)], [(.001, .002), (-.015, .017)]]):
                glyph(m, "gold_branch" + str(i), "rune_gold", path, .0016, -.0231)
        elif ident == "odin":
            glyph(m, "eye_inclusion", "rune_gold", [(0, .027), (-.012, 0), (0, -.028), (.013, 0), (0, .027)], .0024, -.0231)
            extrude(m, "eye_core", "obsidian", [(0, .018), (-.004, 0), (0, -.02), (.005, 0)], -.0233, -.0223)
        elif ident == "skadi":
            glyph(m, "swept_ridge", "ivory_light", [(-.029, -.043), (.001, -.009), (.03, .046)], .0022, -.0231)
            glyph(m, "swept_branch", "gem_light", [(.001, -.009), (-.01, .021)], .002, -.0231)
        elif ident == "heimdall":
            for i, end in enumerate([(-.035, .009), (-.023, .028), (-.006, .046), (.009, .046), (.024, .023), (.036, .008)]):
                glyph(m, "fan_ridge" + str(i), "rune_gold", [(0, -.036), end], .0028, -.0231)
        else:
            glyph(m, "hoof_grain", "brass_dark", [(-.026, -.035), (-.023, .016), (.001, .044), (.02, .049)], .002, -.023)
        yield m


def validate(data):
    assert data["product"] == "Inscribe"
    assert len(data["parts"]) > 0
    for p in data["parts"]:
        assert p["material"] in data["materials"]
        assert len(p["triangles"]) % 3 == 0
        assert all(isinstance(i, int) and 0 <= i < len(p["vertices"]) for i in p["triangles"])
        assert all(math.isfinite(v) for pnt in p["vertices"] for v in pnt)
        for i in range(0, len(p["triangles"]), 3):
            a, b, c = [p["vertices"][index] for index in p["triangles"][i:i + 3]]
            u, v = [b[k] - a[k] for k in range(3)], [c[k] - a[k] for k in range(3)]
            cross = [u[1] * v[2] - u[2] * v[1], u[2] * v[0] - u[0] * v[2], u[0] * v[1] - u[1] * v[0]]
            assert sum(n * n for n in cross) > 1e-20, (data["id"], p["name"], i)


def main():
    models = list(runes()) + [chisel()] + [scroll(i) for i in range(1, 5)] + list(gems())
    assert len(models) == 21
    assert len({m.data["id"] for m in models}) == 21
    entries = []
    for m in models:
        validate(m.data)
        entries.append(m.save())
    (ROOT / "manifest.json").write_text(json.dumps({"schemaVersion": 1, "product": "Inscribe", "models": entries}, indent=2) + "\n", encoding="utf-8")
    materials = {}
    assets = []
    for m in models:
        data = m.data
        for key, value in data["materials"].items():
            material_id = "inscribe_" + key if not key.startswith("gem") else data["id"] + "_" + key
            materials[material_id] = {"id": material_id, "color": value["color"],
                                      "roughness": value["roughness"], "metallic": value["metallic"],
                                      "emission": [round(v * value["emissionStrength"], 5) for v in value["emission"]],
                                      "nativePrefab": "SerpentScale"}
        lo, hi = data["bounds"]["min"], data["bounds"]["max"]
        parts = []
        for part in data["parts"]:
            key = part["material"]
            material_id = "inscribe_" + key if not key.startswith("gem") else data["id"] + "_" + key
            parts.append({"name": part["name"], "material": material_id,
                          "vertices": [v for p in part["vertices"] for v in p],
                          "triangles": part["triangles"], "state": "always", "collidable": False})
        assets.append({"id": data["id"], "displayName": data["displayName"], "prefabs": data["prefabNames"],
                       "kind": "item", "parts": parts,
                       "icon": {"position": data["icon"]["cameraPosition"], "target": data["icon"]["cameraTarget"],
                                "orthographicSize": data["icon"]["orthographicSize"]},
                       "anchors": [{"name": "item_center", "position": [0, 0, 0]}],
                       "collider": {"center": [round((hi[a] + lo[a]) / 2, 7) for a in range(3)],
                                    "size": [round(hi[a] - lo[a], 7) for a in range(3)]}})
    bundle = {"schema": 1, "product": "Inscribe", "units": "meters", "materials": list(materials.values()), "assets": assets}
    (ROOT / "models.json").write_text(json.dumps(bundle, separators=(",", ":")) + "\n", encoding="utf-8")
    print(json.dumps({"models": len(entries), "triangles": sum(e["triangleCount"] for e in entries), "modelsFile": str(ROOT / "models.json")}))


if __name__ == "__main__":
    main()
