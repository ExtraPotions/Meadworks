// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration; the star draw respects the world's enemy level-up rate (Respect Enemy Level Up Rate).
using ExtraPotions.Wildblood.Adopted.Rules;
using ExtraPotions.Wildblood.Adopted.Util;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Traits
{
    /// <summary>
    /// A creature's roll under a difficulty preset (features/difficulty.md): stars drawn from the preset's cell for its
    /// biome and the world tier, then one chance to be mutated - the cell's rate plus a quarter of it per star, never
    /// above 100 - and, if it is, exactly one mutation, picked by the biome's own leanings: each enabled mutation's
    /// chance at that star count in the biome's rows (with the creature's `creatures:` entry on top), so the Swamp
    /// still breeds Miasmic and the Ashlands Bloated, and a creature that can never take a mutation stays plain. A large
    /// creature's Gilded and Relentless weigh nothing (<see cref="BodySize.Barred"/>), so a mutated one takes one of the
    /// others. The world tier's boost lines are not used: the preset carries its own tier terms. Owner only, like every
    /// roll.
    /// </summary>
    public static class PresetRoller
    {
        public static CreatureTraits Roll(BiomeRules rules, RuleSet ruleSet, int tier, Heightmap.Biome biome, int barred)
        {
            PresetCell cell = PresetCell.For(ruleSet.Difficulty, tier, PresetTables.Rank(biome));
            int stars = ruleSet.CreatureStars ? TraitRoller.PickWeighted(
                ExtraPotions.Wildblood.Stars.StarWeights.Effective(cell.Weights, ruleSet.MaxStars, ruleSet.RespectLevelUpRate, Game.m_enemyLevelUpRate)) : 0;
            float rate = Mathf.Min(100f, cell.Mutation * (1f + PresetTables.StarMutation * stars));
            int mask = Dice.Percent(rate) ? PickOne(rules, ruleSet, stars, barred) : 0;
            return new CreatureTraits(stars, mask) { Tier = tier };
        }

        // One mutation, weighted by the biome's chance of each at this star count; 0 when none can be taken here.
        private static int PickOne(BiomeRules rules, RuleSet ruleSet, int stars, int barred)
        {
            Mutation[] all = MutationCatalog.InOrder;
            float[] weights = new float[all.Length];
            float total = 0f;
            for (int i = 0; i < all.Length; i++)
            {
                weights[i] = TraitRoller.MayRoll(all[i], ruleSet, barred)
                    ? Mathf.Max(0f, rules.ChanceOf(all[i], stars)) : 0f;
                total += weights[i];
            }
            if (total <= 0f)
            {
                return 0;
            }
            return 1 << (int)all[TraitRoller.PickWeighted(weights)];
        }
    }
}
