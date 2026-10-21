// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// Changed by expDARE, 2026: Wildblood namespace and authority-owned raid integration.
using ExtraPotions.Wildblood.Adopted.Util;

namespace ExtraPotions.Wildblood.Adopted.Raids
{
    /// <summary>
    /// Ending a raid, however it ends, on the host's owner: the end goes into the host's ZDO (which is what every raider
    /// and every HUD line reads), the raiders are dealt with - on a stop every one dies dropping nothing, on a loss they
    /// walk off and vanish with their coins (<see cref="RaiderOrders"/>), after a win there are none - and the
    /// players near read how it ended. Nothing is paid at the end: what the raiders carried was paid as they fell.
    /// </summary>
    internal static class RaidFinish
    {
        public static void End(RaidRunner runner, RaidEnd how)
        {
            if (!runner.IsOwner || how == RaidEnd.None)
            {
                return;
            }
            RaidState state = runner.State;
            if (!state.Running)
            {
                return;
            }
            state.Ended = how;
            state.EndedAt = NetTime.NowMs();
            state.Phase = RaidPhase.Ended;
            Dismiss(runner, how);
            RaidNet.Tell(runner, RaidText.Ended(state.DisplayName, how));
            Log.Info($"raid at {runner.Position:F0} ({state.DisplayName}, {RaidTable.Band(state.Band).Name}) ended: {how}");
        }

        private static void Dismiss(RaidRunner runner, RaidEnd how)
        {
            if (how == RaidEnd.Stopped)
            {
                RaiderOrders.Stop(runner);
            }
            else if (how != RaidEnd.Won)
            {
                RaiderOrders.Disband(runner, how);
            }
        }
    }
}
