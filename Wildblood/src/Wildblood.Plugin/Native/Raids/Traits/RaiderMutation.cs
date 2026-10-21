// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// Changed by expDARE, 2026: Wildblood namespace and authority-owned raid integration.
using ExtraPotions.Wildblood.Adopted.Rules;
using ExtraPotions.Wildblood.Adopted.Traits;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Raids
{
    /// <summary>
    /// The one mutation a hot raid gives a raider that rolled none: weighted by the biome's own chance of each at the
    /// raider's star count (with its `creatures:` entry on top), as a difficulty preset picks one, so the Swamp's raiders
    /// still lean Miasmic. Never Gilded (a raider that runs from every player is no raid), never one switched off or ruled
    /// out by its body or kind. Where the biome gives no mutation any weight it draws evenly among those allowed, since the
    /// heat promised one.
    /// </summary>
    internal static class RaiderMutation
    {
        private const int NeverOnRaiders = 1 << (int)Mutation.Gilded;

        /// <summary>A mask with one mutation, or 0 when the creature may take none.</summary>
        public static int Pick(Character creature, Heightmap.Biome biome, int stars)
        {
            RuleSet set = RuleState.Active;
            BiomeRules rules = set.For(biome, Utils.GetPrefabName(creature.gameObject));
            int barred = MutationBars.Of(creature) | NeverOnRaiders;
            Mutation[] all = MutationCatalog.InOrder;
            float[] weights = new float[all.Length];
            float total = 0f;
            int allowed = 0;
            for (int i = 0; i < all.Length; i++)
            {
                bool may = TraitRoller.MayRoll(all[i], set, barred);
                allowed += may ? 1 : 0;
                weights[i] = may ? Mathf.Max(0f, rules.ChanceOf(all[i], stars)) : 0f;
                total += weights[i];
            }
            if (allowed == 0)
            {
                return 0;
            }
            return 1 << (int)all[total > 0f ? TraitRoller.PickWeighted(weights) : Even(all, set, barred, allowed)];
        }

        // The n-th allowed mutation, n drawn evenly.
        private static int Even(Mutation[] all, RuleSet set, int barred, int allowed)
        {
            int n = Random.Range(0, allowed);
            for (int i = 0; i < all.Length; i++)
            {
                if (TraitRoller.MayRoll(all[i], set, barred) && n-- == 0)
                {
                    return i;
                }
            }
            return 0;
        }
    }
}
