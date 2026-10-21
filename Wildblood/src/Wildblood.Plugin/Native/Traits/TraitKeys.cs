// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Traits
{
    /// <summary>
    /// The ZDO key strings this slice owns. All are prefixed "wildblood."; legacy ecr keys remain readable and untouched, so they never collide
    /// with the game's keys or another slice's. String overloads of the ZDO getters/setters are used, so no manual
    /// hashing is needed.
    /// </summary>
    public static class TraitKeys
    {
        /// <summary>Set once a creature's traits have been rolled, so it is never re-rolled on the owner.</summary>
        public const string Resolved = "wildblood.resolved";

        /// <summary>Saved source star count for migration/cache only; new resolved creatures use native level minus one.</summary>
        public const string Stars = "wildblood.stars";

        /// <summary>The mutation set as a bitmask.</summary>
        public const string Mask = "wildblood.mask";

        /// <summary>The biome the creature was rolled in, so its rules stay fixed as it wanders. Stored as the flag value.</summary>
        public const string Biome = "wildblood.biome";

        /// <summary>World tiers: the tier a wild creature was rolled at, for `wildblood inspect`. Written only when above 0.</summary>
        public const string Tier = "wildblood.tier";

        /// <summary>Breeding: the partner's stars plus one, on the pregnant parent's ZDO from conception to birth; 0 or absent
        /// means no partner was beside it. Plus one so a plain partner still reads as a partner.</summary>
        public const string SireStars = "wildblood.sire_stars";

        /// <summary>Breeding: the partner's mutation set, beside <see cref="SireStars"/>.</summary>
        public const string SireMask = "wildblood.sire_mask";

        /// <summary>Breeding: an egg's inherited traits, in the egg item's own custom data rather than a ZDO, so they travel
        /// with it through inventories and chests. "stars,mask,biome", invariant integers.</summary>
        public const string EggTraits = "wildblood.egg";

        /// <summary>Devouring: accumulated maximum-health eaten, added on top of star scaling.</summary>
        public const string DevouredHealth = "wildblood.dev_hp";

        /// <summary>Devouring: accumulated per-hit damage eaten.</summary>
        public const string DevouredDamage = "wildblood.dev_dmg";

        /// <summary>Splintering: how many generations deep this creature is in its cascade.</summary>
        public const string Generation = "wildblood.gen";

        /// <summary>Splintering: the ZDOID of the cascade's root, as a string, for the live-descendant cap.</summary>
        public const string CascadeRoot = "wildblood.root";

        /// <summary>Miasmic: the owner's rolling record of recent cloud drops, so every client draws the same trail.</summary>
        public const string MiasmaTrail = "wildblood.miasma";

        /// <summary>Miasmic: a monotonic drop counter, so every cloud has a unique id no trim can reissue.</summary>
        public const string MiasmaSeq = "wildblood.miasma_seq";

        /// <summary>Devouring: the shared-clock instant (whole milliseconds) at which a devourer may eat again. Written on
        /// the devourer's ZDO by its owner when it banks a meal; read anywhere to gate its next bite through the cooldown.</summary>
        public const string DevourReadyAt = "wildblood.dev_ready";

        /// <summary>Respawning: the world time (ticks) a dungeon loot chest was last filled, for its regeneration timer.</summary>
        public const string LootFilledAt = "wildblood.loot_at";

        /// <summary>Devouring: which devourer instant-killed THIS prey, as its ZDOID, set on the prey's ZDO at the bite so
        /// it survives a handover. Set only on a genuine devour, so the prey's death path feeds that devourer and no other.</summary>
        public const string DevouredBy = "wildblood.devoured_by";

        /// <summary>Devouring: what this devourer has eaten, oldest first, as the eaten creatures' prefab hashes packed four
        /// bytes each (see <c>Mutations.MealStore</c>). Its length is the meal count the one-per-star limit reads, and every
        /// client draws the eaten creatures on the nameplate from it. Written only by the devourer's owner.</summary>
        public const string DevouredMeals = "wildblood.dev_meals";

        /// <summary>Thieving: the pouch of stolen items, as one packed byte array. Owner-written, everyone-read, so the
        /// nameplate icons and `wildblood inspect` agree with what the creature actually holds on every machine.</summary>
        public const string Pouch = "wildblood.stolen";

        /// <summary>Thieving: pouch entry keys whose death/despawn drop an owner has already decided (Meadworks-original).
        /// A later owner never creates those drops again.</summary>
        public const string PouchDrops = "wildblood.stolen.drops";

        /// <summary>Thieving: the owner's retained v2 steal outcomes, written in the same owner update as the pouch.</summary>
        public const string TheftOutcomes = "wildblood.stolen.outcomes";

        /// <summary>Boss aspects: the boss's rolled aspect, by its enum value. Written only when it is not None.</summary>
        public const string Aspect = "wildblood.aspect";

        /// <summary>Boss aspects: the aspects a boss carries beside its headline one (Bountiful's), one bit per aspect value.
        /// Written only when there are any.</summary>
        public const string AspectExtra = "wildblood.aspect_extra";

        /// <summary>Boss aspects: the extra aspects an altar showing Bountiful will give, beside <see cref="AltarAspect"/>,
        /// one bit per aspect value; 0 or absent for none.</summary>
        public const string AltarExtra = "wildblood.altar_extra";

        /// <summary>Boss aspects: the aspect currently on an altar, on the altar's own ZDO; -1 (absent) until first rolled.</summary>
        public const string AltarAspect = "wildblood.altar_aspect";

        /// <summary>Boss aspects: the world time (whole milliseconds) of an altar's next shift, on the altar's ZDO.</summary>
        public const string AltarShiftAt = "wildblood.altar_shift";

        /// <summary>Boss altars: the star count the boss summoned at an altar will have, on the altar's own ZDO, rolled
        /// again at every shift; -1 (absent) until first rolled - as on an altar rolled before stars came to altars.</summary>
        public const string AltarStars = "wildblood.altar_stars";

        /// <summary>Twin: the partner boss's ZDOID, on each of the two, so either can find the other to share its health.</summary>
        public const string TwinPartner = "wildblood.twin";

        /// <summary>Phantom: on a copy, the ZDOID of the boss that brought it. Its presence is what makes a copy a copy.</summary>
        public const string PhantomOf = "wildblood.phantom_of";

        /// <summary>Phantom: on the boss, how many of its `split at` marks it has split at, so a hand-over repeats none.</summary>
        public const string PhantomSplits = "wildblood.phantom_splits";

        /// <summary>
        /// Phantom: on the boss and on each copy, a random number drawn at each split that orders their health bars in
        /// one row, so the boss's place in the row says nothing about which one it is.
        /// </summary>
        public const string PhantomOrder = "wildblood.phantom_order";

        /// <summary>Summoner: how many waves the boss has called, so a hand-over neither repeats nor skips one.</summary>
        public const string SummonWaves = "wildblood.waves";

        /// <summary>Boss damage board: on a boss, the health each player has taken off it, written by its owner at each
        /// hit so a hand-over mid-fight keeps the count.</summary>
        public const string BossDamage = "wildblood.boss_dmg";

        /// <summary>Adaptive: the damage type the boss resists right now, by its <c>AdaptiveType</c> value (0 or absent for
        /// none). Written by the boss's owner only when it changes, so every client glows the same colour and a new owner
        /// carries on from it.</summary>
        public const string Adapted = "wildblood.adaptive";

        /// <summary>Fixated: on the boss, the marked player's character as a ZDOID (stored under this name plus "_u" and
        /// "_i"), written by the boss's owner so every machine sees the same mark and a new owner keeps it.</summary>
        public const string Fixated = "wildblood.fixated";

        /// <summary>Fixated: when the mark last landed or moved (shared-clock ms), so a client says the chat line only for
        /// a change that just happened, never for a mark it meets later.</summary>
        public const string FixatedAt = "wildblood.fixated_at";

        /// <summary>Blinking: when the last tell went out (shared-clock ms), so a new owner never blinks twice.</summary>
        public const string BlinkedAt = "wildblood.blink_at";

        /// <summary>Stormbound: when the latest storm's lightning falls (shared-clock ms), on the boss. Written by its
        /// owner with the circles, so every client strikes at the same moment, a player arriving mid-storm still sees it,
        /// and a new owner carries on the rhythm instead of calling a second storm at once.</summary>
        public const string StormAt = "wildblood.storm_at";

        /// <summary>Stormbound: the latest storm, packed, on the boss - the radius and damage its owner called it with and
        /// its circle centres, so every client draws and judges with the same numbers; read with <see cref="StormAt"/>.</summary>
        public const string StormCircles = "wildblood.storm";

        /// <summary>Relentless: the ZDOID of the target it has picked, written by its owner and cleared when lost, so a
        /// new owner takes up the same hunt.</summary>
        public const string Quarry = "wildblood.quarry";

        /// <summary>Gravitic: when the boss last roared (shared-clock ms), written by its owner, so a new owner keeps the
        /// rhythm and never roars twice.</summary>
        public const string GravityAt = "wildblood.gravity_at";

        /// <summary>Colossal: on a boss's ragdoll, the size its corpse keeps, written by the ragdoll's owner so every machine
        /// grows its copy to match the boss that fell.</summary>
        public const string CorpseScale = "wildblood.corpse_scale";

        /// <summary>Star look: on a starred creature's ragdoll, its star count, written by the ragdoll's owner so every
        /// machine dresses its copy in the same look as the creature that fell.</summary>
        public const string CorpseStars = "wildblood.corpse_stars";

        /// <summary>Tethered: the partner boss's ZDOID, on each of the two, so either can read the other's health and draw
        /// the tether.</summary>
        public const string TetherPartner = "wildblood.tether";

        /// <summary>Portalbound: when the far portal of the current attack opened (shared-clock ms), on the boss, written by
        /// its owner with <see cref="PortalSpot"/> so every client draws the same portal.</summary>
        public const string PortalAt = "wildblood.portal_at";

        /// <summary>Portalbound: where the far portal of the current attack hangs, on the boss.</summary>
        public const string PortalSpot = "wildblood.portal_spot";

        /// <summary>Screecher: when it last shrieked (shared-clock ms), written by its owner, so a new owner keeps the cooldown.</summary>
        public const string ShriekAt = "wildblood.shriek_at";

        /// <summary>Frostbound: the owner's newest ice-patch drops, packed, so every client lays the same trail.</summary>
        public const string FrostTrail = "wildblood.frost_trail";

        /// <summary>Frostbound: a monotonic ice-patch counter, so every patch has a unique id no trim can reissue.</summary>
        public const string FrostTrailSeq = "wildblood.frost_trail_seq";

        /// <summary>Mudbound: the owner's newest mud-patch drops, packed, so every client lays the same trail.</summary>
        public const string MudTrail = "wildblood.mud_trail";

        /// <summary>Mudbound: a monotonic mud-patch counter, so every patch has a unique id no trim can reissue.</summary>
        public const string MudTrailSeq = "wildblood.mud_trail_seq";

        /// <summary>Flamebound: the owner's newest burning-patch drops, packed, so every client lays the same trail.</summary>
        public const string FireTrail = "wildblood.fire_trail";

        /// <summary>Flamebound: a monotonic burning-patch counter, so every patch has a unique id no trim can reissue.</summary>
        public const string FireTrailSeq = "wildblood.fire_trail_seq";

        /// <summary>Howling: true once it has howled; a creature howls once in its life, whoever owns it.</summary>
        public const string Howled = "wildblood.howled";

        /// <summary>Echoing: on an echo, the ZDOID of the boss it replays. Its presence is what makes an echo an echo.</summary>
        public const string EchoOf = "wildblood.echo_of";

        /// <summary>Cloning: on the creature, its decoy's ZDOID while it hides behind one; absent once it shows again. Written
        /// by its owner; every machine hides or shows the body from it.</summary>
        public const string CloneDecoy = "wildblood.clone_decoy";

        /// <summary>Cloning: on a decoy, the ZDOID of the creature it stands in for. Its presence is what makes a decoy a decoy.</summary>
        public const string CloneOf = "wildblood.clone_of";

        /// <summary>Cloning: on the creature, how many times it has hidden behind a decoy, against its `times`.</summary>
        public const string Clones = "wildblood.clones";

        /// <summary>Cloning: on the creature, when its current trick began (shared-clock ms) while it hides, and when the last
        /// one ended once it shows again: the decoy's life and the cooldown both count from it, whoever owns it.</summary>
        public const string CloneAt = "wildblood.clone_at";

        /// <summary>Nightfall: when the latest wave of tornadoes rose (shared-clock ms), on the boss. Written by its owner
        /// with the wave, so every client raises the same tornadoes at the same moment, a player arriving mid-wave still
        /// sees them, and a new owner keeps the rhythm and carries the hunt on.</summary>
        public const string TornadoAt = "wildblood.tornado_at";

        /// <summary>Nightfall: the latest wave, packed, on the boss - the numbers its owner raised it with, the delay to the
        /// next wave, and each tornado's player and the point it rose at; read with <see cref="TornadoAt"/>.</summary>
        public const string TornadoWave = "wildblood.tornado";

        /// <summary>Nightfall: where the latest wave's tornadoes are and where they are heading, packed, on the boss;
        /// written by its owner a few times a second while they hunt, so every client draws them where the owner moves
        /// them.</summary>
        public const string TornadoTrack = "wildblood.tornado_track";

        /// <summary>Distance Scaling: the health multiplier the matching rule's distanceScaling band gave this creature where its owner
        /// rolled it (Meadworks-original key). Absent means 1. Stored once so wandering never changes a creature's health.</summary>
        public const string DistanceHealth = "wildblood.dist_health";

        /// <summary>Distance Scaling: the outgoing damage multiplier from the same band, beside <see cref="DistanceHealth"/>.</summary>
        public const string DistanceDamage = "wildblood.dist_damage";

        public const string Blob = "wildblood.traits";
        public const string Source = "wildblood.source";
        private const string LegacyImported = "wildblood.imported.ecr_aux";
        public static string Legacy(string key) => key.StartsWith("wildblood.", StringComparison.Ordinal) ? "ecr_" + key.Substring(10) : key;
        internal static bool Imported(ZDO zdo) => zdo.GetBool(LegacyImported);
        internal static int Int(ZDO zdo, string key, int fallback = 0) => zdo.GetInt(key.GetStableHashCode(),
            Imported(zdo) ? fallback : zdo.GetInt(Legacy(key).GetStableHashCode(), fallback));
        internal static float Float(ZDO zdo, string key, float fallback = 0f) => zdo.GetFloat(key.GetStableHashCode(),
            Imported(zdo) ? fallback : zdo.GetFloat(Legacy(key).GetStableHashCode(), fallback));
        internal static long Long(ZDO zdo, string key) => zdo.GetLong(key.GetStableHashCode(),
            Imported(zdo) ? 0L : zdo.GetLong(Legacy(key).GetStableHashCode()));
        internal static ZDOID Id(ZDO zdo, string key) {
            var value = zdo.GetZDOID(ZDO.GetHashZDOID(key));
            return value != ZDOID.None || Imported(zdo) ? value : zdo.GetZDOID(ZDO.GetHashZDOID(Legacy(key)));
        }
        // Explicit source schemas preserve auxiliary state without touching raw dictionaries or deleting legacy keys.
        // Once imported, zero/None values must never resurrect an old source state.
        internal static void MigrateAuxiliary(ZDO zdo) {
            if (!zdo.IsOwner() || Imported(zdo)) return;
            foreach (string key in new[] { Stars, Mask, Biome, Tier, SireStars, SireMask, Generation, MiasmaSeq, Aspect, AspectExtra, AltarExtra, AltarAspect, AltarStars, PhantomSplits, SummonWaves, Adapted, CorpseStars, FrostTrailSeq, MudTrailSeq, Clones }) {
                int current = zdo.GetInt(key.GetStableHashCode(), int.MinValue), previous = zdo.GetInt(Legacy(key).GetStableHashCode(), int.MinValue);
                if (current == int.MinValue && previous != int.MinValue) zdo.Set(key, previous);
            }
            foreach (string key in new[] { DevourReadyAt, LootFilledAt, AltarShiftAt, FixatedAt, BlinkedAt, StormAt, GravityAt, PortalAt, ShriekAt, CloneAt, TornadoAt }) {
                long current = zdo.GetLong(key.GetStableHashCode(), long.MinValue), previous = zdo.GetLong(Legacy(key).GetStableHashCode(), long.MinValue);
                if (current == long.MinValue && previous != long.MinValue) zdo.Set(key, previous);
            }
            foreach (string key in new[] { DevouredHealth, DevouredDamage, PhantomOrder, CorpseScale }) {
                float current = zdo.GetFloat(key.GetStableHashCode(), float.NaN), previous = zdo.GetFloat(Legacy(key).GetStableHashCode(), float.NaN);
                if (float.IsNaN(current) && !float.IsNaN(previous)) zdo.Set(key, previous);
            }
            foreach (string key in new[] { CascadeRoot }) {
                string? current = zdo.GetString(key.GetStableHashCode(), null!), previous = zdo.GetString(Legacy(key).GetStableHashCode(), null!);
                if (current == null && previous != null) zdo.Set(key, previous);
            }
            foreach (string key in new[] { MiasmaTrail, DevouredMeals, Pouch, BossDamage, StormCircles, FrostTrail, MudTrail, TornadoWave, TornadoTrack }) {
                byte[]? current = zdo.GetByteArray(key.GetStableHashCode()), previous = zdo.GetByteArray(Legacy(key).GetStableHashCode());
                if (current == null && previous != null) zdo.Set(key, previous);
            }
            foreach (string key in new[] { DevouredBy, TwinPartner, PhantomOf, Fixated, Quarry, TetherPartner, CloneDecoy, CloneOf }) {
                ZDOID previous = zdo.GetZDOID(ZDO.GetHashZDOID(Legacy(key)));
                if (zdo.GetZDOID(ZDO.GetHashZDOID(key)) == ZDOID.None && previous != ZDOID.None) zdo.Set(key, previous);
            }
            var absent = new Vector3(float.NaN, float.NaN, float.NaN);
            Vector3 currentSpot = zdo.GetVec3(PortalSpot.GetStableHashCode(), absent), previousSpot = zdo.GetVec3(Legacy(PortalSpot).GetStableHashCode(), absent);
            if (float.IsNaN(currentSpot.x) && !float.IsNaN(previousSpot.x)) zdo.Set(PortalSpot, previousSpot);
            zdo.Set(LegacyImported, true);
        }
    }
}
