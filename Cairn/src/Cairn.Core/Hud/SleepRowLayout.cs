// Cairn - ExtraPotions Meadworks sleep row layout under the minimap.
// Portions Copyright (c) 2026 Pete Navarra (aka Vapok), BetterSleepBruh 2.1.0, MIT.
// Layout values adopted from BetterSleepBruh/Components/SleepHudView.cs at fe2bb81866e58f3c4afeb832876cba6ebf1e12cd
// https://github.com/Vapok/BetterSleepBruh. Full notice: THIRD_PARTY_NOTICES/BetterSleepBruh-MIT.txt.
// Changed by expDARE, 2026: only the row height, the gap under the minimap and the bonus text color are kept (the row is drawn
// on the game's own forged-iron plate look, as Mantle's HUD column squares); no code or art is used, and Cairn keeps its own moon and bed icons.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later AND MIT

namespace ExtraPotions.Cairn.Hud
{
    /// <summary>The sleep row under the minimap, in the minimap's UI units: its height, its gap under the map and the bonus text colour.</summary>
    public static class SleepRowLayout
    {
        public const float Height = 40f;
        /// <summary>Space between the minimap's bottom edge and the row.</summary>
        public const float Gap = 1f;
        /// <summary>The rest bonus text colour, red, green, blue.</summary>
        public static readonly byte[] BoostColour = { 255, 183, 91 };
    }
}
