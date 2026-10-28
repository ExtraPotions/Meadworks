# License scope

Product author: expDARE. ExtraPotions Meadworks.

## Original code

Original Solstice code, including most of Solstice.Core and Solstice.Plugin, is GPL-3.0-or-later. Copyright (C) 2026 expDARE. Complete terms are in licenses/GPL-3.0.txt; see LICENSE-CODE.md. Original work: the settings model and Crucible binding, the season capability, the Tempest weather cooperation (WeatherYield), the module step-aside, the localisation catalog, the plugin lifecycle, all tests and the packaging scripts.

## Original artwork and documentation

Original documentation and artwork use CC BY-NC-SA 4.0, with complete terms in `licenses/CC-BY-NC-SA-4.0.txt`; see `LICENSE-ASSETS.md`. Solstice ships its approved 2026-10-28 package icon and no other image, model, or sound. The four season marks are drawn in memory at runtime. Seasonal music uses tracks shipped with Valheim. No Seasons audio file is included.

## Adopted sources and preserved notices

Seasons 1.10.6 (shudnal/Seasons) at 4054af6e8823d787921d673607e4942554872b66, Unlicense (public domain). Pin: assets/references/source/Seasons-4054af6e.json (zip SHA256 265879F0...8D13D). The Unlicense text is retained in licenses/Unlicense.txt and THIRD_PARTY_NOTICES/Seasons-Unlicense.txt. What is copied, adapted and left out:

- Copied (values and rule tables, as data): the default per-season effect values (SeasonSettings.LoadDefaultSeasonSettings), the default stat modifiers (SeasonStats), the default biome environment add/replace rules (SeasonBiomeEnvironments), and the protection defaults (fire heat, shield dome, the winter-hardy crop list).
- Adapted (re-expressed as Unity-free Solstice.Core code): the calendar arithmetic (SeasonState GetDayOfYear, GetSeason, GetDayInSeason, GetYearLengthInDays), the gradual night length and the day start fraction, the biome environment rule application (add, replace, remove), and the protected-place rules (indoors, terrain, shield dome) and the winter cold rule for plants.
- Adapted (plugin): the effect patches on the game's own methods (plant growth, plant winter cold, beehives, food and stamina drain, fire fuel, sap, rested duration, wind, rain protection) and the reading of the protection facts, taken from SeasonStatePatches.cs and rewritten around Solstice's settings and Crucible authority.
- Left out (see DEVELOPMENT.md for why): textures, minimap art, Seasons' music files, snow meshes, ice floes, the `SummerHeat/` health-cap meter, torch warmth, sleep-gated season change, real-time calendar, night length, procreation and drop multipliers as applied effects (Ember, Shepherd and Wildblood own those; Solstice publishes the numbers), bush respawn, trader items, Marketplace/EpicLoot/EWD/MyLittleUI compatibility, and the reference's own config sync and UI. Pickable perish, the season status, warm-clothes overheat, the client color tints, the season plate, the drawn season marks, the heat meter and the choice among the game's own tracks are Solstice's own code, not copies of those files.

Exact pinned provenance (Solstice path, upstream path, SHA256 of both) is in docs/source-provenance.json and is checked by `python tools/adopt-solstice.py --check`. No GPL-3.0-only or AGPL source is used, so Solstice stays GPL-3.0-or-later. No remote publication has occurred.
