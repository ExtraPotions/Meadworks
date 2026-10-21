// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.10.0 at d3b57606cb24775d61b7bda244b22eb89e7e7717.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System;
using System.Collections.Generic;
using ExtraPotions.Inscribe.Adopted.Items;

namespace ExtraPotions.Inscribe.Adopted.Rules
{
    /// <summary>
    /// The economy family (<c>ExtraPotions.Inscribe.Adopted_economy*.yml</c>) after merging and validation, with lookups and the
    /// per-tier drop draw tables precomputed at load. Immutable.
    /// </summary>
    public sealed class EconomyRules
    {
        public static readonly EconomyRules Empty = new EconomyRules();

        /// <summary>In ladder order, lowest first; index 0 is the base rarity.</summary>
        public IReadOnlyList<RarityDef> Rarities { get; internal set; } = Array.Empty<RarityDef>();

        public RollingSettings Rolling { get; internal set; } = new RollingSettings();
        public IReadOnlyList<StoneDef> Stones { get; internal set; } = Array.Empty<StoneDef>();

        /// <summary>The YAML's item classes in classification order (read through <c>Items.ItemClasses</c>, which adds registered ones).</summary>
        public IReadOnlyList<ItemClass> Classes { get; internal set; } = Array.Empty<ItemClass>();

        public ItemTierMaps ItemTiers { get; internal set; } = new ItemTierMaps();

        /// <summary>biome id (<c>meadows</c>, <c>black_forest</c>, ..., <c>ocean</c>, <c>deep_north</c>) → tier.</summary>
        public IReadOnlyDictionary<string, int> Biomes { get; internal set; } = new Dictionary<string, int>();

        public DropRules Drops { get; internal set; } = new DropRules();

        // lookups, precomputed at load
        public IReadOnlyDictionary<string, RarityDef> RarityById { get; internal set; } = new Dictionary<string, RarityDef>();
        public IReadOnlyDictionary<string, StoneDef> StoneById { get; internal set; } = new Dictionary<string, StoneDef>();
        public IReadOnlyDictionary<string, StoneDef> StoneByPrefab { get; internal set; } = new Dictionary<string, StoneDef>();

        internal WeightedTable<StoneDef>[] StoneTables { get; set; } = Array.Empty<WeightedTable<StoneDef>>();
        internal WeightedTable<StoneDef>[] RuneOnlyTables { get; set; } = Array.Empty<WeightedTable<StoneDef>>();
        internal WeightedTable<RarityDef>[] GearTables { get; set; } = Array.Empty<WeightedTable<RarityDef>>();
        internal WeightedTable<RarityDef>[] BossGearTables { get; set; } = Array.Empty<WeightedTable<RarityDef>>();

        public RarityDef? BaseRarity => Rarities.Count > 0 ? Rarities[0] : null;

        public RarityDef? Rarity(string? id) => id != null && RarityById.TryGetValue(id, out RarityDef r) ? r : null;

        public StoneDef? Stone(string? id) => id != null && StoneById.TryGetValue(id, out StoneDef s) ? s : null;

        public StoneDef? StoneForPrefab(string? prefab) =>
            prefab != null && StoneByPrefab.TryGetValue(prefab, out StoneDef s) ? s : null;

        public RarityDef? Next(RarityDef rarity) => rarity.Index + 1 < Rarities.Count ? Rarities[rarity.Index + 1] : null;

        public RarityDef? Previous(RarityDef rarity) => rarity.Index > 0 ? Rarities[rarity.Index - 1] : null;

        /// <summary>
        /// Which stone drops at a tier (1-8): enabled stones only, weights from <c>drops.runes</c>. Without
        /// <paramref name="sockets"/> (<c>Gems and sockets</c> off) the chisel's share goes to the runes.
        /// </summary>
        public WeightedTable<StoneDef> StoneDraw(int tier, bool sockets) => ByTier(sockets ? StoneTables : RuneOnlyTables, tier);

        /// <summary>The rarity of dropped gear at a tier, <c>drop_weight</c> applied; the boss table when asked.</summary>
        public WeightedTable<RarityDef> GearRarityDraw(int tier, bool boss) => ByTier(boss ? BossGearTables : GearTables, tier);

        /// <summary>The tier of a biome id, or 0 when unmapped.</summary>
        public int BiomeTier(string biomeId) => Biomes.TryGetValue(biomeId, out int tier) ? tier : 0;

        private static WeightedTable<T> ByTier<T>(WeightedTable<T>[] tables, int tier) =>
            tier >= 1 && tier <= tables.Length ? tables[tier - 1] : WeightedTable<T>.Empty;
    }
}
