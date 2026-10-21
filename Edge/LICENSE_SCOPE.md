# License scope

Product author: expDARE. ExtraPotions Meadworks.

## Original code

All original Edge code, including Edge.Core, is GPL-3.0-or-later. Copyright (C) 2026 expDARE. Complete terms are in licenses/GPL-3.0.txt; see LICENSE-CODE.md.

## Original artwork and documentation

Original documentation and artwork use CC BY-NC-SA 4.0, with complete terms in licenses/CC-BY-NC-SA-4.0.txt; see LICENSE-ASSETS.md. icon.png is the byte-identical approved Edge product artwork by expDARE, CC BY-NC-SA 4.0.

## Adopted source and preserved notice

- SecondaryAttacks 1.2.18 at f32966cad8303329b3490a40857205bd2a303b96 (sighsorry1029, GPL-3.0-only): the optional EpicLoot Burst bridge, per-shot ammo/resource and reload helpers, active-attack state and focused regression harness. Existing 1.2.17 notices remain for retained portions; the same retained GPLv3 LICENSE text applies to both pins. Compatible-contract discovery follows public 1.2.19 changelog behavior without claiming a 1.2.19 source pin. EpicLoot is an optional external runtime: neither its source nor its DLL is bundled.

- SecondaryAttacks 1.2.17 (sighsorry1029) at 4375a2b17297052a773c5b143b0cae21dc655ed8 (GPLv3 LICENSE, no "or later" statement in the repository, its README or its source files, so treated as GPL-3.0-only): the preset catalog (SecondaryAttackPresetCatalog.cs), the automatic weapon families and their cooldown group names (SecondaryAttackWeaponFamilyResolver.cs), the cooldown formula, group resolution and group table (MeleePresetCooldownSystem.cs, RangedSecondaryCooldownSystem.cs, SecondaryAttackCooldownGroupResolver.cs, SecondaryCooldownGroupSystem.cs), the quickstep constants, eligibility and flow (QuickstepSystem.cs) and the last-shield keys and rules (LastEquippedShieldSystem.cs). Adapted files carry "Portions from SecondaryAttacks 1.2.17 (sighsorry1029), GPL-3.0", the pin and SPDX GPL-3.0-only. Retained notice: THIRD_PARTY_NOTICES/SecondaryAttacks-GPL-3.0.txt (byte-identical to the LICENSE blob at the pin and to licenses/GPL-3.0.txt). Setting, section and option names of its config are used as names only elsewhere (settings table, converter).

Because GPL-3.0-only files are part of Edge.Core, the compiled assemblies as a whole are distributed under GPL-3.0.

## Behavior references (no code adopted)

- Digitalroot's Slope Combat Assistance 2.0.30 at a4b71e3df2b9402f22654fbd255a5426e5d3617e (AGPL-3.0, no "or later" statement: AGPL-3.0-only). Only its LICENSE and docs/README.md were read; no source file was opened for Edge. Slope assist (src/Edge.Core/Slope/SlopeAssist.cs, src/Edge.Plugin/Slope/SlopePatches.cs) is a clean-room implementation from the README, the config names and defaults and the game's own Attack fields. The original idea is flppyflip3's Slope Combat Fix (credited in that README).
- KeepWeaponsAfterSwim 1.0.1 (Lazly), build e7c8c6e1 (no license found, no public source: all rights reserved). Only its README, CHANGELOG, manifest and config file were used; its DLL was never opened. Swim re-equip (src/Edge.Core/Equipment/SwimReequip.cs) is a clean-room implementation of the documented behavior.

Never adopted: SecondaryAttacks Libs/ServerSync.dll (Crucible binds clients), LocalizationManager.cs (Crucible C05 replaces it), Thunderstore/icon.png, analysis artifacts (attack_ctor_il.txt, projectile_fields_reference and stormcaller_analysis_report PDF and HTML) and docs/warfare-decompiled (decompiled third-party code). Valheim game code is referenced by member name only through Harmony; nothing decompiled is copied.

Additional adoption: SecondaryAttacks 1.2.17 raw YAML schemas, normalized models, weapon and summon normalizers, parser and compiled snapshot, with source rules retained and Edge lifecycle/configuration seams. Embedded YAML defaults retain pinned bytes. The map/scalar layer merge subset is adapted from EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1, GPL-3.0-or-later; original Gerald Glasgow copyright retained and license text in THIRD_PARTY_NOTICES/EliteCrafting-GPL-3.0-or-later.txt. YamlDotNet 16.3.0 (MIT, package repository commit ae480660f4fb26f3eb0b41c1d1fcf21c0e9d9e73) is merged as private implementation inside Edge.dll; retained notice THIRD_PARTY_NOTICES/YamlDotNet-MIT.txt. No upstream ServerSync is incorporated.

Native adoption additionally includes the source definition compiler, source projectile controllers and launch/hit/resource/ammo/reload/attribution/cooldown helpers, Quickstep controller and last-shield identity runtime. The full native ranged/melee/Blood Magic execution, prefab apply/restore, owned cleanup and kit presentation are incorporated.

BepInEx.AssemblyPublicizer.MSBuild 0.4.3 (MIT, repository commit 874cfb60160c053cbefc7277f5f0cb6f7a732748) supplies a compiled IgnoresAccessChecksToAttribute source shim through its NuGet contentFiles. Its complete copyright/permission notice is retained at THIRD_PARTY_NOTICES/BepInEx.AssemblyPublicizer-MIT.txt; build instructions are in the repository's root `BUILDING.md`. The tool itself is not bundled in the runtime package.

Keep Crossbow Loaded (src/Edge.Plugin/Equipment/LoadedWeapons.cs, LoadedWeaponPatch.cs) is adapted from EliteCreaturesReborn 3.21.0 (Gerald Glasgow) at 9da28eb58adbe065be1e4baef779a448acd38e0f in geraldjglasgow/ValheimMods, GPL-3.0-or-later; original copyright retained and license text, byte-identical to EliteCreaturesReborn/LICENSE at the pin, in THIRD_PARTY_NOTICES/EliteCreaturesReborn-GPL-3.0-or-later.txt.
