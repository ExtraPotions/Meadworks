# Source availability notice

Product author: expDARE.

Saga.Core.dll and Saga.dll contain logic adapted from GrindstoneSkills 0.14.1 and ValheimModLibs (ValheimMods monorepo pinned at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1, GPL-3.0-or-later, Copyright (C) 2026 Gerald Glasgow; the files whose only later upstream change is the removal of item stars follow GrindstoneSkills 0.16.0 at 9da28eb58adbe065be1e4baef779a448acd38e0f) and from SecondaryAttacks 1.2.17 (pinned at 4375a2b17297052a773c5b143b0cae21dc655ed8, GPL-3.0). Saga.dll embeds five GrindstoneSkills skill icons adopted as-is under the GrindstoneSkills GPL notice. No binary or file of the ProgressivePowers and SNEAKer behaviour references is copied into Saga.

The Corresponding Source for this version is at https://github.com/ExtraPotions/Meadworks/tree/main/Saga. No copyright transfer is inferred. Original upstream copyright and license notices remain intact.

The licensed GrindstoneSkills display templates and their translations are also GPL-3.0-or-later. `assets/localization-source/grindstone-english.json` retains the source notice; `gs_` runtime catalog entries preserve its corresponding terms. Original Saga catalog text is authored by expDARE.

Local Sneak effects (2026-10-15): Soft Steps and Assassin behavior was implemented after reading GrindstoneSkills 0.18 at fdd1e1ad1c0462adb42abf25572cbc8f0245635a. This source-exposed slice uses Saga ownership/CS1 and the existing outgoing dispatcher, retains all prior notices and does not import upstream XP prediction.

Successful-backstab authentication (2026-10-15): the retained SecondaryAttacks BackstabSkillGainSystem.cs notice now represents retired inference code; its implementation and legacy float receiver were replaced by a validated native success insertion, bounded Saga receipt policy and existing Crucible Operations/PeerCommands adapters. All notices remain.

Forsaken power wheel (2026-10-21): the hold-to-choose timing in `src/Saga.Core/Powers/PowerWheel.cs` is adapted from BossRules 1.1.8 `ForsakenPowerShortcutInput` (GPL-3.0, Copyright (C) 2026 sighsorry, snapshot `assets/references/thunderstore-20261019/sighsorry-BossRules-1.1.8`). The wheel is drawn with Crucible.UI. The native Valheim radial renderer is not copied.


Successor corresponding source: this package uses the exact member map and build receipts in SOURCE-SNAPSHOT.json and BUILD-RECIPES/code-release-0.1.17/BUILD.json. The matching accepted provider-source supplements remain part of the release source inventory. No new Git commit is asserted.
