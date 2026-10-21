// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System;
using System.Collections.Generic;
using ExtraPotions.Inscribe.Adopted.Rules;

namespace ExtraPotions.Inscribe.Adopted.Items
{
    /// <summary>
    /// The item level of an item type, 1 Meadows to 8 Deep North (classes-and-tiers.md section 2, item-tier.md for the
    /// derivation; the code keeps the older word "tier"): the explicit YAML item map, else a level a mod set through the
    /// API (<see cref="CodeLevels"/>), else the highest tier among its recipe's craft materials (crafted intermediates followed
    /// <c>material_depth</c> deep, cycle-safe; the lowest of several recipes), else its crafting station (with optional
    /// per-level refinement), else <c>fallback_tier</c>. Computed per prefab name on first use and cached; the cache
    /// is dropped on every rules change and whenever the object database's recipes change.
    /// </summary>
    public static class ItemTier
    {
        private static readonly Dictionary<string, TierResult> Cache = new Dictionary<string, TierResult>(StringComparer.Ordinal);

        static ItemTier()
        {
            ActiveRules.RulesChanged += Cache.Clear;
        }

        /// <summary>The item level of an item (by its prefab), 1-8.</summary>
        public static int Of(ItemDrop.ItemData? item) => Explain(PrefabName(item)).Tier;

        /// <summary>The item level of an item prefab name, 1-8.</summary>
        public static int Of(string prefabName) => Explain(prefabName).Tier;

        /// <summary>The tier and the step that decided it (<c>override</c>, <c>api</c>, <c>material: Bronze</c>, <c>station: forge</c>, <c>fallback</c>).</summary>
        public static TierResult Explain(string? prefabName)
        {
            RefreshRecipes();
            if (string.IsNullOrEmpty(prefabName))
            {
                return new TierResult(ActiveRules.Current.Economy.ItemTiers.FallbackTier, "fallback");
            }
            if (!Cache.TryGetValue(prefabName!, out TierResult result))
            {
                result = TierDerivation.Derive(prefabName!, ActiveRules.Current.Economy.ItemTiers);
                Cache[prefabName!] = result;
            }
            return result;
        }

        /// <summary>
        /// Brings the recipe index up to date with the object database (other mods add recipes late) and drops the tier
        /// cache when it was rebuilt. True when it was rebuilt. Every tier lookup does this itself; commands call it
        /// before reading recipes directly.
        /// </summary>
        public static bool RefreshRecipes()
        {
            if (!RecipeIndex.Refresh())
            {
                return false;
            }
            Cache.Clear();
            return true;
        }

        /// <summary>Drops every cached level (an API level was set: <see cref="CodeLevels"/>).</summary>
        internal static void Forget() => Cache.Clear();

        /// <summary>Whether an enabled recipe crafts this prefab (index refreshed first).</summary>
        public static bool HasRecipe(string prefabName)
        {
            RefreshRecipes();
            return RecipeIndex.HasRecipe(prefabName);
        }

        /// <summary>The prefab name of an item: its drop prefab, else the database's prefab for its shared data.</summary>
        public static string? PrefabName(ItemDrop.ItemData? item)
        {
            if (item == null)
            {
                return null;
            }
            if (item.m_dropPrefab != null)
            {
                return item.m_dropPrefab.name;
            }
            return ObjectDB.instance?.GetItemPrefab(item.m_shared)?.name;
        }
    }

    /// <summary>A derived item level and why.</summary>
    public readonly struct TierResult
    {
        /// <summary>The highest item level (8, Deep North).</summary>
        public const int MaxLevel = 8;

        public TierResult(int tier, string source)
        {
            Tier = Math.Max(1, Math.Min(MaxLevel, tier));
            Source = source;
        }

        /// <summary>The item level, 1-8.</summary>
        public int Tier { get; }
        public string Source { get; }
    }

    /// <summary>The four steps of item-tier.md section 3.</summary>
    internal static class TierDerivation
    {
        public static TierResult Derive(string prefab, ItemTierMaps maps)
        {
            if (maps.Items.TryGetValue(prefab, out int explicitTier))
            {
                return new TierResult(explicitTier, "override");
            }
            if (CodeLevels.TryGet(prefab, out int codeTier))
            {
                return new TierResult(codeTier, "api");
            }
            string material = "";
            int tier = RecipeTier(prefab, maps, maps.MaterialDepth, new HashSet<string>(), ref material);
            if (tier > 0)
            {
                return new TierResult(tier, "material: " + material);
            }
            string station = "";
            tier = StationTier(prefab, maps, ref station);
            return tier > 0 ? new TierResult(tier, "station: " + station) : new TierResult(maps.FallbackTier, "fallback");
        }

        // Lowest recipe tier across recipes; a recipe's tier is its highest known craft material. 0 = none resolved.
        private static int RecipeTier(string prefab, ItemTierMaps maps, int depth, HashSet<string> visiting, ref string material)
        {
            if (!visiting.Add(prefab))
            {
                return 0;
            }
            int best = 0;
            foreach (Recipe recipe in RecipeIndex.For(prefab))
            {
                string defining = "";
                int tier = MaterialsTier(recipe, maps, depth, visiting, ref defining);
                if (tier > 0 && (best == 0 || tier < best))
                {
                    best = tier;
                    material = defining;
                }
            }
            visiting.Remove(prefab);
            return best;
        }

        private static int MaterialsTier(Recipe recipe, ItemTierMaps maps, int depth, HashSet<string> visiting, ref string defining)
        {
            int highest = 0;
            foreach (Piece.Requirement req in recipe.m_resources ?? Array.Empty<Piece.Requirement>())
            {
                if (req?.m_resItem == null || req.m_amount <= 0)
                {
                    continue;   // upgrade-only resources do not decide the tier
                }
                string name = req.m_resItem.gameObject.name;
                int tier = MaterialTier(name, maps, depth, visiting);
                if (tier > highest)
                {
                    highest = tier;
                    defining = name;
                }
            }
            return highest;
        }

        private static int MaterialTier(string name, ItemTierMaps maps, int depth, HashSet<string> visiting)
        {
            if (maps.Materials.TryGetValue(name, out int tier))
            {
                return tier;
            }
            string ignored = "";
            return depth > 0 ? RecipeTier(name, maps, depth - 1, visiting, ref ignored) : 0;
        }

        private static int StationTier(string prefab, ItemTierMaps maps, ref string station)
        {
            int best = 0;
            foreach (Recipe recipe in RecipeIndex.For(prefab))
            {
                string? name = recipe.m_craftingStation?.gameObject.name;
                int tier = name == null ? 0 : StationRecipeTier(name, recipe.m_minStationLevel, maps);
                if (tier > 0 && (best == 0 || tier < best))
                {
                    best = tier;
                    station = name!;
                }
            }
            return best;
        }

        private static int StationRecipeTier(string station, int minLevel, ItemTierMaps maps)
        {
            int tier = maps.Stations.TryGetValue(station, out int t) ? t : 0;
            if (!maps.StationLevels.TryGetValue(station, out IReadOnlyDictionary<int, int> levels))
            {
                return tier;
            }
            int bestLevel = 0;
            foreach (KeyValuePair<int, int> level in levels)
            {
                if (level.Key <= minLevel && level.Key > bestLevel)
                {
                    bestLevel = level.Key;
                    tier = level.Value;
                }
            }
            return tier;
        }
    }
}
