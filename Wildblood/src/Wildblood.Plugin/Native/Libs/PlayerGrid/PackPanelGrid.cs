// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, ValheimModLibs, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System.Collections.Generic;

namespace ExtraPotions.Wildblood.AdoptedLibs.PlayerGrid
{
    /// <summary>
    /// The reading side: the local player's main grid as PackPanel publishes it (<see cref="GridContract"/>). Every row
    /// below the main rows is PackPanel's slots (worn gear, utilities, food, mead, ammo, the purse, the key ring, the
    /// tacklebox), which a consumer's bulk moves, sorts and thefts leave alone. Trusted only while PackPanel lays the
    /// inventory out: the key is saved with the character and outlives the mod.
    /// </summary>
    public static class PackPanelGrid
    {
        private static string? parsedFrom;
        private static int width, rows, blocked;

        /// <summary>The player's main grid while PackPanel lays it out; false otherwise.</summary>
        public static bool TryRead(Player player, out int mainWidth, out int mainRows, out int blockedCells)
        {
            mainWidth = mainRows = blockedCells = 0;
            if (player == null || !PackPanelLink.LaysOutInventory || !player.m_customData.TryGetValue(GridContract.Key, out string value))
                return false;
            if (!ReferenceEquals(value, parsedFrom) && !Parse(value))
                return false;
            mainWidth = width;
            mainRows = rows;
            blockedCells = blocked;
            return true;
        }

        /// <summary>
        /// How many rows from the top are the player's own grid, for a caller that must never touch a slot: every row
        /// (<see cref="int.MaxValue"/>) while PackPanel does not lay the inventory out, its main rows while it does, and
        /// false when it does but its grid cannot be read, so the caller can hold back rather than guess.
        /// </summary>
        public static bool TryMainRows(Player player, out int mainRows)
        {
            mainRows = int.MaxValue;
            return !PackPanelLink.LaysOutInventory || TryRead(player, out _, out mainRows, out _);
        }

        /// <summary>Whether an item of the local player's own inventory lies below the main grid, in a PackPanel slot.</summary>
        public static bool InSlot(Player player, Inventory inventory, ItemDrop.ItemData item) =>
            item != null && player != null && inventory == player.GetInventory()
            && TryRead(player, out _, out int mainRows, out _) && item.m_gridPos.y >= mainRows;

        /// <summary>The cells of the bottom main row that hold nothing (a backpack's partly used last row); else none.</summary>
        public static IEnumerable<Vector2i> BlockedCells(Player player, Inventory inventory)
        {
            if (player == null || inventory != player.GetInventory() || !TryRead(player, out int w, out int r, out int b))
                yield break;
            for (int x = w - b; x < w; x++)
                yield return new Vector2i(x, r - 1);
        }

        private static bool Parse(string value)
        {
            if (!GridContract.TryParse(value, out int w, out int r, out int b))
                return false;
            width = w;
            rows = r;
            blocked = b;
            parsedFrom = value;
            return true;
        }
    }
}
