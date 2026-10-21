// Inscribe - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: meadworks.inscribe.legendary_reward, the only way a Legendary item comes into the world (batch 3).
using System;
using ExtraPotions.Inscribe.Adopted.Loot;
using ExtraPotions.Inscribe.Adopted.Rules;
using ExtraPotions.Inscribe.Config;
using ExtraPotions.Inscribe.Integration;
using ExtraPotions.Inscribe.Rarity;
using ExtraPotions.Meadworks.Crucible;
using RarityDef = ExtraPotions.Inscribe.Adopted.Rules.RarityDef;
using UnityEngine;

namespace ExtraPotions.Inscribe.Runtime
{
    /// <summary>
    /// <c>Func&lt;string source, string creaturePrefab, Vector3 position, bool&gt;</c> for Wildblood: source <c>boss</c> on a
    /// boss kill (the boss prefab), <c>raid</c> on a won raid (prefab may be empty). Checks the source switch and chance
    /// (host settings, synced), picks a gear base of the boss's tier (or the biome's at the position) that can hold five
    /// inscriptions, rolls it Legendary and drops it there. Runs on the peer Wildblood calls it on (the creature or raid
    /// owner), like every other Inscribe drop. True when an item dropped. Without Wildblood nothing calls it.
    /// </summary>
    internal static class LegendaryRewards
    {
        private const int BaseAttempts = 12;

        public static void Publish(ProductRegistration product) =>
            product.AddCapability<Func<string, string, Vector3, bool>>(LegendarySources.Capability, LegendarySources.Version, Reward);

        internal static bool Reward(string source, string creaturePrefab, Vector3 position)
        {
            try
            {
                InscribeConfig? settings = Plugin.Settings;
                if (settings == null || !InscribeModules.Active(InscribeModule.Rarity)) return false;
                bool boss = source == LegendarySources.Boss;
                if (!LegendarySources.Allows(source, settings.Get(InscribeSettings.Drops.LegendaryFromBosses), settings.Get(InscribeSettings.Drops.LegendaryFromRaids)))
                    return false;
                float chance = settings.Get(boss ? InscribeSettings.Drops.LegendaryBossChance : InscribeSettings.Drops.LegendaryRaidChance);
                if (!LegendarySources.Rolls(chance, LootRoller.Rng.NextDouble())) return false;
                RuleSet rules = ActiveRules.Current;
                RarityDef? legendary = rules.Economy.Rarity(RarityTiers.Legendary);
                if (legendary == null) return false;   // an owner removed the rung from the ladder
                int tier = TierOf(rules, creaturePrefab, position);
                WeightedTable<GearBase> bases = GearPool.ForTier(tier);
                for (int i = 0; i < BaseAttempts; i++)
                {
                    if (!bases.TryPick((float)LootRoller.Rng.NextDouble(), out GearBase gear) || gear.CapacityFor(legendary) < legendary.MinAffixes)
                        continue;
                    ItemDrop.ItemData? item = GearFactory.Build(gear, legendary, false, LootRoller.Rng, rules);
                    if (item == null || ItemStateId(item) != legendary.Id) continue;
                    Adopted.Loot.LootSpawner.Drop(item, 1, position + Vector3.up);
                    return true;
                }
                return false;
            }
            catch (Exception e)
            {
                Adopted.Core.Log.Warn("legendary reward failed: " + e.Message);
                return false;
            }
        }

        private static string? ItemStateId(ItemDrop.ItemData item) => Adopted.Affixes.ItemState.Read(item).RarityId;

        private static int TierOf(RuleSet rules, string creaturePrefab, Vector3 position) =>
            !string.IsNullOrEmpty(creaturePrefab) && rules.Economy.Drops.Bosses.TryGetValue(creaturePrefab, out BossDrop drop) && drop.Tier > 0
                ? BiomeTiers.Clamp(drop.Tier)
                : BiomeTiers.AtPosition(position, rules.Economy, out _);
    }
}
