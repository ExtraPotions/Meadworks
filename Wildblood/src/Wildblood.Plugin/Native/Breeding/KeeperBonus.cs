// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using ExtraPotions.Wildblood.Adopted.Rules;
using ExtraPotions.Wildblood.Adopted.Traits;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Breeding
{
    /// <summary>
    /// The hand-off from GrindstoneSkills' Husbandry skill. When a keeper's "Better offspring" roll succeeds for a birth,
    /// that mod writes the int <see cref="StarUpKey"/> (1) on the pregnant parent's ZDO just before the game's birth call
    /// and clears it right after, on the same machine this birth runs on. The newborn then gets that many stars more than
    /// its inheritance roll, up to one above the stronger parent. When that cap leaves no room, the key is set to 0, which
    /// tells GrindstoneSkills no star was added; with `creature stars: false` it is always declined, since this mod adds
    /// no stars. Only the key name is shared: neither mod references the other, and
    /// without GrindstoneSkills the key is never set.
    /// </summary>
    internal static class KeeperBonus
    {
        public const string StarUpKey = "grindstone_star_up";

        public static CreatureTraits Apply(ZDO zdo, CreatureTraits child, CreatureTraits mother, CreatureTraits? sire)
        {
            int bonus = zdo.GetInt(StarUpKey);
            if (bonus <= 0)
            {
                return child;
            }
            bool starsKept = ExtraPotions.Wildblood.Runtime.Plugin.Settings == null
                || ExtraPotions.Wildblood.Runtime.Plugin.Settings.Get(ExtraPotions.Wildblood.Config.WildbloodSettings.Traits.TamedStars);
            int cap = RuleState.Active.CreatureStars && starsKept ? Mathf.Max(mother.Stars, sire?.Stars ?? 0) + 1 : 0; // off: declined
            int stars = Mathf.Min(child.Stars + bonus, cap);
            if (stars <= child.Stars)
            {
                zdo.Set(StarUpKey, 0); // declined: GrindstoneSkills then shows no "Strong offspring!"
                return child;
            }
            return new CreatureTraits(stars, child.Mask);
        }
    }
}
