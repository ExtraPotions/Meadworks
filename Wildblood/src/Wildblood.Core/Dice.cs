// Wildblood - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original. The percent convention follows EliteCreaturesReborn Util/Dice.cs (0 never fires, 100 always does).
using System;

namespace ExtraPotions.Wildblood
{
    /// <summary>
    /// Every roll in Wildblood.Core draws from this, so the rules are pure and testable with fixed draws. The plugin
    /// passes a source backed by UnityEngine.Random on the creature's owner.
    /// </summary>
    public interface IDice
    {
        /// <summary>A uniform draw in [0, 1).</summary>
        float Value();
    }

    public static class DiceExtensions
    {
        /// <summary>True with the given percentage (0 to 100): 0 never fires, 100 always does.</summary>
        public static bool Percent(this IDice dice, float chancePercent)
        {
            if (chancePercent <= 0f) return false;
            if (chancePercent >= 100f) { dice.Value(); return true; }
            return dice.Value() * 100f < chancePercent;
        }

        /// <summary>An integer in [min, maxExclusive); min when the range is empty.</summary>
        public static int Range(this IDice dice, int min, int maxExclusive)
        {
            if (maxExclusive <= min) { dice.Value(); return min; }
            var v = Math.Min(0.99999994f, Math.Max(0f, dice.Value()));
            return min + (int)Math.Floor(v * (maxExclusive - min));
        }

        /// <summary>An index drawn in proportion to its weight (weights at or below 0 never win); 0 when all are 0.</summary>
        public static int PickWeighted(this IDice dice, float[] weights)
        {
            if (weights == null) throw new ArgumentNullException(nameof(weights));
            var total = 0f;
            foreach (var w in weights) total += Math.Max(0f, w);
            var draw = Math.Min(0.99999994f, Math.Max(0f, dice.Value())) * total;
            if (total <= 0f) return 0;
            var running = 0f;
            var last = 0;
            for (var i = 0; i < weights.Length; i++)
            {
                if (weights[i] <= 0f) continue;
                last = i;
                running += weights[i];
                if (draw < running) return i;
            }
            return last;
        }
    }

    /// <summary>A seeded source for code that has no Unity random (tests, tools, the dedicated server's deterministic paths).</summary>
    public sealed class SeededDice : IDice
    {
        private readonly Random random;

        public SeededDice(int seed) { random = new Random(seed); }

        public float Value() => (float)random.NextDouble();
    }
}
