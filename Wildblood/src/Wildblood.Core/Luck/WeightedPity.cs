// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;

namespace ExtraPotions.Wildblood.Luck
{
    public readonly struct PitySource
    {
        public PitySource(string prefab, float chance, int minimum, int maximum, bool levelMultiplier)
        { Prefab = prefab; Chance = chance; Minimum = minimum; Maximum = maximum; LevelMultiplier = levelMultiplier; }

        public string Prefab { get; }
        public float Chance { get; }
        public int Minimum { get; }
        public int Maximum { get; }
        public bool LevelMultiplier { get; }
        public bool Eligible => WeightedPity.Eligible(Chance, Minimum, Maximum);
    }

    public readonly struct PityProgress
    {
        public PityProgress(int remaining, bool guarantee)
        { Remaining = remaining; Guarantee = guarantee; }

        public int Remaining { get; }
        public bool Guarantee { get; }
    }

    public static class WeightedPity
    {
        public const int MaximumStars = 8;

        public static bool Eligible(float chance, int minimum, int maximum) =>
            chance > 0f && chance <= .3f && minimum == 1 && maximum == 1;

        public static bool TryWeight(int nativeLevel, bool levelMultiplier, out int weight)
        {
            weight = 1;
            if (nativeLevel < 1 || nativeLevel > MaximumStars + 1) return false;
            if (levelMultiplier) weight = 1 << (nativeLevel - 1);
            return true;
        }

        public static PityProgress Advance(int remaining, int weight, bool dropped, int reset)
        {
            if (remaining < 0 || weight < 1 || reset < 0) throw new ArgumentOutOfRangeException();
            if (dropped) return new PityProgress(reset, false);
            bool guarantee = remaining <= weight;
            return new PityProgress(guarantee ? reset : remaining - weight, guarantee);
        }

        public static IReadOnlyList<LuckDelta> ReconcileChain(IReadOnlyList<LuckDelta> deltas, LuckTable final)
        {
            var reset = new HashSet<(string Prefab, float Chance)>(deltas.Where(delta => delta.Weighted && delta.NewCounter >= 0)
                .Select(delta => (delta.Prefab, delta.Chance)));
            return deltas.Select(delta =>
            {
                if (!delta.Weighted || !reset.Contains((delta.Prefab, delta.Chance))) return delta;
                if (!final.TryGet(delta.Prefab, delta.Chance, out var row) || row.Counter < 0 ||
                    row.Counter > NativeLuckCounters.MaximumReset(delta.Chance))
                    throw new InvalidOperationException("Weighted chain reset has no bounded final counter");
                // Absolute updates for this bucket all converge even when death claims arrive in a different order.
                return new LuckDelta(delta.Prefab, delta.Chance, 0, row.Counter, true);
            }).ToList();
        }

        // Only a complete source catalog can distinguish a star alias from an independent chance for the same item.
        public static bool TryMerge(LuckTable legacy, IEnumerable<PitySource> sources, string prefab, float baseChance, out int? remaining)
        {
            remaining = null;
            var catalog = sources.Where(source => source.Prefab == prefab).ToList();
            if (!catalog.Any(source => source.Chance == baseChance && source.Eligible)) return false;
            foreach (var row in legacy.Entries.Where(row => row.Prefab == prefab))
            {
                int aliasWeight = 0;
                foreach (var target in catalog.Where(source => source.Chance == baseChance && source.Eligible))
                    for (int weight = 1; weight <= (target.LevelMultiplier ? 1 << MaximumStars : 1); weight *= 2)
                    {
                        float chance = baseChance * weight;
                        if (chance > .3f) break;
                        if (NativeLuckCounters.Bucket(0, chance) != NativeLuckCounters.Bucket(0, row.Chance)) continue;
                        if (chance != row.Chance || row.Counter < 0) return false;
                        aliasWeight = weight;
                    }
                if (aliasWeight == 0) continue;
                foreach (var source in catalog)
                    for (int weight = 1; weight <= (source.LevelMultiplier ? 1 << MaximumStars : 1); weight *= 2)
                    {
                        float chance = source.Chance * weight;
                        if (!(chance > 0f && chance <= .3f)) break;
                        if (NativeLuckCounters.Bucket(0, chance) == NativeLuckCounters.Bucket(0, row.Chance) &&
                            (!source.Eligible || source.Chance != baseChance || chance != row.Chance)) return false;
                    }
                int converted = (int)Math.Min(int.MaxValue, (long)row.Counter * aliasWeight);
                remaining = remaining.HasValue ? Math.Min(remaining.Value, converted) : converted;
            }
            return true;
        }

        public static bool TryStore(LuckTable table, IEnumerable<PitySource> sources, string prefab, float baseChance, int remaining)
        {
            if (remaining < 0) return false;
            var catalog = sources.ToList();
            if (!TryMerge(table, catalog, prefab, baseChance, out _)) return false;
            bool scaled = catalog.Any(source => source.Prefab == prefab && source.Chance == baseChance && source.Eligible && source.LevelMultiplier);
            foreach (var row in table.Entries.Where(row => row.Prefab == prefab).ToList())
                for (int weight = 1; weight <= (scaled ? 1 << MaximumStars : 1); weight *= 2)
                    if (row.Chance > 0f && row.Chance <= .3f && row.Chance == baseChance * weight)
                    {
                        // Keep legacy aliases consistent so reloading cannot reapply their earlier, better countdown.
                        int count = (int)(((long)remaining + weight - 1) / weight);
                        table.Set(prefab, row.Chance, count);
                        break;
                    }
            table.Set(prefab, baseChance, remaining);
            return true;
        }
    }
}
