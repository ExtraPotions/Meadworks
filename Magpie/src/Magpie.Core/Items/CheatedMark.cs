// Magpie - ExtraPotions Meadworks loot handling.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using ExtraPotions.Magpie.StepAside;

namespace ExtraPotions.Magpie.Items
{
    /// <summary>
    /// Rules for Magpie's <c>magpie.cheated</c> ZDO marker (user decision 2026-10-07, V6). The game does not save
    /// <c>ItemData.m_cheated</c> in a ground item's ZDO, so a cheated drop reads clean after a reload. Magpie writes its
    /// own marker when such a drop is saved and restores the flag when the drop is loaded (which the game does before a
    /// pickup), so the flag survives reloads, pickups and merge identity checks.
    /// </summary>
    public static class CheatedMark
    {
        /// <summary>The modules whose work depends on the flag: pickup takes the item, merge compares it, loot clones it.</summary>
        public const MagpieModule Modules = MagpieModule.Pickup | MagpieModule.Merge | MagpieModule.Loot;

        /// <summary>
        /// The marker is written and read only while one of <see cref="Modules"/> is on for this session. The caller passes
        /// the modules that are on after the product switch, module switches and step-aside, so a product that is off or
        /// stepped aside for those modules leaves the game's own behavior untouched.
        /// </summary>
        public static bool Active(MagpieModule on) => (on & Modules) != 0;

        /// <summary>
        /// The marker value to write after the game saved a drop, or null for no write. Only a cheated drop without the
        /// marker gets it; clean drops get no extra data, and a stored marker is never cleared (a ground item cannot become
        /// legitimately clean, and clearing it would lose the flag of an item loaded while Magpie was inactive).
        /// </summary>
        public static int? OnSave(bool active, bool cheated, int stored)
        {
            if (!active || !cheated || stored == 1) return null;
            return 1;
        }

        /// <summary>The cheated flag after the game loaded a drop: set when the item has it or, while active, the marker is 1.</summary>
        public static bool OnLoad(bool active, bool cheated, int stored) => cheated || (active && stored == 1);
    }
}
