// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Horde adoption adapted from ECR f331465b3ed2551c89decc42ba81565a7b689543.
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using UnityEngine;
using Random = UnityEngine.Random;

namespace ExtraPotions.Wildblood.Adopted.Util
{
    /// <summary>Percentage dice. All chance rolls in the mod go through here so the convention is uniform.</summary>
    internal static class Dice
    {
        /// <summary>True with the given percentage (0..100). A chance of 0 never fires, 100 always does.</summary>
        public static bool Percent(float chancePercent)
        {
            if (chancePercent <= 0f)
            {
                return false;
            }
            return Random.Range(0f, 100f) <= chancePercent;
        }

        /// <summary>A fresh 0..100 draw, for stepping through a probability table.</summary>
        public static float Draw() => Random.Range(0f, 100f);

        internal static int Round(float amount)
        {
            int whole = Mathf.FloorToInt(amount);
            return Random.value < amount - whole ? whole + 1 : whole;
        }
    }
}
