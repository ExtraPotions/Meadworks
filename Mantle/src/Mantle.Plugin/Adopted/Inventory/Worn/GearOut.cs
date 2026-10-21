using Mantle.Core;
using Mantle.Layout;
using Mantle.Slots;

namespace Mantle.Worn
{
    /// <summary>
    /// Where a piece goes when it comes off: a free main cell (bottom row first, as the game places what it picks up; for
    /// a backpack none of its own cells, which go with it). With none it stays in its slot, never on the ground (the
    /// user's rule, 2026-10-06: "if inventory is full don't drop the item. allow it to stay in the gear"; with Auto Equip
    /// it then stays off, <see cref="GearKeep.LeaveOff"/>).
    /// </summary>
    public static class GearOut
    {
        public static Vector2i FreeCell(Inventory inventory, SlotKind kind) => kind == SlotKind.Backpack
            ? MainCells.FindEmptyOffPack(inventory, InventoryState.Layout)
            : MainCells.FindEmpty(inventory, InventoryState.Layout, topFirst: false);

        public static bool HasRoom(Inventory inventory, SlotKind kind) => FreeCell(inventory, kind).x >= 0;

        /// <summary>Out of its worn slot into a free main cell; with none it stays where it is (false).</summary>
        public static bool Away(Humanoid humanoid, ItemDrop.ItemData item, SlotKind kind)
        {
            Inventory inventory = humanoid.GetInventory();
            Vector2i free = FreeCell(inventory, kind);
            if (free.x < 0)
                return false;
            item.m_gridPos = free;
            inventory.Changed();
            return true;
        }

        /// <summary>Whether the item lies, unworn, in a worn slot of its own kind in the local player's inventory.</summary>
        public static bool LiesUnworn(Humanoid humanoid, ItemDrop.ItemData item, out SlotKind kind)
        {
            kind = SlotKind.Retired;
            if (!humanoid.GetInventory().ContainsItem(item) || humanoid.IsItemEquiped(item))
                return false;
            Slot slot = InventoryState.Layout.SlotAt(item.m_gridPos);
            if (slot == null || !SlotRules.IsWorn(slot.Kind) || SlotRules.WornKindOf(item) != slot.Kind)
                return false;
            kind = slot.Kind;
            return true;
        }
    }
}
