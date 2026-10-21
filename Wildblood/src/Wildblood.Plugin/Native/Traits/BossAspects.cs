// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System.Collections.Generic;

namespace ExtraPotions.Wildblood.Adopted.Traits
{
    /// <summary>
    /// The aspects one boss fight is decided with before there is a boss to carry them: the headline aspect and, beside a
    /// Bountiful one, its extras, one bit per aspect value as <see cref="CreatureTraits.ExtraAspects"/> keeps them. It is
    /// what an altar shows and locks at the offering and what an altar-less boss rolls, carried as one value so the extras
    /// can never be parted from the Bountiful they belong to on the way from the roll (or the bowl) to the boss's traits.
    /// </summary>
    public readonly struct BossAspects
    {
        public static readonly BossAspects None = new BossAspects(Aspect.None, 0);

        public readonly Aspect Headline;

        /// <summary>The extra aspects, one bit per aspect value; only a Bountiful draw makes any, so 0 for every other.</summary>
        public readonly int Extras;

        public BossAspects(Aspect headline, int extras)
        {
            Headline = headline;
            Extras = extras;
        }

        /// <summary>The traits a boss is born with: these aspects on the stars it rolled.</summary>
        public CreatureTraits ToTraits(int stars) => new CreatureTraits(stars, Headline) { ExtraAspects = Extras };

        /// <summary>The extras alone, in catalog order; empty for every aspect but Bountiful.</summary>
        public IEnumerable<Aspect> ExtrasInOrder()
        {
            foreach (Aspect aspect in AspectCatalog.InOrder)
            {
                if (aspect != Headline && (Extras & (1 << (int)aspect)) != 0)
                {
                    yield return aspect;
                }
            }
        }
    }
}
