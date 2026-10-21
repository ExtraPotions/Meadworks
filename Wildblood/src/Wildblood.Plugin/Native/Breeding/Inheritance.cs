// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System.Collections.Generic;
using ExtraPotions.Wildblood.Adopted.Rules;
using ExtraPotions.Wildblood.Adopted.Traits;
using ExtraPotions.Wildblood.Adopted.Util;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Breeding
{
    /// <summary>
    /// The inheritance roll, and nothing else - no ZDO, no game objects. Stars are drawn with equal odds from 0 up to
    /// the stronger parent's, so a line only holds its strength if the player keeps the good ones. The mutation is one
    /// of the parents' own, picked at random from both, whenever either has one (at the rule file's chance, 100 by
    /// default); a mutation switched off in `mutations enabled` is never passed on, and neither is one the species' body
    /// rules out (Gilded and Relentless for a large kind, <see cref="BodySize.Barred"/>), which a parent rolled before
    /// that rule may still carry. A newborn never rolls anything new. With `creature stars: false` it inherits no stars of
    /// this mod: it keeps the level the game gives it, its parent's.
    /// </summary>
    internal static class Inheritance
    {
        /// <summary>The newborn's traits; <paramref name="barred"/> is what its species' body rules out.</summary>
        public static CreatureTraits Offspring(CreatureTraits mother, CreatureTraits? sire, RuleSet rules, int barred = 0)
        {
            int cap = Mathf.Max(mother.Stars, sire?.Stars ?? 0);
            bool starsKept = ExtraPotions.Wildblood.Runtime.Plugin.Settings == null
                || ExtraPotions.Wildblood.Runtime.Plugin.Settings.Get(ExtraPotions.Wildblood.Config.WildbloodSettings.Traits.TamedStars);
            bool mutationsKept = ExtraPotions.Wildblood.Runtime.Plugin.Settings == null
                || ExtraPotions.Wildblood.Runtime.Plugin.Settings.Get(ExtraPotions.Wildblood.Config.WildbloodSettings.Traits.TamedKeepTraits);
            int stars = rules.CreatureStars && starsKept ? Random.Range(0, cap + 1) : 0; // off: the game passes the parent's level on
            int mask = mutationsKept ? PickMutation((mother.Mask | (sire?.Mask ?? 0)) & ~barred, rules) : 0;
            return new CreatureTraits(stars, mask);
        }

        private static int PickMutation(int pool, RuleSet rules)
        {
            List<Mutation> candidates = new List<Mutation>();
            foreach (Mutation mutation in MutationCatalog.InOrder)
            {
                if ((pool & (1 << (int)mutation)) != 0 && rules.IsEnabled(mutation))
                {
                    candidates.Add(mutation);
                }
            }
            if (candidates.Count == 0 || !Dice.Percent(rules.Breeding.MutationChance))
            {
                return 0;
            }
            return 1 << (int)candidates[Random.Range(0, candidates.Count)];
        }
    }
}
