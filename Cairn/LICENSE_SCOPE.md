# License scope

Product author: expDARE. ExtraPotions Meadworks.

## Original code

All original Cairn code, including Cairn.Core, is GPL-3.0-or-later. Copyright (C) 2026 expDARE. Complete terms are in licenses/GPL-3.0.txt; see LICENSE-CODE.md.

## Original artwork and documentation

Original documentation and artwork use CC BY-NC-SA 4.0, with complete terms in licenses/CC-BY-NC-SA-4.0.txt; see LICENSE-ASSETS.md. Cairn ships no image other than its approved package icon.

## Adopted sources and preserved notices

- Pathfinder 2.2.5 (CrystalFerrai/ValheimMods) at f8a2cb3edca0417d54281d1353680dff46c4ed62 (Apache-2.0, Copyright 2026 Crystal Ferrai): the exploration radius computation (base radius, ship within 21 m, light, fog and particle weather, altitude, forest location terms, multiplier and radius clamps, interior rule) and the UpdateExplore transpiler, from Pathfinder/PathfinderPlugin.cs. Adapted files (src/Cairn.Core/Radius/RadiusModel.cs, src/Cairn.Plugin/Radius/CairnRadius.cs) keep the Apache-2.0 header and state the changes (Apache-2.0 section 4(b)). Retained license: THIRD_PARTY_NOTICES/Pathfinder-Apache-2.0.txt (the repository's license.txt at the pin). The repository has no NOTICE file at the pin. ConditionalConfigSync and the HUD text objects are not adopted.
- NoFogBruh 2.1.2 (Vapok/NoFogBruh) at 6a1bfe69271a0db58e51459767f9cbce5b3ac580 (MIT, Copyright (c) 2023 Pete Navarra (aka Vapok)): the scene paths of the fog and mist objects and the switch hooks (fog density, snow glint, mist emitters, distant fog emitter, particle mist), from NoFogBruh/Features/FogTargetManager.cs and DisableFogComponent.cs. Adapted files (src/Cairn.Core/Visibility/VisibilityTargets.cs, src/Cairn.Plugin/Visibility/CairnVisibility.cs) keep the copyright line. Retained notice: THIRD_PARTY_NOTICES/NoFogBruh-MIT.txt. Weather-event code (WeatherEventsComponent.cs), telemetry, splash and error reporting are not adopted; its environment names are used as game identifiers only in src/Cairn.Core/Hud/WeatherCategories.cs.
- BetterFog 1.0.2 (JevMods/BetterFog) at 208646c7edd89c57b93cbb1c934a12830910dcb2 (MIT, Copyright (c) 2026 JevMods): the Smoke.GetAlpha hook (src/BetterFog/Patches/SmokePatches.cs), turned into removal. Same adapted files as NoFogBruh. Retained notice: THIRD_PARTY_NOTICES/BetterFog-MIT.txt.
- BetterSleepBruh 2.1.0 (Vapok/BetterSleepBruh) at fe2bb81866e58f3c4afeb832876cba6ebf1e12cd (MIT, Copyright (c) 2026 Pete Navarra (aka Vapok)): the sleep row's height (40 units), its 1-unit gap under the minimap and the bonus text color (since 0.1.6 the row is drawn on Cairn's own heading plate; the 0.39 strip, 0.25 bar, paddings and cells are no longer used) from BetterSleepBruh/Components/SleepHudView.cs. Adapted file: src/Cairn.Core/Hud/SleepRowLayout.cs. No code structure, image or icon is used; the moon and bed icons stay Cairn's own. Retained notice: THIRD_PARTY_NOTICES/BetterSleepBruh-MIT.txt.

Apache-2.0 and MIT code combine into this GPL-3.0-or-later work.

## Behavior references (no code)

- TimeWeatherHud 1.1.0 (Lazly): no license found. Cairn's clock, weather categories, strip compass and marker logic (src/Cairn.Core/Hud) are reimplemented from its README, CHANGELOG, manifest and config only; the DLL was never opened and no icon, frame art or README screenshot is used. The HUD's look (one dark plate with a thin gold rim, gold text and rule, a triangle pointer, ticks under the numbers, plain heading text) is matched by eye to expDARE's own in-game captures and drawn with plain Images and code shapes. Pin: assets/references/source/TimeWeatherHud-a6cf7db4.
- GrindstoneSkills 0.14.1 (GPL-3.0-or-later): the Exploration skill follows its linear "At 100" perk pattern; no file is copied. Its public SailingApi is called by reflection at runtime when it is installed.
