# License scope

Product author: expDARE. ExtraPotions Meadworks.

## Original code

All original Ember code, including Ember.Core, is GPL-3.0-or-later. Copyright (C) 2026 expDARE. Complete terms are in licenses/GPL-3.0.txt; see LICENSE-CODE.md.

## Original artwork and documentation

Original documentation and artwork use CC BY-NC-SA 4.0, with complete terms in licenses/CC-BY-NC-SA-4.0.txt; see LICENSE-ASSETS.md.

## Adopted sources and preserved notices

- BetterSleepBruh 2.1.0 at fe2bb81866e58f3c4afeb832876cba6ebf1e12cd (MIT, Copyright (c) 2026 Pete Navarra (aka Vapok)): sleep window, occupancy counts, rate formula, testing offsets, boost step with fade and morning cap, all-in-bed skip decision, recount throttle and heartbeat, station timer safeguards with the saved boost total, combined into the GPL-3.0-or-later work. Adapted files keep "Portions Copyright (c) 2026 Pete Navarra (aka Vapok), MIT". Retained notice: THIRD_PARTY_NOTICES/BetterSleepBruh-MIT.txt.
- DoorOpenerBruh 2.0.10 at dafb2af2418710dd1f87907e8b30d4efb1b68a20 (MIT, Copyright (c) 2023 Pete Navarra (aka Vapok)): door kind table, automation mechanic and key rule, collider-bounds distance, open and close hysteresis with auto-opened tracking, combined into the GPL-3.0-or-later work. Adapted files keep "Portions Copyright (c) 2023 Pete Navarra (aka Vapok), MIT". Retained notice: THIRD_PARTY_NOTICES/DoorOpenerBruh-MIT.txt.
- BepInEx.AssemblyPublicizer 0.4.3 (MIT, Copyright (c) 2022 BepInEx): package-provided compiled attribute shim, with exact MIT notice and corresponding-source instructions retained.
- TorchesAreFires 1.1.0 (blacks7ar, no license found): behavior reference only. Nothing is copied and the DLL is not decompiled; `src/Ember.Core/Torches/TorchRules.cs` and `src/Ember.Plugin/Torches/TorchRuntime.cs` are clean-room reimplementations from its README, CHANGELOG and config names.

Never adopted: the decompiled game files at the BetterSleepBruh repository root (EnvMan.cs, ZNet.cs), BetterSleepBruh and DoorOpenerBruh images (provenance not verified), BetterSleepBruh SleepHudView.cs (behavior rebuilt on the kit), Vapok.Common, telemetry and splash code.
