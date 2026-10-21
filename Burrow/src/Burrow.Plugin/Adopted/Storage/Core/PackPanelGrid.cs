using System.Collections.Generic;
using System.Globalization;

namespace Burrow.Core
{
    /// <summary>The active layout's width|rows|blocked: PackPanel's custom data key, or the MeadWorks owner's published grid (meadworks.inventory.layout). Invalid active contracts protect every player cell.</summary>
    public static class PackPanelGrid
    {
        public const string Key = "PackPanel.mainGrid";
        // Adopted InventoryLayout permits width1..12, main rows0..128, with a partial final row.
        public static bool TryRead(Player player, out int mainWidth, out int mainRows, out int blockedCells)
        {
            mainWidth = mainRows = blockedCells = 0;
            if (player == null || player != Player.m_localPlayer || !Burrow.Plugin.IsReady
                || Burrow.Resources.NativeChestIdentity.World == 0 || !PackPanelLink.LaysOutInventory
                || PackPanelLink.Ambiguous) return false;
            int w, rows, blocked;
            if (PackPanelLink.OwnerLaysOut)
            {
                // The MeadWorks owner publishes its grid through Crucible (meadworks.inventory.layout); none yet is invalid.
                if (!PackPanelLink.TryOwnerGrid(out ExtraPotions.Meadworks.Contracts.InventoryLayout grid)) return false;
                w = grid.Width; rows = grid.Rows; blocked = grid.Blocked;
            }
            else
            {
                if (!player.m_customData.TryGetValue(Key, out string value)
                    || value == null || value.Length > 40) return false;
                string[] parts = value.Split('|');
                if (parts.Length != 3
                    || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out w)
                    || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out rows)
                    || !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out blocked)) return false;
            }
            if (w < 1 || w > 12 || rows < 0 || rows > 128 || blocked < 0 || blocked >= w
                || (rows == 0 && blocked != 0)) return false;
            Inventory inventory = player.GetInventory();
            if (inventory == null || inventory.GetWidth() != w || rows > inventory.GetHeight()) return false;
            mainWidth = w; mainRows = rows; blockedCells = blocked;
            return true;
        }

        /// <summary>Protected source cells, including occupied blocked cells; containers are independent.</summary>
        public static bool InSlot(Player player, Inventory inventory, ItemDrop.ItemData item)
        {
            if (item == null || player == null || inventory != player.GetInventory() || !PackPanelLink.LaysOutInventory)
                return false;
            if (!TryRead(player, out int w, out int rows, out int blocked)) return true;
            Vector2i cell = item.m_gridPos;
            return cell.x < 0 || cell.x >= w || cell.y < 0 || cell.y >= rows
                || (cell.y == rows - 1 && cell.x >= w - blocked);
        }

        public static IEnumerable<Vector2i> BlockedCells(Player player, Inventory inventory)
        {
            if (player == null || inventory != player.GetInventory() || !PackPanelLink.LaysOutInventory) yield break;
            if (!TryRead(player, out int w, out int rows, out int blocked))
            {
                for (int y = 0; y < inventory.GetHeight(); y++)
                    for (int x = 0; x < inventory.GetWidth(); x++) yield return new Vector2i(x, y);
                yield break;
            }
            for (int x = w - blocked; x < w; x++) yield return new Vector2i(x, rows - 1);
        }
    }
}
