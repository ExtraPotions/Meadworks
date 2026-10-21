# License scope

Product author: expDARE. ExtraPotions Meadworks.

## Original code

All original Bestiary code, including Bestiary.Core, is GPL-3.0-or-later. Copyright (C) 2026 expDARE. Complete terms are in licenses/GPL-3.0.txt; see LICENSE-CODE.md.

## Original artwork and documentation

Original documentation and artwork use CC BY-NC-SA 4.0, with complete terms in licenses/CC-BY-NC-SA-4.0.txt; see LICENSE-ASSETS.md.

## Adopted sources and preserved notices

- EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1 (GPL-3.0-or-later, Copyright (C) 2026 Gerald Glasgow). Adapted: the spawn rules (mimic chance, slinger share, skeleton draw, Rime Giant mountain hash, Kraken spawn conditions, Executioner chamber roll), the creature, drop, item and recipe numbers, the recipe text format, the settings table (sections, keys, defaults, ranges, descriptions) and the English words. Retained notice: THIRD_PARTY_NOTICES/EliteCreaturesPack-GPL-3.0-or-later.txt (EliteCreaturesPack/LICENSE at the pin, byte for byte).
- The pinned EliteCreaturesPack native creature, combat, spawn, recipe, item and loot implementations are adopted under `src/Bestiary.Plugin/Native`. Upstream entry-point, Charter settings and reflection handoff are replaced with Bestiary lifecycle, existing C01 values and Crucible capability adapters. Original source notices are preserved.
- EliteCreaturesPack 0.9.0 at ValheimMods 3bb778330f23645ccddb42fbc0c6ffab72c842b6 (same GPL-3.0-or-later notice, byte for byte at that pin). Adapted: the Bone Ballista (hold, aim, reload from Bone Missiles, shot, crafting, words), the stand-in counts that keep bone piles, greydwarf nests and wild spawns within their caps, and the Rime Giant's Ascension Rune bonus. The upstream ballista bundle is not used: the Bone Ballista wears the running game's own ballista model, and the Bone Missile the game's own unused bone missile; both are bleached to bone at run time from the game's own textures (`Prefabs/BoneDye.cs`), so no art is shipped for them. The Paws of the Bear triple strike adapted earlier was removed on 2026-10-15 and is no longer shipped (see SOURCE_NOTICE.md).
- ValheimModLibs BundlePrefabs and LocalEffects at the same pin (GPL-3.0-or-later, Copyright (C) 2026 Gerald Glasgow): PrefabBench, NetPrefabs, ItemPrefabs, EmbeddedBundle, GameMaterials, ModelBounds, BundleEffects and six LocalEffects helpers, adapted. Retained notice: THIRD_PARTY_NOTICES/ValheimModLibs-GPL-3.0-or-later.txt (ValheimModLibs/LICENSE at the pin, byte for byte).
- Adapted files carry "Portions Copyright (C) 2026 Gerald Glasgow" with the pin and "Changed by expDARE, 2026:".
- Eight Windows/Linux bundle pairs are independently rebuilt from GPL sources and permitted authored data. The AssetWorkshop pin is a8ea29008f23dcebfd3afe93c157c6bf6e8d7be1; its unchanged license is retained in THIRD_PARTY_NOTICES. The approved original Bestiary icon uses the artwork license above. The nine excluded Iron Gate-derived clips and unchanged upstream bundle bytes are not included.
- CreatureManager 1.2.5 at 47e45a6e8c3b389a51e42fc7387d1a4fb90af473 (GPL-3.0-only, sighsorry1029): twelve workshop source engines, the selected native patch subset, reference dispatch, language hooks, FrozenKing identity guard and Compendium page insertion pattern. Level, modifier, Karma, loot, source plugin/config manager, ServerSync DLL, MonsterLabZ samples and the recolored sample texture pack are excluded. The combined plugin is GPL-3.0-only; individual original/ECP grants remain GPL-3.0-or-later. Both upstream notices are retained byte for byte.
- YamlDotNet 16.3.0 is merged and internalized, MIT notice retained unchanged (verified against the exact v16.3.0 tag). The pinned source ILRepack target is adapted to merge only Bestiary and YamlDotNet, with no game/profile deployment.
