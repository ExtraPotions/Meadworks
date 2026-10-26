# Source availability notice

Product author: expDARE.

Shepherd.Core.dll contains logic adapted from GrindstoneSkills 0.14.1 and EliteCreaturesReborn 3.20.0 (ValheimMods monorepo pinned at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1, GPL-3.0-or-later). No decompiled game code is included; game members are referenced by name only.

The ECR breeding inputs now follow 3.30.0 at `0471a49e972e4453a453e365cb90b5fd20d0f4ea` under the same license and retained notice. All six reviewed breeding files match the refreshed 3.30.2 snapshot. OffspringStamp and LegacyBreeding adapt the native-quality preservation rule: Wildblood's permission controls birth level/quality writes, and legacy egg migration preserves saved quality. Wildblood owns stars and mutations; no ECR tame-star switches, TameStrip, or mutation algorithms are incorporated. The original Husbandry and other source pins remain intact. The file-by-file decisions are in `docs/ecr-breeding-refresh.md`.

Shepherd.dll also incorporates the actual pinned GrindstoneSkills Husbandry settings, keeper facts, taming/calm/petting paths and its unchanged Husbandry skill icon, plus ValheimModLibs' embedded-sprite loader. Native gameplay algorithms remain in those source files. Explicit adaptations use Crucible synchronized configuration, CS1 levels, per-module source coexistence, exception reporting and owned patch/RPC/icon lifecycles. Pet RPCs additionally validate sender/proximity; contentment uses Shepherd's existing millisecond save keys. This incorporation does not claim that all upstream Husbandry or Shepherd features are finished.

The Corresponding Source for this version is at https://github.com/ExtraPotions/Meadworks/tree/main/Shepherd. No copyright transfer is inferred. Original upstream copyright and license notices remain intact.

Trough pieces, feeder registry, trough eating and the feeder recipe parse are adapted from GrindstoneSkills 0.14.1 Animal Feeder sources (FeederPrefab, FeederRecipe, FeederGate, Feeders, FeederLife, FeederEating) and OpenKeep 3.0.0 PetEating (same monorepo pin and licence). Shepherd changes: three trough pieces, the grindstone_feeder alias, troughs-then-containers search, Burrow feed-source capability and the owner-routed take in place of a local removal. FeedingTrough is behaviour only; none of its code, models or icons are included.

Breeding pace, growth speed, twin conception, the newborn claim slot and grow-up/hatch carries are adapted from GrindstoneSkills 0.14.1 (BreedingCheck, BreedingPace, BirthRolls, TwinPregnancy, GrowingUp) and EliteCreaturesReborn 3.20.0 (Patches/BreedingPatch, Breeding/Lineage, Breeding/Parents), same pin and licence. Shepherd changes: decisions in the portable core, Shepherd lineage keys and egg record, twins due after Twin Delay; ECR mutation code is not included (owned by Wildblood).

Herd experience (taming progress, tame with first-of-kind discovery, feeding, births) is adapted from GrindstoneSkills 0.14.1 Experience/HerdExperience, HerdSnapshot, HerdWatch and TameDiscovery (same pin and licence). Shepherd changes: the decision and discovery list in the portable core, Shepherd's `shepherd.births` counter and `shepherd.tamed_kinds` list (GrindstoneSkills' list is read, never edited), ticked from the plugin update.

Yield and companions (butcher yield, produce, extra honey, pack leader, routed husbandry credit) are adapted from GrindstoneSkills 0.14.1 Yield/*, Companions/PackLeader, Core/HusbandryCredit, Core/PlayerIds (same pin and licence); Yield/Butchering, ButcherContext, YieldCatalog, YieldDiscovery, the Core/PrefabIndex scene walk and Core/HusbandryYieldSettings follow GrindstoneSkills 0.16.0 (ValheimMods at 9da28eb58adbe065be1e4baef779a448acd38e0f, same licence), which removed item stars and Prime Cuts. Shepherd changes: decisions in the portable core (YieldMath, ProduceClock), and the next produce roll under `shepherd.produce_next`. Herd limits and the herd room hover adapt GrindstoneSkills Lore/BreedingLore and Lore/LoreHover.

Follow Command (approved 2026-10-07) is reimplemented from the documented behaviour of Cartur's Follow Command (https://github.com/jekkle/cartur-follow-command). On 2026-10-07 that repository showed no LICENSE file and its README states no licence grant, so no code, text or assets were copied: Shepherd sets the game's own `Tameable.m_commandable` flag on uncommandable non-summon tameables with native MonsterAI and restores only the flags it set. The October 16 guard follows the shipped native command handler's AI requirement; no decompiled game code is included.

Animal lore now adapts the five Husbandry/Lore files at the same GPL pin. HerdCallout and Core/FloatingText supply birth callout presentation, adapted to typed animal-owner-verified native ZNetView handlers with exact cleanup and local C05 strings.

The October 16, 2026 feeding fix adds an original one-meter vertical reach guard to the portable FeedMath and the adapted TroughEating approach/arrival checks. It prevents the confirmed upper-floor meal while Require Move To Feed is enabled; the explicit remote-feeding setting retains its existing range behavior. No additional upstream source or assets were copied, and all recorded upstream revisions, blob hashes and license notices remain unchanged.

The bounded wall-reach update independently implements the public FeedingTrough 1.2.0 behavior specification: https://thunderstore.io/c/valheim/p/Brainless_Azura/FeedingTrough/changelog/. Reach Through Walls defaults to one meter and caps at two; its native collider queries, solid-span union and final consumption checks are original MeadWorks code. No FeedingTrough implementation, models or icons were copied. Existing adapted TroughEating source retains its upstream notices and revision.

The native-young feeding update follows the public FeedingTrough 1.2.2 behavior description and observed shipped-game component contracts. YoungMealPacing and YoungFeeding are original GPL-3.0-or-later code by expDARE: they use the young creature's native foods and persist a world-time meal timestamp without adding taming or growth callbacks. The existing adapted TroughEating eligibility boundary is extended narrowly, retaining its upstream notices and pins. No FeedingTrough implementation or assets, or decompiled game implementation, are included.

The build-only BepInEx.AssemblyPublicizer 0.4.3 dependency retains its MIT notice in THIRD_PARTY_NOTICES/BepInEx.AssemblyPublicizer-MIT.txt; its binary is not bundled. The original package artwork is covered by LICENSE-ASSETS.md.

Honey production settings/rate/progress selectively adapt OpenKeep HiveSettings, HiveRate, HivePatch and HiveProgress at ValheimMods `3bb778330f23645ccddb42fbc0c6ffab72c842b6`, GPL-3.0-or-later. Existing ad278/9da source pins and every prior license notice remain unchanged; the new raw pinned LICENSE exactly matches the retained ValheimMods notice. The original Shepherd integration owns a separate production patch lifecycle and narrow OpenKeep source coexistence. The adapted hook keeps saved progress in native units between calls, uses the real native producer and its strict threshold/remainder semantics only while opted in, and adds no persistent protocol or second timer. Existing ExtraHoney remains the separate original harvest-phase adaptation.


## Corresponding source for Shepherd 0.1.20

This source archive contains the source snapshot for the paired runtime assemblies. SOURCE-SNAPSHOT.json records exact member identities; BUILD-RECIPES records the frozen source-member map used for the successful builds, runtime hashes, normalized build commands and scoped test-evidence hashes. Existing third-party notices and accepted art are retained. Machine paths and raw private test/authentication records are omitted.


Successor corresponding source: this package uses the exact member map and build receipts in SOURCE-SNAPSHOT.json and BUILD-RECIPES/code-release-0.1.21/BUILD.json. The matching accepted provider-source supplements remain part of the release source inventory. No new Git commit is asserted.
