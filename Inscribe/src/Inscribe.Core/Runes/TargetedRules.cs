// Inscribe - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: which inscription the Honing Rune may change (batch 3; the Tempering Rune folded into it 2026-10-19), Unity-free for the portable tests.
namespace ExtraPotions.Inscribe.Runes
{
    public static class TargetedRules
    {
        /// <summary>
        /// Honing: the next stronger tier (grade + 1) when it exists, weighs more than 0, is unlocked at the item's level and
        /// is not one of the closed top tiers (an only-allowed class closes its strongest <paramref name="closed"/> tiers).
        /// </summary>
        public static bool CanHone(bool exists, float weight, int unlockLevel, int shown, int itemLevel, int closed) =>
            exists && weight > 0f && unlockLevel <= itemLevel && shown > closed;
    }
}
