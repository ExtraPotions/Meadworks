// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System.Collections.Generic;
using ExtraPotions.Inscribe.Adopted.Rules;

namespace ExtraPotions.Inscribe.Adopted.Loot
{
    /// <summary>
    /// Norns' Favour on the gear rarity draw (drops.md section 10): every weight above the lowest magic rarity (ladder
    /// index 2 and up; index 0 is the base rarity, which never drops) is multiplied by <c>1 + L/100</c> before the pick.
    /// A weight of 0 stays 0, so a blocked rarity stays blocked. Without a bonus the precomputed table is used as is.
    /// The re-weighted pick walks the table's few entries twice and allocates nothing (drops.md section 13).
    /// </summary>
    public static class RarityFind
    {
        public static bool TryPick(EconomyRules economy, WeightedTable<RarityDef> table, int tier, bool boss, float bonusPercent,
            float unit, out RarityDef rarity)
        {
            if ((!(bonusPercent > 0f) && !Runtime.RarityTuning.Active) || table.Count == 0)
            {
                return table.TryPick(unit, out rarity);
            }
            IReadOnlyDictionary<string, float[]> rows = boss ? economy.Drops.BossRarityWeights : economy.Drops.RarityWeights;
            float factor = 1f + bonusPercent / 100f;
            float total = 0f;
            foreach (RarityDef item in table.Items)
            {
                total += Weight(item, rows, tier, factor);
            }
            float target = System.Math.Max(0f, System.Math.Min(unit, 0.9999999f)) * total;
            return Walk(table.Items, rows, tier, factor, target, out rarity);
        }

        private static bool Walk(IReadOnlyList<RarityDef> items, IReadOnlyDictionary<string, float[]> rows, int tier, float factor,
            float target, out RarityDef rarity)
        {
            rarity = null!;
            float running = 0f;
            for (int i = 0; i < items.Count; i++)
            {
                float weight = Weight(items[i], rows, tier, factor);
                if (weight <= 0f)
                {
                    continue;
                }
                rarity = items[i];
                running += weight;
                if (target < running)
                {
                    return true;
                }
            }
            return rarity != null;   // rounding at the top end: the last weighted entry
        }

        /// <summary>The row weight times <c>drop_weight</c> (as the rules' table was built), shifted above the first magic rarity.</summary>
        public static float Weight(RarityDef rarity, IReadOnlyDictionary<string, float[]> rows, int tier, float factor)
        {
            float weight = !rarity.SourceOnly && rows.TryGetValue(rarity.Id, out float[] row) && tier >= 1 && tier <= row.Length
                ? row[tier - 1] * rarity.DropWeight * Runtime.RarityTuning.Multiplier(rarity.Id)
                : 0f;
            return rarity.Index > 1 ? weight * factor : weight;
        }
    }
}
