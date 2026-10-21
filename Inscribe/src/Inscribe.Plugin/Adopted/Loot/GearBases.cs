// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System.Collections.Generic;
using ExtraPotions.Inscribe.Adopted.Items;
using ExtraPotions.Inscribe.Adopted.Rules;
using UnityEngine;

namespace ExtraPotions.Inscribe.Adopted.Loot
{
    /// <summary>One item type that can drop pre-rolled: its prefab, class, item level, pool tier and affix capacity.</summary>
    public sealed class GearBase
    {
        internal GearBase(GameObject prefab, ItemDrop template, string name, ClassInfo info, int level, int poolTier, GearCapacity capacity)
        {
            Prefab = prefab;
            Template = template;
            Name = name;
            Class = info;
            Level = level;
            PoolTier = poolTier;
            Capacity = capacity;
        }

        public GameObject Prefab { get; }
        public ItemDrop Template { get; }
        public string Name { get; }

        /// <summary>The base's item class (classes-and-tiers.md section 1).</summary>
        public ClassInfo Class { get; }

        /// <summary>The base's own item level (1-8): the level its affixes roll under, never the biome's.</summary>
        public int Level { get; }

        /// <summary>The tier the pool files it under: the item level, or its <c>gear.include</c> tier.</summary>
        public int PoolTier { get; }

        /// <summary>How many affixes a fresh roll can give it, per kind (<see cref="GearCapacity"/>).</summary>
        internal GearCapacity Capacity { get; }

        /// <summary>The most affixes a fresh roll at this rarity can give it.</summary>
        public int CapacityFor(RarityDef rarity) => Capacity.For(rarity);
    }

    /// <summary>
    /// Collects the drop-eligible magic bases from the object database (drops.md section 8, classes-and-tiers.md section
    /// 10): magic bases only (a class with <c>rolls: true</c>, never stackable, never a rune), not in <c>gear.exclude</c>,
    /// and - unless listed in <c>gear.include</c> - with a recipe (when <c>gear.require_recipe</c>), no DLC and no quest
    /// flag. A base whose pool cannot fill the lowest magic rarity's minimum is left out with one load warning.
    /// All at once (<see cref="Collect"/>) or a slice at a time (<see cref="GearCollector"/>, the pool's warm-up).
    /// </summary>
    internal static class GearBases
    {
        public static List<GearBase> Collect(ObjectDB db, RuleSet rules)
        {
            GearCollector collector = new GearCollector(rules);
            foreach (GameObject prefab in db.m_items)
            {
                collector.Add(prefab);
            }
            return collector.Finish();
        }

        internal static GearBase? TryBase(GameObject prefab, GearDropRules gear, HashSet<string> exclude, RuleSet rules)
        {
            ItemDrop drop = prefab.GetComponent<ItemDrop>();
            if (drop == null || !ItemClasses.IsMagicBase(drop.m_itemData))
            {
                return null;
            }
            string name = prefab.name;
            bool included = gear.Include.TryGetValue(name, out int includeTier);
            if (exclude.Contains(name) || (!included && !Eligible(drop.m_itemData, name, gear)))
            {
                return null;
            }
            int level = ItemTier.Of(name);
            ClassInfo info = ItemClasses.Classify(drop.m_itemData);
            int poolTier = included ? BiomeTiers.Clamp(includeTier) : level;
            return new GearBase(prefab, drop, name, info, level, poolTier, GearCapacity.Of(info, level, rules));
        }

        private static bool Eligible(ItemDrop.ItemData item, string name, GearDropRules gear)
        {
            ItemDrop.ItemData.SharedData shared = item.m_shared;
            if (!string.IsNullOrEmpty(shared.m_dlc) || shared.m_questItem)
            {
                return false;
            }
            return !gear.RequireRecipe || ItemTier.HasRecipe(name);
        }

        internal static RarityDef? LowestMagic(EconomyRules economy)
        {
            for (int i = 0; i < economy.Rarities.Count; i++)
            {
                if (!economy.Rarities[i].IsBase)
                {
                    return economy.Rarities[i];
                }
            }
            return null;
        }
    }
}
