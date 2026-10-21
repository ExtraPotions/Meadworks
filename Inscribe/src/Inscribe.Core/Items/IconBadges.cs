// Inscribe - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: what the ground glint shows since enhancement was removed (user decision 2026-10-19: glint only on
// Legendary items). The icon badge shows nothing: the game already draws the quality level in each slot's top-right corner.
using System;
using ExtraPotions.Inscribe.Rarity;

namespace ExtraPotions.Inscribe.Items
{
    public static class IconBadges
    {
        /// <summary>The ground glint: Legendary items only, while 7 - Ground Glow / Legendary Glint is on.</summary>
        public static bool Glint(bool enabled, string? rarityId) =>
            enabled && string.Equals(rarityId, RarityTiers.Legendary, StringComparison.Ordinal);
    }
}
