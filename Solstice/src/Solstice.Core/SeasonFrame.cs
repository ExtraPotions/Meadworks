// Solstice - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Diagnostics;

namespace ExtraPotions.Solstice
{
    /// <summary>Work a day-change or General Enabled frame can name in the Debug timing line.</summary>
    [Flags]
    public enum SeasonFrameSystem
    {
        None = 0,
        WeatherListSwap = 1,
        EnvironmentVariants = 2,
        MusicInvalidate = 4,
        VisualsRepaint = 8,
        MinimapTint = 16
    }

    /// <summary>
    /// Marks which Solstice systems did work since the last clear, and times the weather-list swap and music invalidate,
    /// which can run outside <c>Plugin.Update</c>. Main thread only.
    /// </summary>
    public static class SeasonFrame
    {
        static readonly Stopwatch clock = Stopwatch.StartNew();

        public static SeasonFrameSystem Ran { get; private set; }

        public static bool DayMoved { get; set; }

        public static bool EnabledToggled { get; set; }

        public static long WeatherSwapTicks { get; private set; }

        public static long MusicTicks { get; private set; }

        public static void Mark(SeasonFrameSystem system) => Ran |= system;

        public static long Stamp() => clock.ElapsedTicks;

        public static void Add(SeasonFrameSystem system, long startedAt)
        {
            Ran |= system;
            long delta = clock.ElapsedTicks - startedAt;
            if (delta < 0) delta = 0;
            if (system == SeasonFrameSystem.WeatherListSwap) WeatherSwapTicks += delta;
            else if (system == SeasonFrameSystem.MusicInvalidate) MusicTicks += delta;
        }

        public static void ClearWork()
        {
            Ran = SeasonFrameSystem.None;
            WeatherSwapTicks = 0;
            MusicTicks = 0;
        }

        public static int Milliseconds(long ticks)
        {
            if (ticks <= 0) return 0;
            long frequency = Stopwatch.Frequency;
            if (frequency <= 0) return 0;
            return (int)(ticks * 1000 / frequency);
        }

        public static string Describe(SeasonFrameSystem systems)
        {
            if (systems == SeasonFrameSystem.None) return "none";
            string text = "";
            if ((systems & SeasonFrameSystem.WeatherListSwap) != 0) text = Join(text, "weather list swap");
            if ((systems & SeasonFrameSystem.EnvironmentVariants) != 0) text = Join(text, "environment variants");
            if ((systems & SeasonFrameSystem.MusicInvalidate) != 0) text = Join(text, "music invalidate");
            if ((systems & SeasonFrameSystem.VisualsRepaint) != 0) text = Join(text, "visuals repaint");
            if ((systems & SeasonFrameSystem.MinimapTint) != 0) text = Join(text, "minimap tint");
            return text.Length == 0 ? "none" : text;
        }

        static string Join(string text, string name) => text.Length == 0 ? name : text + ", " + name;
    }
}
