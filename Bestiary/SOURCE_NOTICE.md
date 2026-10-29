# Source availability notice

Product author: expDARE.

Bestiary contains code and text adapted from EliteCreaturesPack 0.7.2 and ValheimModLibs, pinned at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1 of https://github.com/geraldjglasgow/ValheimMods (GPL-3.0-or-later). The Bone Ballista, stand-in spawn counts and Rime Giant Ascension bonus are adapted from EliteCreaturesPack 0.9.0 at 3bb778330f23645ccddb42fbc0c6ffab72c842b6 of the same repository and license; its ballista bundle is not used (the game's own ballista and bone missile are bleached at run time). The creature, combat, weapon, recipe, spawn and loot runtime, stream loader, material/model helpers, local effects and animation/IK helpers retain their pinned algorithms and notices. The upstream entry point, Charter configuration and reflection handoff are replaced by Bestiary lifecycle, existing Crucible settings and capability adapters. Runtime registrations and event subscriptions are owned by Bestiary. The nine excluded animation clips remain absent; live game controllers supply their movement/hold roles. Unchanged upstream asset bundles and the nine Iron Gate-derived clips are not included; the approved Bestiary icon and independently rebuilt platform bundles are.

The Corresponding Source for this version is at https://github.com/ExtraPotions/Meadworks/tree/main/Bestiary. Its asset Corresponding Source includes the AssetWorkshop subset at a8ea29008f23dcebfd3afe93c157c6bf6e8d7be1, the authored clip curves and kit data, numerical Rime fitting data, and Bestiary's build adapters. Build and regeneration instructions are in the repository's root `BUILDING.md`. Original upstream copyright and license notices remain intact.

CreatureManager 1.2.5 workshop code at 47e45a6e8c3b389a51e42fc7387d1a4fb90af473 (https://github.com/sighsorry1029/CreatureManager) is adopted with GPL-3.0-only notices. Original domain validation, clone dependency transactions, last-known-good snapshots, bounded/authenticated PNG transfers and reference algorithms are retained. C01 hidden carriers replace ServerSync; Bestiary owns native patches, files, command/RPC registrations and teardown. Workshop-disabled sessions register known custom clone identities passively without template/faction/appearance writes or texture transfers. The Compendium uses the pinned TextsDialog pattern with original seven-language content. No source plugin, ServerSync DLL, recolored sample texture or MonsterLabZ-dependent sample is deployed. The combined plugin is GPL-3.0-only.

Adoption correction (2026-10-15): Triple Strike and its configuration were removed to match EliteCreaturesPack 0.11.0 commit 9866479d148af07d3bcd32a4fc0106d547ee5924. The current build contains no BearClaws runtime or flurry Harmony patches. Existing configuration files are not edited by this removal.


## Corresponding source for Bestiary 0.1.23

This source archive contains the source snapshot for the paired runtime assemblies. SOURCE-SNAPSHOT.json records exact member identities; BUILD-RECIPES records the frozen source-member map used for the successful builds, runtime hashes, normalized build commands and scoped test-evidence hashes. Existing third-party notices and accepted art are retained. Machine paths and raw private test/authentication records are omitted.

Corresponding source for this build is identified by SOURCE-SNAPSHOT.json and BUILD-RECIPES/code-release-0.1.26/BUILD.json. Historical compiler-reference source and DLL supplements remain part of the release source inventory. No new Git commit or broader native acceptance is asserted.

Corresponding source for this build is identified by SOURCE-SNAPSHOT.json and BUILD-RECIPES/code-release-0.1.26/BUILD.json. Historical compiler-reference source and DLL supplements remain part of the release source inventory. No broader native acceptance is asserted.
