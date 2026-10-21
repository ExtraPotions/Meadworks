// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System.Text;
using ExtraPotions.Wildblood.Runtime;
using ExtraPotions.Wildblood.Adopted.Traits;

namespace ExtraPotions.Wildblood.Adopted.Display
{
    /// <summary>
    /// Builds the authoritative tell: a creature's mutations as words before its own name, in specification order.
    /// "Mad Greydwarf"; "Bloated Warding Miasmic Troll". A boss's aspects are its words the same way, headline first:
    /// "Twin Bonemass"; "Bountiful Enraged Mending Eikthyr". The name is always complete even when there are more
    /// mutations than stars, so it, not the colour, is what a player reads.
    /// </summary>
    public static class Naming
    {
        public static string Decorate(CreatureTraits traits, string baseName)
        {
            if (traits == null || string.IsNullOrEmpty(baseName)
                || (!traits.Any && traits.Aspect == Aspect.None && traits.ExtraAspects == 0))
            {
                return baseName;
            }
            StringBuilder builder = new StringBuilder();
            foreach (Aspect aspect in traits.Aspects())
            {
                builder.Append(WildbloodText.Text("aspect_" + aspect.ToString().ToLowerInvariant())).Append(' '); // a boss: "Bountiful Enraged Mending Eikthyr"
            }
            foreach (Mutation mutation in traits.Active())
            {
                builder.Append(WildbloodText.Text("mutation_" + mutation.ToString().ToLowerInvariant())).Append(' ');
            }
            string literalName = (baseName.Length > 256 ? baseName.Substring(0, 256) : baseName).Replace('<', '‹').Replace('>', '›');
            return WildbloodText.Format("name_format", builder.ToString().TrimEnd(), literalName);
        }
    }
}
