// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.10.0 at d3b57606cb24775d61b7bda244b22eb89e7e7717.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using HarmonyLib;

namespace ExtraPotions.Inscribe.Adopted.Tables.Window
{
    /// <summary>
    /// Ties the Rune Table's window to the game's inventory screen: it closes when the screen hides or when the screen
    /// is shown for anything else (a chest, the inventory key), and it is checked and redrawn on the screen's own update.
    /// Local player only.
    /// </summary>
    [HarmonyPatch]
    internal static class TableWindowPatches
    {
        [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Hide))]
        [HarmonyPostfix]
        private static void Hidden() => TableWindow.CloseFading();

        [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Show))]
        [HarmonyPrefix]
        private static void Showing() => TableWindow.BeforeShow();

        [HarmonyPatch(typeof(InventoryGui), "Update")]
        [HarmonyPostfix]
        private static void Updated() => TableWindow.Tick();
    }
}
