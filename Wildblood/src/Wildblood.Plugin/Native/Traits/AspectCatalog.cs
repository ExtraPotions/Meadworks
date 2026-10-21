// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
namespace ExtraPotions.Wildblood.Adopted.Traits
{
    /// <summary>
    /// The static facts about each aspect: the order the specification lists them in, the word placed before a boss's
    /// name, and the key the rule file uses for it. Numbers live in the rule file, not here.
    /// </summary>
    public static class AspectCatalog
    {
        /// <summary>The nineteen aspects, in specification order, without <see cref="Aspect.None"/>.</summary>
        public static readonly Aspect[] InOrder =
        {
            Aspect.Reflective, Aspect.Shielded, Aspect.Mending, Aspect.Summoner,
            Aspect.Elementalist, Aspect.Enraged, Aspect.Twin, Aspect.Phantom,
            Aspect.Adaptive, Aspect.Fixated, Aspect.Stormbound, Aspect.Gravitic, Aspect.Colossal,
            Aspect.Tethered, Aspect.Bountiful, Aspect.Portalbound, Aspect.Nightfall, Aspect.Brutal,
            Aspect.Echoing,
        };

        /// <summary>Every outcome a roll can produce: the plain fight first, then the nineteen.</summary>
        public static readonly Aspect[] Outcomes =
        {
            Aspect.None, Aspect.Reflective, Aspect.Shielded, Aspect.Mending, Aspect.Summoner,
            Aspect.Elementalist, Aspect.Enraged, Aspect.Twin, Aspect.Phantom,
            Aspect.Adaptive, Aspect.Fixated, Aspect.Stormbound, Aspect.Gravitic, Aspect.Colossal,
            Aspect.Tethered, Aspect.Bountiful, Aspect.Portalbound, Aspect.Nightfall, Aspect.Brutal,
            Aspect.Echoing,
        };

        /// <summary>The word placed before the boss's own name, e.g. "Enraged" in "Enraged Eikthyr"; empty for None.</summary>
        public static string Word(Aspect aspect) => aspect == Aspect.None ? "" : aspect.ToString();

        /// <summary>The rule-file key: the aspect's word, or <c>none</c> for the plain fight.</summary>
        public static string Key(Aspect aspect) => aspect == Aspect.None ? "none" : aspect.ToString();

        /// <summary>The outcome a rule-file or console word names, ignoring case; null for an unknown word.</summary>
        public static Aspect? FromName(string name)
        {
            foreach (Aspect aspect in Outcomes)
            {
                if (string.Equals(Key(aspect), name, System.StringComparison.OrdinalIgnoreCase))
                {
                    return aspect;
                }
            }
            return null;
        }
    }
}
