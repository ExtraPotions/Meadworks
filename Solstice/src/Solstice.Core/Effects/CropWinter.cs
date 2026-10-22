// Solstice - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: what winter does to a ripe crop. The plugin applies the harvest outcome.
using System;

namespace ExtraPotions.Solstice.Effects
{
    /// <summary>Remaining grace belongs to the crop; each owner measures only its locally observed real time.</summary>
    public sealed class CropFreezeTimer
    {
        bool observing;
        long previousBudget;
        double previousTime;

        // A positive budget encodes remaining milliseconds plus one, so zero still means no freeze.
        public long Advance(long budget, bool owner, double now)
        {
            if (!owner) { observing = false; return budget; }
            if (!observing || budget != previousBudget)
            {
                observing = true;
                previousBudget = budget;
                previousTime = now;
                return budget;
            }
            double elapsed = Math.Max(0d, now - previousTime);
            long spent = double.IsNaN(elapsed) || double.IsInfinity(elapsed) ? 0L : (long)(elapsed * 1000d);
            long next = budget > 0 ? Math.Max(1L, budget - spent) : 0L;
            previousBudget = next;
            previousTime = now;
            return next;
        }
    }

    /// <summary>What the owner of a crop should do on this check.</summary>
    public enum CropWinterAction { None, ClearFreeze, BeginFreeze, StayFreezing, Survive, Perish }

    /// <summary>Hover line for that check. <see cref="CropHover.Freezing"/> is the only one with a seconds argument.</summary>
    public enum CropHover { None, FrostResistant, HeatProtected, Survived, Freezing, WillPerish, Exposed }

    public readonly struct CropWinterDecision
    {
        public CropWinterDecision(CropWinterAction action, CropHover hover, int hoverSeconds)
        {
            Action = action;
            Hover = hover;
            HoverSeconds = hoverSeconds;
        }

        public CropWinterAction Action { get; }
        public CropHover Hover { get; }
        public int HoverSeconds { get; }
    }

    /// <summary>
    /// Winter perish for a pickable on the control list. A crop that survives stays ripe for the player to pick.
    /// A failed survival asks the plugin to drop the normal harvest. The roll is the caller's, so a test can force it.
    /// A roll that is not a number, or is outside 0 to 1, survives: a bad roll does not destroy the crop.
    /// </summary>
    public static class CropWinter
    {
        public const int DefaultPerishDay = 3;
        public const float DefaultFreezeSeconds = 120f;
        public const float DefaultHarvestChance = 0.33f;

        public static float FreezeWindow(float seconds) => seconds < 0f ? 0f : seconds;

        /// <summary>Unity names a spawned prefab with "(Clone)", with or without the space the editor adds.</summary>
        public static string PrefabName(string? name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            const string mark = "(Clone)";
            int at = name.IndexOf(mark, StringComparison.Ordinal);
            if (at < 0) return name;
            if (at > 0 && name[at - 1] == ' ') at--;
            return name.Substring(0, at);
        }

        public static bool SurvivedWinter(int savedDay, int anchorDay, int length) =>
            savedDay > anchorDay && (long)savedDay <= (long)anchorDay + length;

        public static CropWinterDecision Decide(
            bool perishEnabled, bool winterStopped, bool canBePicked, bool alreadyPicked, bool controlled,
            bool placeProtected, bool hardy, bool heatProtected, bool alreadySurvived,
            int dayInSeason, int perishFromDay, float freezeSeconds, float harvestChance,
            bool freezeStarted, double secondsRemaining, double roll)
        {
            bool involved = perishEnabled && winterStopped && controlled && canBePicked && !alreadyPicked;
            if (!involved) return freezeStarted ? Clear(CropHover.None) : None(CropHover.None);
            if (placeProtected) return freezeStarted ? Clear(CropHover.None) : None(CropHover.None);
            if (hardy) return freezeStarted ? Clear(CropHover.FrostResistant) : None(CropHover.FrostResistant);
            if (heatProtected) return freezeStarted ? Clear(CropHover.HeatProtected) : None(CropHover.HeatProtected);
            if (alreadySurvived) return freezeStarted ? Clear(CropHover.Survived) : None(CropHover.Survived);
            if (dayInSeason < perishFromDay) return freezeStarted ? Clear(CropHover.Exposed) : None(CropHover.Exposed);

            float window = FreezeWindow(freezeSeconds);
            if (window > 0f)
            {
                if (!freezeStarted) return new CropWinterDecision(CropWinterAction.BeginFreeze, CropHover.Freezing, WholeSeconds(window));
                if (secondsRemaining > 0d) return new CropWinterDecision(CropWinterAction.StayFreezing, CropHover.Freezing, WholeSeconds(secondsRemaining));
            }
            if (KeepsCrop(roll, harvestChance)) return new CropWinterDecision(CropWinterAction.Survive, CropHover.Survived, 0);
            return new CropWinterDecision(CropWinterAction.Perish, CropHover.WillPerish, 0);
        }

        /// <summary>Same inputs, same roll, in the range 0 inclusive to 1 exclusive.</summary>
        public static double Roll(long id, int worldDay)
        {
            unchecked
            {
                ulong x = (ulong)id * 0x9E3779B97F4A7C15UL ^ (uint)worldDay;
                x = (x ^ (x >> 30)) * 0xBF58476D1CE4E5B9UL;
                x = (x ^ (x >> 27)) * 0x94D049BB133111EBUL;
                x ^= x >> 31;
                return (x & 0xFFFFFF) / (double)0x1000000;
            }
        }

        static bool KeepsCrop(double roll, float harvestChance)
        {
            if (double.IsNaN(roll) || roll < 0d || roll >= 1d) return true;
            if (harvestChance <= 0f) return false;
            if (harvestChance >= 1f) return true;
            return roll < harvestChance;
        }

        static int WholeSeconds(double seconds) => (int)Math.Ceiling(seconds < 1d ? 1d : seconds);

        static CropWinterDecision None(CropHover hover) => new CropWinterDecision(CropWinterAction.None, hover, 0);

        static CropWinterDecision Clear(CropHover hover) => new CropWinterDecision(CropWinterAction.ClearFreeze, hover, 0);
    }
}
