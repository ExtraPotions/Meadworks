# Source availability notice

Product author: expDARE.

Larder.Core retains 19 recorded portable adaptations from FeastMaster 4.7.4, GrindstoneSkills 0.14.1 and RecipeCounts at GrindstoneSkills 0.19.0 (`7c710ed119b547783812e8171c86f31ce1f7ea50`). The native plugin adopts the corresponding food/mead, eating, recovery, exertion, kitchen, Cooking experience and HUD source modules. The reference pin is `9da28eb58adbe065be1e4baef779a448acd38e0f` (GrindstoneSkills 0.16.0); files the item-star removal did not touch keep their recorded ancestor revision `ad278ecf323901c3645eb1f9f6fac6833a0f8cb1`. Larder has no dish or item stars since 0.2.0. Starred eggs still count for recipes that need every ingredient, because the hen's stars are stored as item quality.

Larder distinguishes adapted portable/native code, the namespace-adapted ItemCopies library, original lifecycle/configuration/presentation adapters, English source strings, six original translation catalogs and the unchanged Cooking skill icon. These are selective module adoptions, not a whole upstream plugin fork. Original adapters may list behavior references without claiming copied construction.

FeastMaster's configuration settings are adopted with Crucible binding/read-at-use boundaries. The source ConfigMigration implementation and Charter transport library are not copied: Crucible provides those boundaries.

`Integration/SafeStamina.cs` and `Larder.Core/Exertion/SafeStaminaRules.cs` are original GPL-3.0-or-later code, Copyright (C) 2026 expDARE. Their behavior reference is [cartur-safe-stamina 1.0.10](https://github.com/jekkle/cartur-safe-stamina/tree/2960c141b64e55852c0d1b423c8d82e64e48555c), pinned at `2960c141b64e55852c0d1b423c8d82e64e48555c`. Inspection of that tree found no licence grant, so no source was copied and no third-party licence is invented. This is a behavior specification, separate from adopted upstream blobs. The default-off extension uses native hostility and Larder's existing adopted FeastMaster final cost/regeneration hooks; it adds no second cost pipeline or transport.

`Integration/MantleFoodIntegration.cs` is an original optional capability consumer. The live contract `meadworks.mantle.foodslots` 1.0 supplies read-only Food-cell coordinates from `com.meadworks.mantle`; the adapter reuses Larder's adopted portable Auto Eat selection and preserves the native single `UseItem` path. It copies no Mantle implementation, writes no inventory and falls back to ordinary native inventory selection when the optional provider is unavailable or invalid. Safe Stamina lifecycle is initialized after FeastMaster, ticked before its patch switch and shut down before FeastMaster; Mantle preference resolves the current capability at use and retains no provider subscription.

Preserved notices are `THIRD_PARTY_NOTICES/ValheimMods-GPL-3.0-or-later.txt` and `THIRD_PARTY_NOTICES/ValheimModLibs-GPL-3.0-or-later.txt`, byte-identical to the respective pinned license blobs. See `LICENSE_SCOPE.md` for the code/content/art distinctions.

The optional fermenter countdown is original expDARE code from the approved feature description. No fermenter timer source was found in the approved aedenthorn pin and none was adopted. `Larder.Core/Cooking/FermentCountdown.cs` formats the remaining world seconds. `KitchenHover` adds that line when Fermenter Timer is on, the barrel is sheltered and fermenting, and the hover already names the contents. `FermenterSpeed` and FeastMaster Fermenting supply the skill speed and effective duration. The GrindstoneSkills `FermenterHover.cs` adoption is retired: upstream removed that file in 0.15.0 with item stars and did not add a countdown. The Fermenter Timer setting key and default are unchanged.

The Corresponding Source for this version is at https://github.com/ExtraPotions/Meadworks/tree/main/Larder.

The additions `Larder.Core/Exertion/IgnoredCreatures.cs` and the ignored-creature/encumbrance settings are original GPL-3.0-or-later work by expDARE. Their additional behavior reference is the public Cartur's Safe Stamina 1.0.11 changelog, recorded separately from the inspected 1.0.10 source pin. No new reference implementation was read or copied. Exact prefab/displayed-name matching feeds the existing native hostility test, and default-off safe encumbrance joins the adopted FeastMaster cost/restoration and single regeneration contribution. Existing upstream blob identities, license notices and ownership/authority gates are preserved.

The build-only BepInEx.AssemblyPublicizer 0.4.3 dependency retains its MIT notice in THIRD_PARTY_NOTICES/BepInEx.AssemblyPublicizer-MIT.txt; its binary is not bundled.

Rested acquisition delay selectively adapts OpenKeep Homestead RestPatch, RestDelay and RestSettings at `3bb778330f23645ccddb42fbc0c6ffab72c842b6` (GPL-3.0-or-later). Existing FeastMaster/Grindstone pins remain unchanged. The exact raw pinned root LICENSE is byte-identical to the retained ValheimMods GPL notice. Larder uses its existing Recovery configuration/authority and changed-only patch lifecycle, binds the real native default instead of adopting the upstream five-second default, and restores its temporary native instance delay after a call or exception when the field still holds the applied value, preserving a different later-written delay. OpenKeep source coexistence disables only this delay hook.


## Corresponding source for Larder 0.2.15

This source archive contains the source snapshot for the paired runtime assemblies. SOURCE-SNAPSHOT.json records exact member identities; BUILD-RECIPES records the frozen source-member map used for the successful builds, runtime hashes, normalized build commands and scoped test-evidence hashes. Existing third-party notices and accepted art are retained. Machine paths and raw private test/authentication records are omitted.


Successor corresponding source: this package uses the exact member map and build receipts in SOURCE-SNAPSHOT.json and BUILD-RECIPES/code-release-0.2.21/BUILD.json. The matching accepted provider-source supplements remain part of the release source inventory. No new Git commit is asserted.

Corresponding source for this build is identified by SOURCE-SNAPSHOT.json and BUILD-RECIPES/code-release-0.2.21/BUILD.json. Historical compiler-reference source and DLL supplements remain part of the release source inventory. No broader native acceptance is asserted.

Corresponding source for this build is identified by SOURCE-SNAPSHOT.json and BUILD-RECIPES/code-release-0.2.21/BUILD.json. Historical compiler-reference source and DLL supplements remain part of the release source inventory. No broader native acceptance is asserted.
