# License scope

Product author: expDARE. ExtraPotions Meadworks.

## Original code

All original Threshold code, including Threshold.Core, is GPL-3.0-or-later. Copyright (C) 2026 expDARE. Complete terms are in licenses/GPL-3.0.txt; see LICENSE-CODE.md.

## Original artwork and documentation

Original documentation and artwork use CC BY-NC-SA 4.0, with complete terms in licenses/CC-BY-NC-SA-4.0.txt; see LICENSE-ASSETS.md. icon.png is the byte-identical approved Threshold product artwork by expDARE (SHA256 106b441ab27cb0d61e74f60930c400c4810afdd6d408cbaf1ee5cb8dab7d707f).

## Adopted source and preserved notice

- Wayfare 0.3.2 (Gerald Glasgow) at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1, GPL-3.0-or-later (explicit grant at the top of `Wayfare/LICENSE`; the source files carry no headers). Adapted: src/Threshold.Core/Portals/ListHash.cs (Core/ListHash.cs), src/Threshold.Core/Portals/PortalKey.cs (the position key and legacy-entry rule of Targeting/PlayerFavourites.cs), src/Threshold.Core/Access/PortalAccessRules.cs (Portals/PortalAccess.cs; Wayfare's three modes), src/Threshold.Plugin/Portals/ThresholdPortalDiscovery.cs (Portals/PortalDiscovery.cs: portal prefab widening, reclassification and the world-load postfixes), src/Threshold.Plugin/Travel/ThresholdJumps.cs (QuickJumps/JumpPatches.cs, JumpScreen.cs, JumpTiming.cs) and 43 files under src/Threshold.Plugin/SeaGates/ (the Wayfare SeaGates file of the same name each, SeaGateFooting.cs also SeaGatePreview.cs). Meadworks-original: SeaGates/SeaGateBridge.cs and SeaGates/ThresholdSeaGatePicker.cs. Retained notice: THIRD_PARTY_NOTICES/Wayfare-GPL-3.0-or-later.txt.
- ValheimModLibs AreaLoading (Gerald Glasgow), same repository and pin, GPL-3.0-or-later (explicit grant in `ValheimModLibs/LICENSE`): src/Threshold.Core/Routing/JumpRules.cs adapts QuickWait.cs (with the timing steps of Wayfare's QuickJumps/JumpTiming.cs); src/Threshold.Plugin/Travel/ThresholdAreaLoading.cs adapts AreaLoader.cs, AreaSettle.cs, LandLoad.cs, ObjectLoad.cs and LoadPatches.cs. Retained notice: THIRD_PARTY_NOTICES/ValheimModLibs-GPL-3.0-or-later.txt.

## Behaviour references (no code adopted)

- PortalSelector 1.0.1 (Lazly), no licence, no public source (all rights reserved). Only its README, CHANGELOG, manifest and the Solo config file were used (reference snapshot cb2c6929); its DLL was never opened. The picker, hubs, return modes and converter are independent implementations of the documented behaviour.
- PotalMap 1.0.1 (korCaptain), no licence, no public source (all rights reserved). Only its README, CHANGELOG and manifest were used (reference snapshot 3ae3d8ee); its DLL and the bundled GuildsAPI.dll and ServerSync.dll were never opened. Categorized list, favourites, access modes, pin separation and optional fuel are independent implementations of the documented behaviour.

Never adopted: Wayfare `thunderstore/` art (icon, header, gallery), its SyncedConfig layer (Crucible C01 replaces it), packaging scripts and `PLAN.md`; any upstream DLL. Valheim game code is referenced by member name only through Harmony; nothing decompiled is copied.
