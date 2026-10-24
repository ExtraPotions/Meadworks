# License scope

Product author: expDARE. ExtraPotions Meadworks.

## Original code

All original Wildblood code, including Wildblood.Core, is GPL-3.0-or-later. Copyright (C) 2026 expDARE. Complete terms are in licenses/GPL-3.0.txt; see LICENSE-CODE.md.

## Original artwork and documentation

Original documentation and artwork use CC BY-NC-SA 4.0, with complete terms in licenses/CC-BY-NC-SA-4.0.txt; see LICENSE-ASSETS.md. Wildblood ships its approved original package icon and original raid-chest runtime artwork. The raid-chest source, generator/editor code and separate notices are retained under assets/raids/source; no game artwork is distributed.

## Adopted sources and preserved notices

- EliteCreaturesReborn 3.20.0 (geraldjglasgow/ValheimMods) at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1 (GPL-3.0-or-later, Copyright (C) 2026 Gerald Glasgow): difficulty names, preset tables and cells, star power and its defaults, mutation catalog and colours, body-size and kind bars, Custom and preset trait rolls, trait inheritance, aspect catalog, defaults, rotation and roller with Bountiful extras, boss trophy count, extra rolls, scaled amounts and the loot engine's step order. Adapted files keep "Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later". Retained notice: THIRD_PARTY_NOTICES/EliteCreaturesReborn-GPL-3.0-or-later.txt.
- CreatureManager 1.2.5 (sighsorry1029) at 47e45a6e8c3b389a51e42fc7387d1a4fb90af473 (GPLv3 LICENSE, no "or later" statement in the repository or its sources, so treated as GPL-3.0-only): the Amount loot formula (CreatureLoot.cs CalculateAmount), the distance-scaling multiplier and its spawn-source rule (CreatureLevelManager.cs, in Core DistanceScaling.cs), the Karma console verb (CreatureConsoleCommands.cs ExecuteKarma, in KarmaCommand.cs), the Protected Prefabs rule (the FrozenKing_p2 check of CreatureLevelManager.cs, read from the setting here) and the modifier mask bit order for the converter (CreatureModifierManager.cs). Adapted files carry "Portions from CreatureManager 1.2.5 (sighsorry1029), GPL-3.0" and SPDX GPL-3.0-only. Retained notice: THIRD_PARTY_NOTICES/CreatureManager-GPL-3.0.txt. The repository's MIT LICENSE.txt (Azumatt template residue) is not needed: no template code is adopted. Setting, modifier and option names are used as names only elsewhere.
- SaveYourLuckBruh 1.0.2 at ece802da9f806f3ee9d6e50552beec479abae4ce (tag v1.0.2, MIT, Copyright (c) 2026 Pete Navarra (aka Vapok)): the counter entry format and world key (Core/StorageManager.cs). Adapted file keeps "Portions Copyright (c) 2026 Pete Navarra (aka Vapok), MIT". Retained notice: THIRD_PARTY_NOTICES/SaveYourLuckBruh-MIT.txt. Killer attribution is new (SaveYourLuckBruh's README promises it; its code does not do it).
- BossRules 1.1.8 (sighsorry1029) at ae28779955a8979a96a43de373504d3a35f6868f (GPLv3 LICENSE without an additional "or later" grant, treated as GPL-3.0-only): abandoned summoned-boss timer, actual offering capture and stack-limited refund behavior from DespawnRulesManager.cs, AltarRuntime.Refunds.cs and DespawnRefundExecutor.cs. The native adaptation retains the BossRules source notice and SPDX GPL-3.0-only. Wildblood adds its own server authority and durable ledger transitions. Retained notice: THIRD_PARTY_NOTICES/BossRules-GPL-3.0.txt. Exact reviewed source blobs and hashes are recorded in docs/2026-10-22-boss-abandon-provenance.json. The separate tamed-pressure adaptation and its precise scope are recorded in docs/2026-10-23-tamed-pressure-provenance.json; no other BossRules features or assets are adopted.

Because a GPL-3.0-only file is part of Wildblood.Core, the compiled assemblies as a whole are distributed under GPL-3.0.

Never adopted: EliteCreaturesReborn Rules/ServerLock.cs (Crucible binds clients) and assets/globe.png (provenance not verified); CreatureManager Libs/ServerSync.dll and its cm_*.png creature textures (recoloured game textures); SaveYourLuckBruh images, telemetry and splash code. Valheim game code is referenced by member name only through Harmony; nothing decompiled is copied.
