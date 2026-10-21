// Kindle - ExtraPotions Meadworks crafting conveniences.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
namespace ExtraPotions.Kindle.Repair
{
    /// <summary>
    /// The crafting column under the game's Repair button (user, 2026-10-12 14:46): fixed slots one button height plus
    /// <see cref="Gap"/> apart at Repair's x, top to bottom Repair all (0), Enhance (1, Inscribe), Salvage (2), Find
    /// materials (3, Burrow) and Swap loadout (4, Mantle). A missing product leaves its slot empty; nothing moves up.
    /// </summary>
    public static class StationStack
    {
        public const float Gap = 6f;
        /// <summary>Height used when Repair has not been laid out yet (zero height).</summary>
        public const float FallbackHeight = 44f;

        public static float Y(float repairY, float repairHeight, int stepsBelow)
            => repairY - stepsBelow * ((repairHeight > 1f ? repairHeight : FallbackHeight) + Gap);

        /// <summary>Salvage's fixed slot.</summary>
        public const int SalvageSlot = 2;

        /// <summary>Wood showing round Repair on each side of its plate (the game's repair panel).</summary>
        public const float BackingMargin = 8f;

        /// <summary>
        /// The plate's size: centered directly behind Repair (user, 2026-10-14 13:39), Repair's size plus
        /// <see cref="BackingMargin"/> on every side; 80 x 80 round the game's 64 x 64 button.
        /// </summary>
        public static (float Width, float Height) Backing(float repairWidth, float repairHeight)
            => (repairWidth + 2f * BackingMargin, repairHeight + 2f * BackingMargin);
    }
}
