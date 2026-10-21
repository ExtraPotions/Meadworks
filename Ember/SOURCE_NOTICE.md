# Source availability notice

Product author: expDARE.

Ember.Core.dll and Ember.dll contain logic adapted from BetterSleepBruh 2.1.0 (pinned at fe2bb81866e58f3c4afeb832876cba6ebf1e12cd, MIT) and DoorOpenerBruh 2.0.10 (pinned at dafb2af2418710dd1f87907e8b30d4efb1b68a20, MIT). The decompiled game files at the BetterSleepBruh repository root, all upstream images and BetterSleepBruh's HUD view are never adopted; the only image is Ember's own approved `icon.png`.

The Corresponding Source for this version is at https://github.com/ExtraPotions/Meadworks/tree/main/Ember. No copyright transfer is inferred. Original upstream copyright and license notices remain intact.


The source includes the full Core/native implementation and preserved notices; build instructions are in the repository's root `BUILDING.md`. The MIT publicizer attribute shim is compiled from the pinned 0.4.3 package; its exact permission notice is retained in THIRD_PARTY_NOTICES/BepInEx.AssemblyPublicizer-MIT.txt. No game/runtime dependency assemblies are distributed.

The optional post-death standing behavior adapts only OpenKeep's BedStandPatch.cs at ValheimMods 684671f98de54db6ea6652092ec041f44016c37d, GPL-3.0-or-later. Copyright and full GPL text remain in THIRD_PARTY_NOTICES/OpenKeep-GPL-3.0-or-later.txt. Existing BetterSleepBruh and DoorOpenerBruh source pins are unchanged.

Nearest-known-owned-bed death respawn additionally adapts OpenKeep's Homestead bed-point codec, registry, death selection and native invalidation/lifecycle patches at the same pin. Ember adds its own authority and compatibility gates, character/world scope checks and finite coordinate validation. The full GPL notice above also covers them. No game implementation is included in the source.

Quick Respawn additionally adapts Homestead BedWait/BedLoadPatch and ValheimModLibs AreaLoading QuickWait/AreaSettle at that same pin. Ember uses observed request ownership, reversible per-call loading thresholds and bounded object-quiet sampling. The retained GPL notice also covers these adaptations.

The map-marker feature selectively adapts Homestead BedPins.cs, BedPinsPatch.cs and the static tint portions of BedPinLook.cs and BedPinLookPatch.cs at the same pin. The death-choice feature additionally adapts the native death-choice, map/input, label, candidate presentation and deferred click behavior from that pinned Homestead implementation and ValheimModLibs MapClicks. The full ValheimModLibs notice is additionally retained in THIRD_PARTY_NOTICES/ValheimModLibs-GPL-3.0-or-later.txt. Existing notices remain preserved.


The optional torch schedule selectively adapts OpenKeep TorchSwitch.cs, TorchNight.cs, TorchKeep.cs and TorchHoverPatch.cs at the same 684671f98de54db6ea6652092ec041f44016c37d pin, GPL-3.0-or-later. The full retained OpenKeep notice also covers these files. Fuel-free/smokeless fire comfort and the authenticated keep command transport are MeadWorks implementations; TorchesAreFires remains a behavior-only reference with no copied code. Existing MIT source records and license notices remain unchanged.
