// Wildblood - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks).
// Portions from CreatureManager 1.2.5 (sighsorry1029), GPL-3.0, adapted from CreatureLoot.cs (CalculateAmount) at
// 47e45a6e8c3b389a51e42fc7387d1a4fb90af473.
// Changed by expDARE, 2026: Wildblood namespace; the fractional draw comes from IDice instead of UnityEngine.Random.
// SPDX-License-Identifier: GPL-3.0-only
using System;

namespace ExtraPotions.Wildblood.Loot
{
    /// <summary>The Amount loot mode (design E1, CreatureManager CalculateChance): base chance kept, amounts scale by stars.</summary>
    public static class AmountScaling
    {
        // Chance-only rows keep native star-scaled chances and unscaled amounts.
        public static bool ScalesQuantity(bool levelMultiplier, bool chanceOnly) => levelMultiplier && !chanceOnly;

        /// <summary>
        /// Expected amount = amount x (1 + stars x percent / 100); the fractional remainder rounds up with that probability.
        /// Empty rolls stay empty; the game's per-row cap of 100 holds before and after.
        /// </summary>
        public static int CalculateAmount(int amount, int level, int percent, IDice dice)
        {
            if (amount <= 0 || amount >= 100) return Math.Min(amount, 100);
            long stars = Math.Max(0L, (long)level - 1);
            long hundredths = amount * (100L + stars * Math.Max(0, Math.Min(percent, 100)));
            int result = (int)Math.Min(100L, hundredths / 100);
            long remainder = hundredths % 100;
            if (result < 100 && remainder > 0 && dice.Value() < remainder / 100f) result++;
            return result;
        }
    }
}
