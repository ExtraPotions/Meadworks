// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System.Collections.Generic;

namespace ExtraPotions.Wildblood.Adopted.Rules
{
    /// <summary>
    /// One drop row written in the rule file: either an override of a row the creature already has (matched by item
    /// prefab name, possibly just removing it) or an extra row added to its table. Amounts are inclusive, chance is
    /// 0-100 like every other percentage in the file.
    /// </summary>
    public sealed class DropRule
    {
        public string Item = "";
        public int AmountMin = 1;
        public int AmountMax = 1;
        public float Chance = 100f;

        /// <summary>Override rows only: delete the creature's own row for this item instead of adjusting it.</summary>
        public bool Remove;

        /// <summary>Extra rows only: true makes the row follow the mode (extra rolls, quantity scaling) like the
        /// creature's own rows; false rolls it exactly once, untouched by stars.</summary>
        public bool PerStar;

        public DropRule Clone()
        {
            return new DropRule
            {
                Item = Item, AmountMin = AmountMin, AmountMax = AmountMax, Chance = Chance,
                Remove = Remove, PerStar = PerStar,
            };
        }
    }

    /// <summary>
    /// The loot rules for one creature or boss, matched by prefab name from the file's `creatures:` list. Everything
    /// here is optional: an absent field leaves that part of the creature's loot to the world-wide settings, so an
    /// entry that names only mutation keys leaves an empty rule that drops exactly as no entry would. In Curated mode
    /// <see cref="Extras"/> is the creature's whole table (`loot.md` section 6). Two entries for one creature fill
    /// the same rule; the same entry's mutation keys live in <see cref="CreatureMutationRule"/>.
    /// </summary>
    public sealed class CreatureLootRule
    {
        /// <summary>Overrides the star `drops` quantity line for this creature; null keeps the biome's line.</summary>
        public float[]? Drops;

        /// <summary>Per-creature override of the world-wide trophy switch; null keeps the world-wide value.</summary>
        public bool? MultiplyTrophies;

        public readonly List<DropRule> Overrides = new List<DropRule>();
        public readonly List<DropRule> Extras = new List<DropRule>();

        public CreatureLootRule Clone()
        {
            CreatureLootRule copy = new CreatureLootRule
            {
                Drops = (float[]?)Drops?.Clone(), MultiplyTrophies = MultiplyTrophies,
            };
            foreach (DropRule rule in Overrides)
            {
                copy.Overrides.Add(rule.Clone());
            }
            foreach (DropRule rule in Extras)
            {
                copy.Extras.Add(rule.Clone());
            }
            return copy;
        }
    }
}
