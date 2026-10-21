// Ember - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original. BetterSleepBruh's SleepHudView.cs is not adopted; its observable behavior (five pillows, chevron
// step 0.35 s to 0.08 s, "+N%" label) is reimplemented here as display math for the kit HUD (design S11, section 6).
using System;
using System.Globalization;

namespace ExtraPotions.Ember.Sleep
{
    /// <summary>S11: display math for the sleep HUD. The label shows the actual share of the maximum boost.</summary>
    public static class SleepHud
    {
        public const int DefaultCompactAbove = 5;
        public const int MinCompactAbove = 1;
        public const int MaxCompactAbove = 20;
        public const double SlowestChevronStepSeconds = 0.35;
        public const double FastestChevronStepSeconds = 0.08;

        /// <summary>
        /// Boost label percent = 100 x extra rate / maximum extra rate (all but one in bed). This is the Meadworks fix
        /// of BetterSleepBruh's label, which used sleeping / players while the rate uses sleeping / (players - 1).
        /// </summary>
        public static double LabelPercent(double extraRate, double maxExtraRate) => 100.0 * BoostFraction(extraRate, maxExtraRate);

        /// <summary>Share of the maximum boost, 0..1; drives the chevron speed.</summary>
        public static double BoostFraction(double extraRate, double maxExtraRate)
        {
            if (!(maxExtraRate > 0.0) || !(extraRate > 0.0))
                return 0.0;
            return Math.Min(1.0, extraRate / maxExtraRate);
        }

        /// <summary>"+N%" with N rounded to a whole number; "+0%" for no boost.</summary>
        public static string Label(double percent)
        {
            if (!(percent > 0.0001))
                return "+0%";
            return "+" + percent.ToString("F0", CultureInfo.InvariantCulture) + "%";
        }

        /// <summary>Seconds per chevron step: 0.35 at no boost to 0.08 at full boost, linear in the boost fraction.</summary>
        public static double ChevronStepSeconds(double boostFraction)
        {
            double f = double.IsNaN(boostFraction) ? 0.0 : Math.Max(0.0, Math.Min(1.0, boostFraction));
            return SlowestChevronStepSeconds + (FastestChevronStepSeconds - SlowestChevronStepSeconds) * f;
        }

        /// <summary>The step (0..3) after the given seconds of boosting.</summary>
        public static int ChevronStep(double elapsedSeconds, double stepSeconds)
        {
            if (!(stepSeconds > 0.0) || !(elapsedSeconds > 0.0))
                return 0;
            return (int)(Math.Floor(elapsedSeconds / stepSeconds) % 4);
        }

        /// <summary>Which of the three chevrons are lit at a step: they fill in over steps 0 to 2 and all dim at step 3.</summary>
        public static bool[] ChevronLit(int step) => new[] { step >= 0 && step <= 2, step >= 1 && step <= 2, step == 2 };

        public static bool ChevronLit(int step, int index) => index >= 0 && index < 3 && step >= index && step <= 2;

        /// <summary>Above Compact Above players the HUD shows one pillow and the ratio text instead of one pillow per player.</summary>
        public static bool Compact(int players, int compactAbove) => players > Math.Max(MinCompactAbove, Math.Min(MaxCompactAbove, compactAbove));

        public static string Ratio(int sleeping, int players) =>
            sleeping.ToString(CultureInfo.InvariantCulture) + "/" + players.ToString(CultureInfo.InvariantCulture);

        /// <summary>Pillows lit (one per sleeper, at most the pillows shown).</summary>
        public static int LitPillows(int sleeping, int pillows) => Math.Max(0, Math.Min(pillows, sleeping));
    }

    public sealed class SleepHudCaption
    {
        string? template, text;
        int sleeping, players;
        double percent;

        public string Text(string format, int sleeping, int players, double percent)
        {
            var rounded = !(percent > 0.0001) ? 0 : Math.Round(percent, MidpointRounding.ToEven);
            if (text != null && template == format && this.sleeping == sleeping && this.players == players && this.percent == rounded) return text;
            template = format; this.sleeping = sleeping; this.players = players; this.percent = rounded;
            return text = string.Format(CultureInfo.InvariantCulture, format, sleeping, players) + "   " + SleepHud.Label(rounded);
        }
    }
}
