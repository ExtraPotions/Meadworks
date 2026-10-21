// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// Changed by expDARE, 2026: Wildblood namespace and authority-owned raid integration.
namespace ExtraPotions.Wildblood.Adopted.Raids
{
    /// <summary>
    /// The words players read about a raid: the message as it is sounded, the one as it ends, and the line under the
    /// minimap. In English, like the rest of the mod's own texts; one place, so they read alike.
    /// </summary>
    internal static class RaidText
    {
        /// <summary>"Raiders are coming for your gold!" with the raid's name and its difficulty.</summary>
        public static string Started(string name, RaidBand band) =>
            $"Raiders are coming for your gold!\n{name} ({band.Name})";

        public static string Ended(string name, RaidEnd how)
        {
            switch (how)
            {
                case RaidEnd.Won: return $"{name} beaten! Your gold is yours again.";
                case RaidEnd.Robbed: return "The Raiders Chest is broken: the raiders make off with your gold.";
                case RaidEnd.Stopped: return $"{name} called off. The raiders fall, and your gold with them.";
                case RaidEnd.Abandoned: return "The raid is abandoned: the raiders leave with your gold.";
                case RaidEnd.TimedOut: return "Time is up: the raiders leave with your gold.";
                default: return "";
            }
        }

        /// <summary>The line under the minimap: "Fuling raid (Brutal) - wave 2 of 3 - 5 left - 8:40".</summary>
        public static string Line(string name, RaidBand band, RaidPhase phase, int wave, int left, int secondsLeft,
            int secondsToNext)
        {
            string head = $"{name} ({band.Name})";
            string clock = Clock(secondsLeft);
            switch (phase)
            {
                case RaidPhase.Countdown: return $"{head} - first wave in {Clock(secondsToNext)}";
                case RaidPhase.Break: return $"{head} - wave {wave + 1} of {RaidTable.Waves} in {Clock(secondsToNext)} - {clock}";
                default: return $"{head} - wave {wave} of {RaidTable.Waves} - {left} left - {clock}";
            }
        }

        /// <summary>Whole seconds as m:ss.</summary>
        public static string Clock(int seconds)
        {
            int s = seconds < 0 ? 0 : seconds;
            return $"{s / 60}:{s % 60:00}";
        }
    }
}
