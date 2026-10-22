// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Death-brood exclusion adapted from ECR f331465b3ed2551c89decc42ba81565a7b689543.
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System.Collections.Generic;

namespace ExtraPotions.Wildblood.Adopted.Traits
{
    /// <summary>
    /// The mutations a creature never takes, whatever the rule file says, packed like a trait mask: what its body rules
    /// out (<see cref="BodySize.Barred"/>) and what its kind does, by prefab name. A Deathsquito is never Cloaked: an
    /// invisible needle out of nowhere is no fight. A kind's bars also come off a creature rolled before them as it
    /// loads, the same on every machine, so no saved world keeps one.
    /// </summary>
    public static class MutationBars
    {
        private static readonly Dictionary<string, int> KindBars = new Dictionary<string, int>
        {
            ["Deathsquito"] = 1 << (int)Mutation.Cloaked,
        };

        /// <summary>Everything this creature never rolls or inherits: its body's bars and its kind's.</summary>
        public static int Of(Character creature) =>
            BodySize.Barred(creature) | OfKind(Utils.GetPrefabName(creature.gameObject));

        /// <summary>What a kind never carries, by prefab name; 0 for most.</summary>
        public static int OfKind(string prefab) =>
            (KindBars.TryGetValue(prefab, out int bars) ? bars : 0) | DeathBrood.BarsOf(prefab)
            | ExtraPotions.Wildblood.Runtime.NativeCreatureRules.Barred(prefab);
    }
}
