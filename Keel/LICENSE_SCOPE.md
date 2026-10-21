# License scope

Product author: expDARE. ExtraPotions Meadworks.

## Original code

All original Keel code, including Keel.Core, is GPL-3.0-or-later. Copyright (C) 2026 expDARE. Complete terms are in licenses/GPL-3.0.txt; see LICENSE-CODE.md.

## Original artwork and documentation

Original documentation and artwork use CC BY-NC-SA 4.0, with complete terms in licenses/CC-BY-NC-SA-4.0.txt; see LICENSE-ASSETS.md.

## Adopted sources and preserved notices

- ShipConfig 1.4.2 (ValheimMods at 14d151ed8a16f7264a21efd35e19f8ac346276c1, GPL-3.0-or-later, Copyright (C) 2026 Gerald Glasgow): per-ship tuning fields and their effective values, global multipliers, build cost scaling, damage factor. Adapted files keep "Portions Copyright (C) 2026 Gerald Glasgow", the upstream path and a "Changed by expDARE, 2026:" line. Retained notice: THIRD_PARTY_NOTICES/ValheimMods-GPL-3.0-or-later.txt.
- GrindstoneSkills 0.15.0 Sailing (same pin, same licence and author): the perk share curve, shipwright health, helm speed, sea exploration factor, sailing experience, Wind Call and Lookout rules and setting names. Same header rule and notice.
- GrindstoneSkills `GrindstoneSkills/assets/skill_sailing.png` (same pin, same licence and author): the Sailing skill icon, adopted byte for byte as `assets/skill_sailing.png` and embedded in Keel.dll (approved 2026-10-08). The image is AI-generated (GrindstoneSkills `artwork/skill-icons-v2/README.md` and `generation.json`). Same retained notice; not covered by Keel's original-asset license.
- BepInEx.AssemblyPublicizer 0.4.3 (MIT, Copyright (c) 2022 BepInEx): package-provided compiled attribute shim, with the exact MIT notice retained.
- TheGreatestShips 0.9.10 (DeathMonger, no license found): behavior reference only. Its README, CHANGELOG, manifest and thunderstore.toml at 8aa408759078a666b1d485cdd28d7a2db5cb5d09 were read as data; no source file was read and the DLL is not decompiled.

Never adopted: ShipConfig's panel UI (`Panel/ShipPanel.cs`, `ShipPanelHud.cs`, `PanelRow.cs`, `PanelLook.cs`, `PanelHover.cs`, `PanelSettings.cs`, `GrindstoneLink.cs`; rebuilt on the Crucible.UI kit), ShipConfig's plugin entry and SyncedConfig, GrindstoneSkills' `CustomSkill` machinery (replaced by Crucible CS1) and `SailingApi`, and every upstream image except the Sailing icon above.