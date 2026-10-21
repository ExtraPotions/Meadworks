// Wildblood - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks).
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later, adapted from
// EliteCreaturesReborn/Loot/DropRoller.cs (ExtraRolls, Scaled), Rules/LootRules.cs (ExtraRollChanceAt) and the
// level-1 rule of Loot/LootEngine.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Wildblood namespace; IDice; a multiplier of 0 removes the drop (the cheat range starts at 0);
// pseudo-chance and bad-luck threshold helpers (design E7, F1).
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;

namespace ExtraPotions.Wildblood.Loot
{
    public static class LootMath
    {
        /// <summary>The game's own per-row quantity cap.</summary>
        public const int AmountCap = 100;

        /// <summary>Rows at or below this chance carry the game's bad-luck counter (design F1).</summary>
        public const float BadLuckThreshold = 0.3f;

        /// <summary>Percent chance of an extra roll at a star; the last entry repeats; an empty line means 100.</summary>
        public static float ExtraRollChanceAt(IReadOnlyList<float> line, int star)
        {
            if (line == null || line.Count == 0) return 100f;
            return line[Math.Min(line.Count - 1, Math.Max(0, star))];
        }

        /// <summary>Rolled: one gate per star, in order, so Max Extra Rolls (0 = no limit) caps the successes, not the attempts.</summary>
        public static int ExtraRolls(IReadOnlyList<float> extraRollChance, int maxExtraRolls, int stars, IDice dice)
        {
            var rolls = 0;
            for (var star = 1; star <= stars; star++)
            {
                if (maxExtraRolls > 0 && rolls >= maxExtraRolls) break;
                if (dice.Percent(ExtraRollChanceAt(extraRollChance, star))) rolls++;
            }
            return rolls;
        }

        /// <summary>A quantity scaled by a multiplier, never below one and at most the cap; a multiplier of 0 or less removes it.</summary>
        public static int Scaled(int amount, float multiplier)
        {
            if (amount <= 0 || !(multiplier > 0f)) return 0;
            return Math.Min(AmountCap, Math.Max(1, (int)Math.Round(amount * multiplier, MidpointRounding.AwayFromZero)));
        }

        /// <summary>
        /// The chance a pseudo-random row is evaluated at. Wildblood runs drop generation at level 1, so the bad-luck counter
        /// keeps the base chance as its key whatever the stars (design E7); the game multiplies by 2^(level-1).
        /// </summary>
        public static float PseudoChance(float baseChance, int level, bool levelMultiplier, bool wildbloodLoot)
        {
            if (wildbloodLoot || !levelMultiplier) return baseChance;
            return baseChance * (float)Math.Pow(2, Math.Max(0, level - 1));
        }

        public static bool BadLuckProtected(float chance) => chance > 0f && chance <= BadLuckThreshold;
    }
}
