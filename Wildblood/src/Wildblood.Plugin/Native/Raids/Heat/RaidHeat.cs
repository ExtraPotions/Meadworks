// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// Changed by expDARE, 2026: Wildblood namespace and authority-owned raid integration.
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Raids
{
    /// <summary>
    /// Gold against gear (features/raids.md section 3): the heat of a stake is the coins divided by the fair stake for
    /// the gear tier of the strongest player within the raid's 96 m, so a geared player cannot bring a fresh character
    /// along to make the gold count for more. The heat picks the band, and the band the loot multiplier. Fixed once when a
    /// raid is sounded; the chest's hover asks for it each time it redraws to show what a raid would be now.
    /// </summary>
    public readonly struct RaidHeat
    {
        public RaidHeat(int tier, float heat)
        {
            Tier = tier;
            Heat = heat;
            Band = RaidTable.BandFor(heat);
            Drops = Band.DropsAt(heat);
        }

        /// <summary>The gear tier the stake was weighed against, 0-7.</summary>
        public int Tier { get; }

        /// <summary>The stake over the fair stake for <see cref="Tier"/>.</summary>
        public float Heat { get; }

        /// <summary>The heat's row of the table.</summary>
        public RaidBand Band { get; }

        /// <summary>The raiders' loot multiplier at this heat.</summary>
        public float Drops { get; }

        /// <summary>
        /// The heat of <paramref name="coins"/> at <paramref name="at"/>: weighed against <paramref name="tierOverride"/>
        /// when it is 0 or more (the test command's), else against the strongest player within the raid's radius.
        /// </summary>
        public static RaidHeat Measure(Vector3 at, int coins, int tierOverride = -1)
        {
            int tier = tierOverride >= 0 ? Mathf.Min(tierOverride, RaidTable.MaxTier)
                : RaidPlayers.Near(at, RaidTable.RaidRadius).StrongestTier;
            return For(coins, tier);
        }

        /// <summary>The heat of a stake for a gear tier.</summary>
        public static RaidHeat For(int coins, int tier) =>
            new RaidHeat(tier, Mathf.Max(0, coins) / (float)RaidTable.FairStake(tier));
    }
}
