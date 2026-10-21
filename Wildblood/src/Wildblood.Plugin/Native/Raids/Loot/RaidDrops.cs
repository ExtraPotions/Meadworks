// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// Changed by expDARE, 2026: Wildblood namespace and authority-owned raid integration.
using ExtraPotions.Wildblood.Adopted.Util;

namespace ExtraPotions.Wildblood.Adopted.Raids
{
    /// <summary>
    /// The raid's loot multiplier while it runs (features/raids.md section 3, "Mid-raid"): the heat is fixed at the
    /// start, but when a stronger player walks in, the drops fall to what their gear would have made of the gold, from
    /// then on. The coins carried and the fight stay as they were. It only ever falls: a geared friend who leaves again
    /// does not bring the loot back, so bringing one in saves the base, not the loot. The multiplier lives in the host's
    /// ZDO, where every raider's owner reads it as the raider dies (<see cref="RaiderPurse.DropsFor"/>).
    /// </summary>
    public static class RaidDrops
    {
        /// <summary>
        /// Every tick (4 a second) of a running raid, on the host's owner, with the strongest gear tier among the players
        /// at the raid now. When it is above <see cref="RaidState.Tier"/>, lowers <see cref="RaidState.Drops"/> to
        /// <c>RaidHeat.For(state.Stake, tier).Drops</c> if that is lower; never raises it. Writes only on a change, so the
        /// host's ZDO is resent only the tick the drops fall. Costs one ZDO read while nobody stronger is there.
        /// </summary>
        public static void Review(RaidRunner runner, int strongestTierNow)
        {
            RaidState state = runner.State;
            if (strongestTierNow <= state.Tier)
            {
                return;
            }
            float before = state.Drops;
            float fallen = RaidHeat.For(state.Stake, strongestTierNow).Drops;
            if (fallen >= before)
            {
                return;
            }
            state.Drops = fallen;
            if (Log.Diagnostics)
            {
                Log.Diag($"raid at {runner.Position:F0}: a tier {strongestTierNow} player arrived, drops x{before:0.##} -> x{fallen:0.##}");
            }
        }
    }
}
