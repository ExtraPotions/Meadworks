// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System;
using System.Collections.Generic;
using ExtraPotions.Inscribe.Adopted.Core;
using ExtraPotions.Inscribe.Adopted.Items;
using ExtraPotions.Inscribe.Adopted.Rules;

namespace ExtraPotions.Inscribe.Adopted.Loot
{
    /// <summary>
    /// The pre-rolled gear pool per drop tier 1-8 (drops.md sections 8 and 13, classes-and-tiers.md section 10): bases
    /// at the drop tier weigh <c>same_tier_weight</c>, bases up to <c>tiers_below</c> below it weigh
    /// <c>lower_tier_weight</c>, both times the class's <c>drop_weight</c>. Out of date when the rules generation, the
    /// class registry, the object database instance, its item count or its recipe count changes (other mods add items
    /// and recipes late, <see cref="GearPoolKey"/>). <see cref="GearPoolWarmup"/> builds it again a slice a frame
    /// shortly after such a change, so a kill finds it ready and costs five comparisons; a draw that comes first (in
    /// the first seconds after a world load or a rules change) still builds it at once. Main thread only.
    /// </summary>
    public static class GearPool
    {
        private static WeightedTable<GearBase>[] _tables = Array.Empty<WeightedTable<GearBase>>();
        private static IReadOnlyList<GearBase> _bases = Array.Empty<GearBase>();
        private static GearPoolKey? _key;

        /// <summary>Every drop-eligible base of the current build (for the reference command).</summary>
        public static IReadOnlyList<GearBase> Bases
        {
            get
            {
                EnsureFresh();
                return _bases;
            }
        }

        /// <summary>The weighted base draw for a drop tier; empty before the object database exists.</summary>
        public static WeightedTable<GearBase> ForTier(int tier)
        {
            EnsureFresh();
            return tier >= 1 && tier <= _tables.Length ? _tables[tier - 1] : WeightedTable<GearBase>.Empty;
        }

        internal static void Invalidate() => _key = null;

        /// <summary>Whether the pool in use was built from exactly these inputs.</summary>
        internal static bool IsFresh(GearPoolKey key) => _key.HasValue && _key.Value.Equals(key);

        /// <summary>Takes a finished collection as the pool (the warm-up's last slice, or a draw that came first).</summary>
        internal static void Adopt(GearPoolKey key, IReadOnlyList<GearBase> bases, RuleSet rules)
        {
            _key = key;
            _bases = bases;
            _tables = new WeightedTable<GearBase>[DropParser.Tiers];
            for (int tier = 1; tier <= DropParser.Tiers; tier++)
            {
                _tables[tier - 1] = WeightedTable<GearBase>.Build(Weights(_bases, tier, rules.Economy.Drops.Gear));
            }
            if (key.Db != null)
            {
                Log.Info($"gear drop pool: {_bases.Count} bases; per tier {Sizes()}");
            }
        }

        private static void EnsureFresh()
        {
            GearPoolKey key = GearPoolKey.Now();
            if (IsFresh(key))
            {
                return;
            }
            RuleSet rules = ActiveRules.Current;
            ObjectDB? db = key.Db;
            Adopt(key, db?.m_items == null ? Array.Empty<GearBase>() : (IReadOnlyList<GearBase>)GearBases.Collect(db, rules), rules);
        }

        private static IEnumerable<KeyValuePair<GearBase, float>> Weights(IReadOnlyList<GearBase> bases, int tier, GearDropRules gear)
        {
            foreach (GearBase b in bases)
            {
                int below = tier - b.PoolTier;
                float weight = below == 0 ? gear.SameTierWeight : below > 0 && below <= gear.TiersBelow ? gear.LowerTierWeight : 0f;
                yield return new KeyValuePair<GearBase, float>(b, weight * (b.Class.Class?.DropWeight ?? 0f));
            }
        }

        private static string Sizes()
        {
            string[] parts = new string[_tables.Length];
            for (int i = 0; i < _tables.Length; i++)
            {
                parts[i] = $"T{i + 1} {_tables[i].Count}";
            }
            return string.Join(", ", parts);
        }
    }

    /// <summary>
    /// What the gear pool is built from: the object database instance, its item and recipe counts, the rules
    /// generation and the class registry version. Any part changing makes the pool out of date.
    /// </summary>
    internal readonly struct GearPoolKey : IEquatable<GearPoolKey>
    {
        private GearPoolKey(ObjectDB? db, int generation, int classes)
        {
            Db = db;
            Generation = generation;
            Classes = classes;
            Items = db?.m_items?.Count ?? -1;
            Recipes = db?.m_recipes?.Count ?? -1;
        }

        public ObjectDB? Db { get; }
        public int Generation { get; }
        public int Classes { get; }
        public int Items { get; }
        public int Recipes { get; }

        public static GearPoolKey Now() => new GearPoolKey(ObjectDB.instance, ActiveRules.Generation, ItemClasses.Version);

        public bool Equals(GearPoolKey other) => ReferenceEquals(Db, other.Db) && Generation == other.Generation
            && Classes == other.Classes && Items == other.Items && Recipes == other.Recipes;

        public override bool Equals(object? obj) => obj is GearPoolKey other && Equals(other);

        public override int GetHashCode() => (Generation * 397) ^ (Classes * 31) ^ Items ^ (Recipes << 12);
    }
}
