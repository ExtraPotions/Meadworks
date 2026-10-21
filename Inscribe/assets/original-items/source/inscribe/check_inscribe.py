# Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
import hashlib
import json
import math
import re
from pathlib import Path


root = Path(__file__).resolve().parent
bundle = json.loads((root / "models.json").read_text(encoding="utf-8"))
catalog = (root.parents[3] / "Inscribe/src/Inscribe.Plugin/Adopted/Rules/Model/StoneCatalog.cs").read_text(encoding="utf-8")
expected_stones = set(re.findall(r'"(awakening|recasting|ascension|cleansing|serpent|dvergr_chisel|gem_[a-z]+)"', catalog))
expected = expected_stones | {f"scroll_tier_{tier}" for tier in range(1, 5)}
assert bundle["schema"] == 1 and bundle["units"] == "meters"
assets = bundle["assets"]
assert {asset["id"] for asset in assets} == expected
assert len(assets) == len(expected) == 21
materials = {mat["id"] for mat in bundle["materials"]}
assert len(materials) == len(bundle["materials"])
prefabs = []
triangles = 0
for asset in assets:
    ident = asset["id"]
    prefab = "ECF_" + "".join(part.title() for part in ident.split("_")) if ident in expected_stones else "ScrollEnhance_Tier" + ident[-1]
    assert asset["prefabs"] == [prefab], (ident, asset["prefabs"])
    prefabs.extend(asset["prefabs"])
    volume = 0
    positions = []
    for part in asset["parts"]:
        vertices, faces = part["vertices"], part["triangles"]
        assert len(vertices) % 3 == len(faces) % 3 == 0
        assert part["material"] in materials
        assert part["state"] == "always" and part["collidable"] is False
        assert all(math.isfinite(v) for v in vertices)
        assert all(0 <= i < len(vertices) // 3 for i in faces)
        positions.extend(vertices[i:i + 3] for i in range(0, len(vertices), 3))
        for i in range(0, len(faces), 3):
            a, b, c = [vertices[j * 3:j * 3 + 3] for j in faces[i:i + 3]]
            volume += sum(a[k] * (b[(k + 1) % 3] * c[(k + 2) % 3] - b[(k + 2) % 3] * c[(k + 1) % 3]) for k in range(3)) / 6
        triangles += len(faces) // 3
    assert volume > 0, (ident, "inverted overall mesh volume")
    lo, hi = [min(v[a] for v in positions) for a in range(3)], [max(v[a] for v in positions) for a in range(3)]
    assert .07 <= max(hi[a] - lo[a] for a in range(3)) <= .25, (ident, "native hand-item scale")
    assert all(abs(asset["collider"]["size"][a] - hi[a] + lo[a]) < 1e-6 for a in range(3))
assert len(prefabs) == len(set(prefabs))

def luminance(rgb):
    linear = [v / 12.92 if v <= .04045 else ((v + .055) / 1.055) ** 2.4 for v in rgb[:3]]
    return sum(v * weight for v, weight in zip(linear, (.2126, .7152, .0722)))

surfaces = {mat["id"]: mat for mat in bundle["materials"]}
inlay = surfaces["inscribe_rune_pale"]
assert [round(v * 255) for v in inlay["color"][:3]] == [104, 80, 45]
assert inlay["emission"] == [0, 0, 0]
assert [asset["id"] for asset in assets if any(part["material"] == inlay["id"] for part in asset["parts"])] == ["cleansing"]
contrasts = {}
for name in ("inscribe_ivory", "inscribe_ivory_light", "inscribe_ivory_dark"):
    lo, hi = sorted((luminance(inlay["color"]), luminance(surfaces[name]["color"])))
    contrasts[name] = (hi + .05) / (lo + .05)
    assert contrasts[name] >= 3, (name, "Cleansing source albedo contrast", contrasts[name])
receipt = {"result": "PASS", "assetCount": len(assets), "triangleCount": triangles,
           "checks": ["actual StoneCatalog scope", "exact saved prefab identities", "flat JsonUtility mesh schema",
                      "finite indexed triangles", "outward overall signed volumes", "native hand-item meter scale",
                      "collider encloses visual geometry", "unique names and referenced materials",
                      "Cleansing-only nonemissive weathered gold inlay", "minimum 3:1 source albedo contrast against all ivory facets"],
           "cleansingInlay": {"rgb": [104, 80, 45], "emission": inlay["emission"], "sourceAlbedoContrasts": contrasts},
           "modelsSha256": hashlib.sha256((root / "models.json").read_bytes()).hexdigest().upper(),
           "limits": ["No rendered visual approval, Unity import, native material validation, or in-game evidence in this source check."]}
(root / "SOURCE-CHECKS.json").write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf-8")
print(json.dumps(receipt))
