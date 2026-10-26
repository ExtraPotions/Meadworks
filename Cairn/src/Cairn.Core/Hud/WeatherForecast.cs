// Cairn - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original, clean room: the next environment is the game's period roll (shared world seconds divided by the
// environment duration, one weighted pick, then Ashlands and Deep North overrides) and the available-list rule Tempest
// applies (drop names a filter rejects; one fallback entry when none remain). No forecast mod's code is used.
using System;
using System.Collections.Generic;

namespace ExtraPotions.Cairn.Hud
{
    /// <summary>One environment the schedule may roll: its name, weight, and whether it replaces the roll in Ashlands or the Deep North.</summary>
    public readonly struct ForecastEntry
    {
        public ForecastEntry(string name, float weight, bool ashlandsOverride, bool deepnorthOverride)
        {
            Name = name ?? "";
            Weight = weight;
            AshlandsOverride = ashlandsOverride;
            DeepnorthOverride = deepnorthOverride;
        }

        public string Name { get; }
        public float Weight { get; }
        public bool AshlandsOverride { get; }
        public bool DeepnorthOverride { get; }
    }

    /// <summary>One future environment period: its index, the world seconds until it starts, and the environment name.</summary>
    public readonly struct ForecastChange
    {
        public ForecastChange(long period, long secondsUntil, string name)
        {
            Period = period;
            SecondsUntil = secondsUntil;
            Name = name ?? "";
        }

        public long Period { get; }
        public long SecondsUntil { get; }
        public string Name { get; }
    }

    /// <summary>What the clock line shows for the next change. Wind point is an English compass letter; the plugin localizes it.</summary>
    public readonly struct ForecastLine
    {
        public ForecastLine(bool visible, string nameKey, int minutes, string windPoint, int windPercent, WeatherCategory category)
        {
            Visible = visible;
            NameKey = nameKey ?? "";
            Minutes = minutes;
            WindPoint = windPoint ?? "";
            WindPercent = windPercent;
            Category = category;
        }

        public static ForecastLine None => new ForecastLine(false, "", 0, "", 0, WeatherCategory.Clear);

        public bool Visible { get; }
        public string NameKey { get; }
        public int Minutes { get; }
        public string WindPoint { get; }
        public int WindPercent { get; }
        public WeatherCategory Category { get; }
    }

    /// <summary>The environment schedule, without the game's random generator. The plugin supplies each period's roll.</summary>
    public static class WeatherForecast
    {
        public const int MaxAhead = 3;

        public static long Duration(long duration) => duration < 1 ? 1 : duration;

        /// <summary>The period index the game rolls: shared world seconds divided by the environment duration.</summary>
        public static long Period(long worldSeconds, long duration)
        {
            if (worldSeconds < 0) worldSeconds = 0;
            return worldSeconds / Duration(duration);
        }

        /// <summary>World seconds until the next period. Exactly on a boundary, the next change is a full period away.</summary>
        public static long SecondsUntilChange(long worldSeconds, long duration)
        {
            duration = Duration(duration);
            if (worldSeconds < 0) worldSeconds = 0;
            var into = worldSeconds % duration;
            return into == 0 ? duration : duration - into;
        }

        /// <summary>Whole minutes, rounded up, so a change still ahead reads as at least one minute.</summary>
        public static int MinutesUntil(long seconds)
        {
            if (seconds <= 0) return 0;
            var minutes = (seconds + 59) / 60;
            return minutes > int.MaxValue ? int.MaxValue : (int)minutes;
        }

        /// <summary>Sum of the weights that enter the roll. Override entries are left out, as in the game's weighted pick.</summary>
        public static float WeightSum(IReadOnlyList<ForecastEntry>? entries)
        {
            var total = 0f;
            if (entries == null) return 0f;
            foreach (var entry in entries)
                if (!entry.AshlandsOverride && !entry.DeepnorthOverride) total += entry.Weight;
            if (float.IsNaN(total) || float.IsInfinity(total) || total < 0f) return 0f;
            return total;
        }

        /// <summary>
        /// Tempest's available-list rule. Names the filter rejects are dropped. When every name is rejected and a fallback
        /// is known, that fallback is the only entry (weight 1). When no fallback is known, the original list is kept.
        /// A null filter leaves the list alone.
        /// </summary>
        public static IReadOnlyList<ForecastEntry> Filtered(IReadOnlyList<ForecastEntry>? entries, Func<string, bool>? allows, string? fallback)
        {
            if (entries == null) entries = Array.Empty<ForecastEntry>();
            if (allows == null) return entries;
            var blocked = false;
            var kept = new List<ForecastEntry>();
            foreach (var entry in entries)
            {
                if (allows(entry.Name ?? "")) kept.Add(entry);
                else blocked = true;
            }
            if (!blocked) return entries;
            if (kept.Count > 0) return kept;
            if (string.IsNullOrEmpty(fallback)) return entries;
            return new[] { new ForecastEntry(fallback, 1f, false, false) };
        }

        /// <summary>
        /// The game's weighted pick. <paramref name="roll"/> is in the range of <see cref="WeightSum"/> (0 when that sum is 0).
        /// Ashlands overrides, then Deep North overrides, replace the pick when that place applies.
        /// </summary>
        public static string? Select(IReadOnlyList<ForecastEntry>? entries, float roll, bool ashlands, bool deepnorth)
        {
            if (entries == null || entries.Count == 0) return null;
            if (float.IsNaN(roll) || float.IsInfinity(roll)) roll = 0f;
            string? picked = null;
            var cursor = 0f;
            foreach (var entry in entries)
            {
                if (entry.AshlandsOverride || entry.DeepnorthOverride) continue;
                cursor += entry.Weight;
                if (cursor >= roll)
                {
                    picked = entry.Name;
                    break;
                }
            }
            if (picked == null)
            {
                var last = entries[entries.Count - 1];
                if (!last.AshlandsOverride && !last.DeepnorthOverride) picked = last.Name;
            }
            foreach (var entry in entries)
            {
                if (entry.AshlandsOverride && ashlands) picked = entry.Name;
                if (entry.DeepnorthOverride && deepnorth) picked = entry.Name;
            }
            return string.IsNullOrEmpty(picked) ? null : picked;
        }

        /// <summary>
        /// The next periods at this biome, at most <see cref="MaxAhead"/>. <paramref name="rollForPeriod"/> is called with
        /// each future period index and must return that period's roll against the filtered list.
        /// </summary>
        public static IReadOnlyList<ForecastChange> Upcoming(long worldSeconds, long duration, int count, IReadOnlyList<ForecastEntry>? entries,
            bool ashlands, bool deepnorth, Func<long, float>? rollForPeriod, Func<string, bool>? allows, string? fallback)
        {
            if (count < 1 || rollForPeriod == null) return Array.Empty<ForecastChange>();
            if (count > MaxAhead) count = MaxAhead;
            var list = Filtered(entries, allows, fallback);
            if (list.Count == 0) return Array.Empty<ForecastChange>();
            var period = Period(worldSeconds, duration);
            var wait = SecondsUntilChange(worldSeconds, duration);
            var span = Duration(duration);
            var changes = new List<ForecastChange>(count);
            for (var step = 1; step <= count; step++)
            {
                var next = period + step;
                var name = Select(list, rollForPeriod(next), ashlands, deepnorth);
                if (string.IsNullOrEmpty(name)) break;
                changes.Add(new ForecastChange(next, wait + (step - 1) * span, name!));
            }
            return changes;
        }

        /// <summary>The integer the game passes to its random state for one wind octave. A zero step (duration shorter than the octave) is treated as 1.</summary>
        public static int WindSeed(long worldSeconds, long windPeriodDuration, int octave)
        {
            if (octave < 1) octave = 1;
            if (worldSeconds < 0) worldSeconds = 0;
            var step = windPeriodDuration / octave;
            if (step < 1) step = 1;
            return unchecked((int)(worldSeconds / step));
        }

        /// <summary>
        /// Wind at one moment from eight random values, two per octave (1, 2, 4, 8): angle, then the intensity push.
        /// The result is the environment's wind range, clamped the way the game clamps the target wind.
        /// </summary>
        public static (float Angle, float Intensity) Wind(float windMin, float windMax, IReadOnlyList<float>? values)
        {
            var angle = 0f;
            var intensity = 0.5f;
            var octaves = new[] { 1, 2, 4, 8 };
            for (var i = 0; i < octaves.Length; i++)
            {
                var octave = octaves[i];
                var turn = Value(values, i * 2);
                var push = Value(values, i * 2 + 1);
                angle += turn * ((float)Math.PI * 2f / octave);
                intensity += -0.5f / octave + push / octave;
            }
            if (float.IsNaN(windMin) || float.IsInfinity(windMin)) windMin = 0f;
            if (float.IsNaN(windMax) || float.IsInfinity(windMax)) windMax = windMin;
            // EnvMan uses Mathf.Lerp, which clamps the combined octave value before interpolation.
            if (intensity < 0f) intensity = 0f;
            if (intensity > 1f) intensity = 1f;
            intensity = windMin + (windMax - windMin) * intensity;
            if (intensity < 0.05f) intensity = 0.05f;
            if (intensity > 1f) intensity = 1f;
            return (angle, intensity);
        }

        public static string WindPoint(float angleRadians)
        {
            if (float.IsNaN(angleRadians) || float.IsInfinity(angleRadians)) return Compass.Point(0f);
            return Compass.Point(angleRadians * (180f / (float)Math.PI));
        }

        public static int WindPercent(float intensity)
        {
            if (float.IsNaN(intensity) || float.IsInfinity(intensity)) return 0;
            var percent = (int)Math.Round(intensity * 100d, MidpointRounding.AwayFromZero);
            if (percent < 0) return 0;
            if (percent > 100) return 100;
            return percent;
        }

        static float Value(IReadOnlyList<float>? values, int index)
        {
            if (values == null || index < 0 || index >= values.Count) return 0f;
            var value = values[index];
            return float.IsNaN(value) || float.IsInfinity(value) ? 0f : value;
        }
    }

    /// <summary>A missed optional-assembly lookup is cached. Tempest is loaded before gameplay, so one scan covers the session.</summary>
    public static class AssemblyScan
    {
        public static bool CacheMiss() => true;
    }
}
