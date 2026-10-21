using System;
using Burrow.Core;

namespace Burrow.Stow
{
    /// <summary>
    /// The part of the player inventory the bulk actions work on: the rows the game gives the player (its
    /// <c>invrows</c> key: 4, more once rows are bought from the trader), or the first <c>Main Inventory Rows</c>
    /// rows when that is set, without the hotbar (row 0). Mods that add equipment, food or ammo slots keep them in
    /// rows below the game's; quick stack, store all, dump and sort take nothing from those rows or the hotbar and
    /// the sort puts nothing there. Top up still fills the stacks there. Every row of a container is main grid.
    /// </summary>
    public static class MainGrid
    {
        private const int GameDefaultRows = 4;

        /// <summary>The first row the bulk actions touch: 1 in the player inventory (row 0 is the hotbar), else 0.</summary>
        public static int FirstRow(Player player, Inventory inventory)
        {
            return player != null && inventory == player.GetInventory() ? 1 : 0;
        }

        public static int Rows(Player player, Inventory inventory)
        {
            int height = inventory.GetHeight();
            if (player == null || inventory != player.GetInventory())
                return height;
            if (player != Player.m_localPlayer || !Burrow.Plugin.IsReady
                || Burrow.Resources.NativeChestIdentity.World == 0) return 0;
            if (PackPanelLink.LaysOutInventory)
            {
                if (!PackPanelGrid.TryRead(player, out _, out int mainRows, out _)) return 0;
                int configured = StowSettings.MainInventoryRows.Value;
                return configured > 0 ? Math.Min(mainRows, configured) : mainRows;
            }
            int rows = StowSettings.MainInventoryRows.Value;
            return Math.Min(height, rows > 0 ? rows : GameRows(player));
        }

        /// <summary>The player's main grid: PackPanel's rows while it lays the inventory out, else the game's.</summary>
        private static int GameRows(Player player)
        {
            if (PackPanelGrid.TryRead(player, out _, out int mainRows, out _))
                return mainRows;
            if (player.TryGetUniqueKeyValue(Player.InventoryRowsKey, out string value) && int.TryParse(value, out int rows))
                return rows;
            return GameDefaultRows;
        }
    }
}
