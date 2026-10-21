using System.Collections.Generic;
using Mantle.Core;
using Mantle.Layout;
using Mantle.Slots;

namespace Mantle.Backpacks
{
    /// <summary>
    /// The backpack a player wears (the user's request, 2026-09-28, then one per biome): an item of Mantle's own, worn by
    /// lying in the Backpack slot. While it lies there the main grid has its slots at the bottom (<see cref="LayoutBuilder"/>,
    /// a partly used last row keeps its spare cells blocked), the player carries more (<see cref="CarryWeight"/>), and the
    /// player's ZDO names it, so every client hangs its model on that player's back (<see cref="BackpackMount"/>). It is worn
    /// by where it lies; being equipment, it also carries the game's equipped flag there (<see cref="BackpackEquip"/>).
    /// </summary>
    public static class Backpack
    {
        /// <summary>On a player's ZDO (int): the stable hash of the worn pack's prefab name, 0 for none. Written by its own client.</summary>
        public static readonly int WornKey = "Mantle.backpack".GetStableHashCode();

        private static readonly InventoryWatch wornWatch = new InventoryWatch();
        private static InventoryLayout wornLayout;
        private static BackpackKind wornKind;
        private static ItemDrop.ItemData wornItem;

        /// <summary>The kind the local player wears now, or null (also while Mantle or the backpacks are off).</summary>
        public static BackpackKind Worn(Player player) => BackpackCatalog.Of(WornItem(player));

        /// <summary>
        /// <see cref="Worn"/> for the carry weight, which the game asks every frame (<c>IsEncumbered</c>): the Backpack
        /// slot is looked at again only after the inventory changed (<see cref="InventoryWatch"/>) or the layout did,
        /// rather than the inventory searched on every call. The settings are read every time.
        /// </summary>
        public static BackpackKind WornNow(Player player) => Looked(player) ? wornKind : null;

        /// <summary><see cref="WornItem"/> kept the same way, for the movement modifier the game sums in every physics step.</summary>
        public static ItemDrop.ItemData WornItemNow(Player player) => Looked(player) ? wornItem : null;

        private static bool Looked(Player player)
        {
            if (!InventoryState.IsLocal(player) || !InventoryState.Active || !BackpackSettings.Active)
                return false;
            Inventory inventory = player.GetInventory();
            InventoryLayout layout = InventoryState.Layout;
            if (wornWatch.Changed(inventory) || !ReferenceEquals(layout, wornLayout))
            {
                wornLayout = layout;
                ItemDrop.ItemData item = ItemInSlot(inventory, layout);
                wornKind = BackpackCatalog.Of(item);
                wornItem = wornKind != null ? item : null;
            }
            return true;
        }

        /// <summary>The pack the local player wears now, or null (also while Mantle or the backpacks are off).</summary>
        public static ItemDrop.ItemData WornItem(Player player)
        {
            if (!InventoryState.IsLocal(player) || !InventoryState.Active || !BackpackSettings.Active)
                return null;
            return InSlot(player.GetInventory());
        }

        /// <summary>The pack in the Backpack slot of the local player's inventory, looked up now; null for any other inventory.</summary>
        public static ItemDrop.ItemData InSlot(Inventory inventory)
        {
            if (!InventoryState.Manages(inventory) || !BackpackSettings.Active)
                return null;
            ItemDrop.ItemData item = ItemInSlot(inventory, InventoryState.Layout);
            return BackpackCatalog.Of(item) != null ? item : null;
        }

        private static ItemDrop.ItemData ItemInSlot(Inventory inventory, InventoryLayout layout)
        {
            IReadOnlyList<Vector2i> cells = layout.CellsOf(SlotKind.Backpack);
            return cells.Count > 0 ? inventory.GetItemAt(cells[0].x, cells[0].y) : null;
        }

        /// <summary>
        /// The slots the worn pack adds to the next layout (its Deep Pockets included). The items still lie where the
        /// recorded layout put them, so the Backpack slot is looked up there.
        /// </summary>
        public static int SlotsFor(Player player) => BackpackSettings.SlotsOf(ItemInSlot(player.GetInventory(), LayoutRecord.Read(player)));
    }
}
