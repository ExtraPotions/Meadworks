# Source availability notice

Product author: expDARE.

Optional EpicLoot Burst support additionally adapts SecondaryAttacks 1.2.18 at f32966cad8303329b3490a40857205bd2a303b96 (sighsorry1029, GPL-3.0-only): the bridge, per-shot payment/reload helpers, active-attack state and focused regression harness. The existing GPLv3 notice is byte-identical to that pin's LICENSE. Compatible-contract discovery implements public 1.2.19 changelog behavior independently; no 1.2.19 code pin is claimed. EpicLoot callback APIs are referenced by name; its implementation and DLL are not incorporated or distributed.

Dodge Cancel is an original clean room feature inspired by the one-line DodgeCancel idea by SirJose. No DodgeCancel source, DLL or assets were accessed. Native Valheim APIs were inspected without copying game code. The optional toggle, input/animation adapter and attack-specific cancellation ownership are original Meadworks work; small lifecycle extensions to adopted SecondaryAttacks controllers retain their existing notices and provenance. The audited aedenthorn/ValheimMods pin contains no matching implementation and contributes no source to this feature.

Edge.Core.dll and Edge.dll contain logic adapted from SecondaryAttacks 1.2.17 (pinned at 4375a2b17297052a773c5b143b0cae21dc655ed8, GPL-3.0). Never-adopt files (SecondaryAttacks ServerSync.dll, LocalizationManager.cs, package icon, analysis artifacts and the decompiled third-party folder) and unapproved images are not included. Slope Combat Assistance (AGPL-3.0) and KeepWeaponsAfterSwim are behavior references only; none of their code or binaries are included.

The Corresponding Source for this version is at https://github.com/ExtraPotions/Meadworks/tree/main/Edge. No copyright transfer is inferred. Original upstream copyright and license notices remain intact.

Additional adoption: SecondaryAttacks 1.2.17 raw YAML schemas, normalized models, weapon and summon normalizers, parser and compiled snapshot, with source rules retained and Edge lifecycle/configuration seams. Embedded YAML defaults retain pinned bytes. The map/scalar layer merge subset is adapted from EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1, GPL-3.0-or-later; original Gerald Glasgow copyright retained and license text in THIRD_PARTY_NOTICES/EliteCrafting-GPL-3.0-or-later.txt. YamlDotNet 16.3.0 (MIT, package repository commit ae480660f4fb26f3eb0b41c1d1fcf21c0e9d9e73) is merged as private implementation inside Edge.dll; retained notice THIRD_PARTY_NOTICES/YamlDotNet-MIT.txt. No upstream ServerSync is incorporated.

Approved artwork: icon.png is original product artwork authored by expDARE under CC BY-NC-SA 4.0. The fixed SHA256 is cb716dbd13d5e8813b6ffda2442ae145f78b6ee8d19f135dde9946d995f49ef4. It is not the SecondaryAttacks package icon.

Build instructions are in the repository's root `BUILDING.md`. The publicizer attribute shim is supplied by a restored MIT NuGet contentFiles entry and its complete notice is included. Binary packages contain only Edge.dll and Edge.Core.dll; shared runtime/game assemblies and YAML sidecars are not included.

Keep Crossbow Loaded (src/Edge.Plugin/Equipment/LoadedWeapons.cs, LoadedWeaponPatch.cs) is adapted from EliteCreaturesReborn 3.21.0 (Gerald Glasgow) at 9da28eb58adbe065be1e4baef779a448acd38e0f in geraldjglasgow/ValheimMods, GPL-3.0-or-later; original copyright retained and license text, byte-identical to EliteCreaturesReborn/LICENSE at the pin, in THIRD_PARTY_NOTICES/EliteCreaturesReborn-GPL-3.0-or-later.txt.

Corresponding source for this build is identified by SOURCE-SNAPSHOT.json and BUILD-RECIPES/code-release-0.2.17/BUILD.json. Historical compiler-reference source and DLL supplements remain part of the release source inventory. No new Git commit or broader native acceptance is asserted.

Corresponding source for this build is identified by SOURCE-SNAPSHOT.json and BUILD-RECIPES/code-release-0.2.17/BUILD.json. Historical compiler-reference source and DLL supplements remain part of the release source inventory. No broader native acceptance is asserted.
