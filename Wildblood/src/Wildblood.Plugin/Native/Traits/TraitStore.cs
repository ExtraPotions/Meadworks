// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System.Collections.Generic;

namespace ExtraPotions.Wildblood.Adopted.Traits
{
    /// <summary>
    /// Moves <see cref="CreatureTraits"/> to and from a creature's ZDO. This is the single seam between the resolved
    /// traits and their persisted form; nothing else touches the trait ZDO keys. Reads work on any client (the ZDO
    /// is shared); writes only take on the owner, which is where resolution happens.
    /// </summary>
    public static class TraitStore
    {
        private const string MissingBlob = "\u0000";
        private const string NativeLevelKeptKey = "wildblood.cm.native_level_kept";
        private static readonly string[] LegacyLevelKeys = {
            "wildblood.cm.LevelApplied", "wildblood.cm.LevelHealthApplied", "wildblood.cm.LevelHealthMultiplier",
            "wildblood.cm.LevelDamageApplied", "wildblood.cm.LevelDamageMultiplier", "wildblood.cm.LevelDamageReplacesVanilla"
        };

        /// <summary>A migrated source level remains the game's current native value, never its stale DesiredLevel cache.</summary>
        internal static bool KeepsNativeLevel(ZDO zdo) => zdo != null && zdo.GetBool(NativeLevelKeptKey,
            zdo.GetBool("wildblood.cm.LevelApplied", zdo.GetBool("CreatureManager_LevelApplied", false)));

        /// <summary>Imports existing level metadata without rolling or writing the creature's native level.</summary>
        internal static void MigrateNativeLevel(ZDO zdo)
        {
            if (!CanUpdateModifiers(zdo)) return;
            ExtraPotions.Wildblood.AdoptedCm.CreatureModifierManager.MigrateOwnedKeys(zdo, LegacyLevelKeys, "wildblood.cm.level_imported");
            if (!zdo.GetBool("wildblood.cm.LevelApplied", false)) return;
            zdo.Set(NativeLevelKeptKey, true);
            if (!IsResolved(zdo))
            {
                var saved = ReadSet(zdo);
                Save(zdo, new CreatureTraits(System.Math.Max(0, zdo.GetInt(ZDOVars.s_level, 1) - 1), saved.Mutations));
                SetBiome(zdo, Heightmap.FindBiome(zdo.GetPosition()));
            }
        }
        public static bool IsResolved(ZDO zdo) => zdo != null && (zdo.GetBool(TraitKeys.Resolved) || zdo.GetBool(TraitKeys.Legacy(TraitKeys.Resolved)));

        /// <summary>Blob state is independent from the source's numeric cache; reads never mutate a peer's ZDO.</summary>
        public static ExtraPotions.Wildblood.Traits.TraitSet ReadSet(ZDO zdo)
        {
            string blob = zdo.GetString(TraitKeys.Blob, MissingBlob);
            if (blob != MissingBlob && ExtraPotions.Wildblood.Traits.TraitBlob.TryDecode(blob, out var saved)) return saved;
            return new ExtraPotions.Wildblood.Traits.TraitSet(TraitKeys.Int(zdo, TraitKeys.Mask),
                System.Array.Empty<ExtraPotions.Wildblood.Traits.Modifier>(), PortableBiome(GetBiome(zdo)));
        }

        public static CreatureTraits Load(ZDO zdo)
        {
            TraitKeys.MigrateAuxiliary(zdo);
            var saved = ReadSet(zdo);
            // Until the owner migrates native level and calls Save, original ecr_stars remains authoritative.
            int stars = zdo.GetBool(TraitKeys.Resolved) || KeepsNativeLevel(zdo) ? System.Math.Max(0, zdo.GetInt(ZDOVars.s_level, 1) - 1)
                : TraitKeys.Int(zdo, TraitKeys.Stars);
            return new CreatureTraits(stars, saved.Mutations)
            {
                Aspect = (Aspect)TraitKeys.Int(zdo, TraitKeys.Aspect),
                ExtraAspects = TraitKeys.Int(zdo, TraitKeys.AspectExtra),
                PhantomCopy = TraitKeys.Id(zdo, TraitKeys.PhantomOf) != ZDOID.None,
                Decoy = TraitKeys.Id(zdo, TraitKeys.CloneOf) != ZDOID.None,
                Echo = TraitKeys.Id(zdo, TraitKeys.EchoOf) != ZDOID.None,
                Tier = TraitKeys.Int(zdo, TraitKeys.Tier),
            };
        }

        public static void Save(ZDO zdo, CreatureTraits traits)
        {
            if (zdo == null || !zdo.IsOwner()) return;
            TraitKeys.MigrateAuxiliary(zdo);
            // Preserve opaque future ids/fields as well as unknown codec versions during a downgrade.
            if (!CanUpdateModifiers(zdo)) return;
            var saved = ReadSet(zdo);
            var updated = new ExtraPotions.Wildblood.Traits.TraitSet(traits.Mask, saved.Modifiers, PortableBiome(GetBiome(zdo)));
            zdo.Set(TraitKeys.Blob, ExtraPotions.Wildblood.Traits.TraitBlob.Encode(updated));
            zdo.Set(TraitKeys.Stars, traits.Stars);
            zdo.Set(TraitKeys.Mask, updated.Mutations);
            zdo.Set(TraitKeys.Aspect, (int)traits.Aspect);
            zdo.Set(TraitKeys.AspectExtra, traits.ExtraAspects);
            zdo.Set(TraitKeys.Tier, traits.Tier);
            zdo.Set(TraitKeys.Resolved, true);
        }

        /// <summary>Unknown future state remains byte-for-byte intact; lenient decoding alone is not permission to rewrite it.</summary>
        internal static bool CanUpdateModifiers(ZDO zdo)
        {
            if (zdo == null || !zdo.IsOwner()) return false;
            string current = zdo.GetString(TraitKeys.Blob, MissingBlob);
            return current == MissingBlob || CanRewriteBlob(current);
        }

        private static bool CanRewriteBlob(string current)
        {
            if (!ExtraPotions.Wildblood.Traits.TraitBlob.TryDecode(current, out var decoded)) return false;
            if (current.StartsWith(ExtraPotions.Wildblood.Traits.TraitBlob.LegacyPrefix, System.StringComparison.Ordinal)) return true;
            try { return ExtraPotions.Wildblood.Traits.TraitBlob.Encode(decoded) == current; }
            catch (System.ArgumentException) { return false; }
        }

        /// <summary>Updates the canonical modifier view without changing the native level or spawn source.</summary>
        internal static bool TryUpdateModifiers(ZDO zdo,
            IEnumerable<ExtraPotions.Wildblood.Traits.Modifier> modifiers, int additionalMutations = 0)
        {
            if (!CanUpdateModifiers(zdo)) return false;
            var saved = ReadSet(zdo);
            var updated = new ExtraPotions.Wildblood.Traits.TraitSet(saved.Mutations | additionalMutations, modifiers, saved.Biome);
            string blob;
            try { blob = ExtraPotions.Wildblood.Traits.TraitBlob.Encode(updated); }
            catch (System.ArgumentException) { return false; }
            TraitKeys.MigrateAuxiliary(zdo);
            zdo.Set(TraitKeys.Blob, blob);
            zdo.Set(TraitKeys.Mask, updated.Mutations);
            return true;
        }

        /// <summary>Used by the traits capability after Shepherd has supplied the newborn's native level.</summary>
        public static bool ApplyBlob(ZDO zdo, string blob)
        {
            if (zdo == null || !zdo.IsOwner() || !ExtraPotions.Wildblood.Traits.TraitBlob.TryDecode(blob, out var saved)) return false;
            if (!CanUpdateModifiers(zdo) || !CanRewriteBlob(blob)) return false;
            TraitKeys.MigrateAuxiliary(zdo);
            zdo.Set(TraitKeys.Blob, ExtraPotions.Wildblood.Traits.TraitBlob.Encode(saved));
            zdo.Set(TraitKeys.Mask, saved.Mutations);
            zdo.Set(TraitKeys.Stars, System.Math.Max(0, zdo.GetInt(ZDOVars.s_level, 1) - 1));
            Heightmap.Biome biome = saved.Biome == ExtraPotions.Wildblood.Stars.Biome.Other
                ? Heightmap.FindBiome(zdo.GetPosition()) : NativeBiome(saved.Biome);
            zdo.Set(TraitKeys.Biome, (int)biome);
            zdo.Set(TraitKeys.Source, (int)ExtraPotions.Wildblood.Stars.SpawnSource.Breeding);
            zdo.Set(TraitKeys.Resolved, true);
            return true;
        }

        internal static ExtraPotions.Wildblood.Stars.Biome PortableBiome(Heightmap.Biome biome) =>
            ExtraPotions.Wildblood.Traits.TraitBlob.ParseBiome(biome.ToString());
        internal static Heightmap.Biome NativeBiome(ExtraPotions.Wildblood.Stars.Biome biome) =>
            System.Enum.TryParse(biome.ToString(), true, out Heightmap.Biome native) ? native : Heightmap.Biome.None;

        /// <summary>Breeding, at conception, on the pregnant parent's owner: remember the partner's traits (or that there
        /// was none) until the birth, since the partner may have wandered off by then.</summary>
        public static void SetSire(ZDO zdo, CreatureTraits? sire)
        {
            if (zdo == null || !zdo.IsOwner()) return;
            TraitKeys.MigrateAuxiliary(zdo);
            zdo.Set(TraitKeys.SireStars, sire != null ? sire.Stars + 1 : 0);
            zdo.Set(TraitKeys.SireMask, sire?.Mask ?? 0);
        }

        /// <summary>Breeding, at birth: the partner remembered at conception, cleared so the next pregnancy starts fresh.
        /// Null when there was no partner, or the pregnancy began before this was recorded.</summary>
        public static CreatureTraits? TakeSire(ZDO zdo)
        {
            int stars = TraitKeys.Int(zdo, TraitKeys.SireStars);
            if (stars <= 0)
            {
                return null;
            }
            CreatureTraits sire = new CreatureTraits(stars - 1, TraitKeys.Int(zdo, TraitKeys.SireMask));
            SetSire(zdo, null);
            return sire;
        }

        public static void SetBiome(ZDO zdo, Heightmap.Biome biome)
        {
            if (!CanUpdateModifiers(zdo)) return;
            TraitKeys.MigrateAuxiliary(zdo);
            string current = zdo.GetString(TraitKeys.Blob, MissingBlob);
            if (current != MissingBlob && ExtraPotions.Wildblood.Traits.TraitBlob.TryDecode(current, out var saved))
                zdo.Set(TraitKeys.Blob, ExtraPotions.Wildblood.Traits.TraitBlob.Encode(new ExtraPotions.Wildblood.Traits.TraitSet(saved.Mutations, saved.Modifiers, PortableBiome(biome))));
            zdo.Set(TraitKeys.Biome, (int)biome);
        }

        public static Heightmap.Biome GetBiome(ZDO zdo)
        {
            string blob = zdo.GetString(TraitKeys.Blob, "");
            if (blob.Length != 0 && ExtraPotions.Wildblood.Traits.TraitBlob.TryDecode(blob, out var saved)) return NativeBiome(saved.Biome);
            return (Heightmap.Biome)TraitKeys.Int(zdo, TraitKeys.Biome, (int)Heightmap.Biome.None);
        }

        /// <summary>Owner-only: remembers the distance-scaling multipliers chosen at the roll (see Distance Scaling).</summary>
        public static void SetDistanceScaling(ZDO zdo, float damage, float health)
        {
            if (zdo == null || !zdo.IsOwner()) return;
            zdo.Set(TraitKeys.DistanceDamage, damage);
            zdo.Set(TraitKeys.DistanceHealth, health);
        }

        public static float GetDistanceHealth(ZDO zdo) => DistanceFactor(zdo, TraitKeys.DistanceHealth);

        public static float GetDistanceDamage(ZDO zdo) => DistanceFactor(zdo, TraitKeys.DistanceDamage);

        // Absent, non-finite or non-positive reads as 1: a corrupted value never zeroes a creature's health or damage.
        private static float DistanceFactor(ZDO zdo, string key)
        {
            if (zdo == null) return 1f;
            float value = zdo.GetFloat(key, 1f);
            return float.IsNaN(value) || float.IsInfinity(value) || value <= 0f ? 1f : value;
        }

        public static float GetDevouredHealth(ZDO zdo) => TraitKeys.Float(zdo, TraitKeys.DevouredHealth);

        public static float GetDevouredDamage(ZDO zdo) => TraitKeys.Float(zdo, TraitKeys.DevouredDamage);

        public static void AddDevoured(ZDO zdo, float health, float damage)
        {
            if (zdo == null || !zdo.IsOwner()) return;
            TraitKeys.MigrateAuxiliary(zdo);
            zdo.Set(TraitKeys.DevouredHealth, GetDevouredHealth(zdo) + health);
            zdo.Set(TraitKeys.DevouredDamage, GetDevouredDamage(zdo) + damage);
        }

        /// <summary>Which devourer instant-killed this prey; <see cref="ZDOID.None"/> when nothing devoured it. On the PREY's ZDO.</summary>
        public static ZDOID GetDevouredBy(ZDO zdo) => TraitKeys.Id(zdo, TraitKeys.DevouredBy);

        /// <summary>Marks this prey as devoured by the given creature. Runs on the prey's owner (RPC_Damage), set at the bite
        /// so the death path feeds exactly that devourer even after the prey changes hands.</summary>
        public static void MarkDevouredBy(ZDO zdo, ZDOID devourer) { if (zdo != null && zdo.IsOwner()) { TraitKeys.MigrateAuxiliary(zdo); zdo.Set(TraitKeys.DevouredBy, devourer); } }

        /// <summary>The shared-clock instant a devourer may eat again, in whole milliseconds (0 when it never has). On the DEVOURER's ZDO.</summary>
        public static long GetDevourReadyAt(ZDO zdo) => TraitKeys.Long(zdo, TraitKeys.DevourReadyAt);

        /// <summary>Owner-only write, at the moment a meal is banked: the cooldown starts here and every client reads it.</summary>
        public static void SetDevourReadyAt(ZDO zdo, long whenMs) { if (zdo != null && zdo.IsOwner()) { TraitKeys.MigrateAuxiliary(zdo); zdo.Set(TraitKeys.DevourReadyAt, whenMs); } }
    }
}
