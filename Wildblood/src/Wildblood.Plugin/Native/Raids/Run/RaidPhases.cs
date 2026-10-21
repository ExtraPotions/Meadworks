// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// Changed by expDARE, 2026: Wildblood namespace and authority-owned raid integration.
namespace ExtraPotions.Wildblood.Adopted.Raids
{
    /// <summary>
    /// One tick of a raid on its host's owner (features/raids.md section 4): the 20 second countdown, waves 1 to 3 with
    /// 20 seconds between them, and the ends. Each tick first counts the players at the raid, then checks the ends that
    /// can come at any time (<see cref="RaidEnding"/>), then moves the clock on. Everything it reads and writes is in the
    /// host's ZDO, so a machine that has just taken the host over picks up exactly where the last one stopped. What a
    /// wave brings, and how many of it still live, is <see cref="RaidWaves"/>'.
    /// </summary>
    internal static class RaidPhases
    {
        /// <summary>Seconds a test marker stays after its raid ends: its raiders read how the raid ended from it (a stop
        /// kills them, anything else walks them off) on their slow tick, and a gone host would read as a loss.</summary>
        private const float MarkerLingerSeconds = 10f;

        public static void Step(RaidRunner runner, long now)
        {
            RaidState state = runner.State;
            RaidPhase phase = state.Phase;
            if (!RaidState.IsRunning(phase))
            {
                if (runner.RemoveWhenDone && now - state.EndedAt >= RaidState.Ms(MarkerLingerSeconds))
                {
                    runner.Remove(); // a test marker with no raid left to run
                }
                return;
            }
            RaidPresence.Count near = RaidPresence.Of(runner);
            runner.Remember(near);
            RaidEnd end = RaidEnding.Check(state, now);
            RaidPresence.Stamp(state, near, now);
            if (end != RaidEnd.None)
            {
                runner.End(end);
                return;
            }
            RaidDrops.Review(runner, near.StrongestTier);
            Advance(runner, state, phase, now);
        }

        private static void Advance(RaidRunner runner, RaidState state, RaidPhase phase, long now)
        {
            switch (phase)
            {
                case RaidPhase.Countdown when now >= state.PhaseUntil:
                    BeginWave(runner, state, 1);
                    break;
                case RaidPhase.Break when now >= state.PhaseUntil:
                    BeginWave(runner, state, state.Wave + 1);
                    break;
                case RaidPhase.Wave:
                    Fight(runner, state, now);
                    break;
            }
        }

        private static void BeginWave(RaidRunner runner, RaidState state, int wave)
        {
            state.Wave = wave;
            state.Phase = RaidPhase.Wave;
            RaidWaves.Begin(runner, wave);
        }

        // The wave runs until nobody of it is alive or still to come; then the next after a break, or the raid is won.
        private static void Fight(RaidRunner runner, RaidState state, long now)
        {
            WaveCount count = RaidWaves.Tick(runner);
            state.Alive = count.Alive;
            state.Left = count.Alive + count.ToCome;
            if (!count.Cleared)
            {
                return;
            }
            if (state.Wave >= RaidTable.Waves)
            {
                runner.End(RaidEnd.Won);
                return;
            }
            state.PhaseUntil = now + RaidState.Ms(RaidTable.BreakSeconds);
            state.Phase = RaidPhase.Break;
        }
    }
}
