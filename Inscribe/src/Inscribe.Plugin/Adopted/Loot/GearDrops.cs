// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using ExtraPotions.Inscribe.Adopted.Config;

namespace ExtraPotions.Inscribe.Adopted.Loot
{
    /// <summary>
    /// Whether creatures and chests drop pre-rolled magic gear: the synced <c>Magic item drops</c> switch. Read on the
    /// peer that rolls the loot.
    /// </summary>
    internal static class GearDrops
    {
        public static bool On => ModSettings.MagicItemDrops.Value;
    }
}
