# License scope

Product author: expDARE. ExtraPotions Meadworks.

## Original code

All original Delve code, including Delve.Core, is GPL-3.0-or-later. Copyright (C) 2026 expDARE. Complete terms are in licenses/GPL-3.0.txt; see LICENSE-CODE.md.

## Original artwork and documentation

Original documentation and artwork use CC BY-NC-SA 4.0, with complete terms in licenses/CC-BY-NC-SA-4.0.txt; see LICENSE-ASSETS.md.

## Adopted sources and preserved notices

- RestockRegen 0.19.0 (liekos47, https://github.com/liekos47/valheim-restock-regen-serverside at aa179d9120b4b141fdb306825136ba05be0908b0, MIT, Copyright (c) 2026 liekos47): the loot chest refill decision and day stamps, the saved inventory item count, the chest history record and message, the spot key and spot matching, the regrow record file format and the per-spot restore decision with its build clearance test. Adapted files keep "Portions Copyright (c) 2026 liekos47, MIT", the upstream path and a "Changed by expDARE, 2026:" line. Retained notice: THIRD_PARTY_NOTICES/RestockRegen-MIT.txt (the LICENSE blob at the pin).
- Elite Creatures Reborn 3.20.0 (Gerald Glasgow, https://github.com/geraldjglasgow/ValheimMods at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1, GPL-3.0-or-later): the respawning rules (camp and dungeon spawner timers in world days, the arm rule for one-shot spawners). Same header rule. Retained notice: THIRD_PARTY_NOTICES/EliteCreaturesReborn-GPL-3.0-or-later.txt (the `EliteCreaturesReborn/LICENSE` blob at the pin). Wildblood, which adopts the rest of Elite Creatures Reborn, excludes its spawner respawn in favour of Delve.

## Behavior references (no code read or copied)

- Max_Dungeon_Rooms 2.0.42 (Digitalroot, AGPL-3.0): room count rules from its README, CHANGELOG and config only (pin `843870b3df71ccf275c50c7169e25fa91fe818bf`; only LICENSE and docs/README.md were read).
- MoreDungeonResets 1.0.0 (Loathe, no license found): full reset behavior from its README and config.
- Valheim_Admin_Tool 0.5.0 (QoLTeam, no license found): automatic reset switches and intervals and the player base skip, from its README and config.

Never adopted: RestockRegen's plugin entry, config binding and reload loop (`src/RestockRegenPlugin.cs`), its admin command parsing beyond the verb list, its icon, screenshots and tools; Elite Creatures Reborn's dungeon loot patch (Delve's chest refill owns dungeon chests) and everything outside its respawning rules.
