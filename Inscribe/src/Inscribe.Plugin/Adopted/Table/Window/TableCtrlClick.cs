// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.10.0 at d3b57606cb24775d61b7bda244b22eb89e7e7717.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using HarmonyLib;

namespace ExtraPotions.Inscribe.Adopted.Tables.Window
{
    /// <summary>
    /// Ctrl + click on a rune or Essence in the player's own inventory while a Rune Table is open puts the stack into the
    /// table (user 2026-10-07). It runs before every other click prefix: OpenKeep's Route Modifier (Ctrl + click too) would
    /// send the same stack to a chest. The clicked item is cleared, so every later prefix sees nothing to move, and the
    /// game's own handling is skipped. Local player only; the table's write happens on this machine, which owns the table
    /// while its window is open.
    /// </summary>
    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.OnSelectedItem))]
    internal static class TableCtrlClick
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First + 100)]
        private static bool Prefix(InventoryGui __instance, InventoryGrid grid, ref ItemDrop.ItemData item, InventoryGrid.Modifier mod)
        {
            if (!TableWindow.IsOpen || mod != InventoryGrid.Modifier.Move || item == null || __instance.m_dragGo != null
                || grid != __instance.m_playerGrid || (RuneStash.StoneIdOf(item) == null && !EssenceItem.Is(item)))
            {
                return true;
            }
            ItemDrop.ItemData clicked = item;
            TableWindow.Run(view => Store(view, clicked));
            item = null!;
            return false;
        }

        private static void Store(TableView view, ItemDrop.ItemData clicked)
        {
            int stored = RuneStash.StoreStack(view.Store, view.Inventory, clicked);
            if (stored > 0)
            {
                view.Player.Message(MessageHud.MessageType.TopLeft, Text.Words.Localize("$ecf_table_stored", stored.ToString()));
            }
        }
    }
}
