# License scope

Product author: expDARE. ExtraPotions Meadworks.

## Original code and artwork

Original Inscribe code is GPL-3.0-or-later, Copyright 2026 expDARE. Complete terms: `licenses/GPL-3.0.txt` and `LICENSE-CODE.md`. Original documentation and the approved product icon are CC BY-NC-SA 4.0, with complete terms in `licenses/CC-BY-NC-SA-4.0.txt`; see `LICENSE-ASSETS.md`.

## Adopted sources and notices

- EliteCrafting 0.7.0, `geraldjglasgow/ValheimMods`, revision `ad278ecf323901c3645eb1f9f6fac6833a0f8cb1`, GPL-3.0-or-later, Copyright (C) 2026 Gerald Glasgow. The pinned tree equals release `ebe8caf`. Adopted native rule/model/parser, item/roll/rune/effect/drop/display/API/command paths, portable formulas, default data, English translations and four approved raw rune/lootglow bundles come from this pin. Adapted files retain the pin/copyright/SPDX notice. Complete notice: `THIRD_PARTY_NOTICES/EliteCrafting-GPL-3.0-or-later.txt`.
- ValheimModLibs bundle/model-dressing helpers from the same monorepo pin retain GPL-3.0-or-later notices. Complete license: `THIRD_PARTY_NOTICES/ValheimModLibs-GPL-3.0-or-later.txt`.
- ItemEnhancements 1.0.2, `ckforgame/Valheim.ItemEnhancements`, revision `02201869cbb3e1356f4b1931d01a778af3c07532`, GPL-3.0-only. Versions before 0.2.0 adopted its level/attempt codecs, stat calculator, equipment cache, scroll prefab/fusion/drop logic and default data. Inscribe 0.2.0 removed all of it (the enhancement system was retired); only its item key names (`ValheimEnhancement_Level`, `ValheimEnhancement_Crafter`) and scroll prefab names are read, as facts, to convert old items. The complete notice `THIRD_PARTY_NOTICES/ItemEnhancements-GPL-3.0.txt` is kept for the Corresponding Source of earlier versions.
- EliteCrafting 0.10.0, `geraldjglasgow/ValheimMods`, release revision `d3b57606cb24775d61b7bda244b22eb89e7e7717`, GPL-3.0-or-later, Copyright (C) 2026 Gerald Glasgow. The 2026-10-10 parity port merged its code changes since 0.7.0 into the adopted tree and its default YAML and English translations; each such file names this release and revision. Its new bundles (`ecf_gems`, `ecf_runetable`, `ecf_tablefx`, the 0.10.0 `ecf_runes`) and its essence/table images are not adopted.
- EliteCrafting 0.8.0 revision `dab238855e21e6623e06217b686492e66a6c2bc1` supplies converter test fixtures only under its GPL-3.0-or-later grant. Gameplay follows 0.10.0 (below).
- YamlDotNet 13.7.1 is internalized into Inscribe.dll and retains MIT terms in `THIRD_PARTY_NOTICES/YamlDotNet-MIT.txt`.

Since 0.2.0 no ItemEnhancements portions are included in the assemblies; the combined assemblies are distributed under GPL-3.0. Corresponding Source is available as described in `SOURCE_NOTICE.md`.

## Exclusions

The source package/prototype icons and models, ItemEnhancements icon, source configuration sync/server binder and camera/input patches remain excluded. The source scrolling tooltip window is replaced with the mandatory Crucible.UI kit. PlayerPatches adoption contains its stat logic, excluding its window/input/camera tail. ItemEnhancements configuration is an original adapter retaining compatible cumulative semantics. The private parser comes from its licensed NuGet package, not the upstream bundled binary.

Game code is referenced through installed assemblies and Harmony member names; no decompiled code is copied. Models/materials borrowed from the installed game at runtime are not extracted or shipped. Approved third-party artwork retains its source grant; it is outside the original-artwork CC license.
