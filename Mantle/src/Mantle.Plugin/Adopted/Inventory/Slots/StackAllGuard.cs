using System.Collections.Generic;
using HarmonyLib;
using Mantle.Core;

namespace Mantle.Slots
{
    /// <summary>
    /// The game's Place stacks (<c>Inventory.StackAll</c>, from <c>InventoryGui.OnStackAll</c> and the container's own
    /// stack request) moves every unworn stack whose kind the chest already holds from anywhere in the player's
    /// inventory, so the food, mead, ammo, coins and keys in the slots went into the chest (found by the key ring
    /// session, 2026-09-28). Like OpenKeep's quick stack it now works on the grid only: the prefix takes the unworn items
    /// of the slot cells out of the player inventory's list for the length of the call, and the finalizer puts them back,
    /// also after an exception. Worn items stay in the list, as the game never moves them and taking them out could let
    /// the change callback unequip them. Local player only. A shared chest another player is using goes through
    /// OpenKeep's own request, whose candidates skip the rows below the main grid it reads from
    /// <see cref="Layout.GridContract"/>.
    /// </summary>
    [HarmonyPatch(typeof(Inventory), nameof(Inventory.StackAll))]
    public static class StackAllGuard
    {
        [HarmonyPrefix]
        public static void Prefix(Inventory fromInventory, out List<ItemDrop.ItemData> __state)
        {
            __state = InventoryState.Manages(fromInventory) ? UnwornSlotItems(fromInventory) : null;
            if (__state == null)
                return;
            foreach (ItemDrop.ItemData item in __state)
                fromInventory.m_inventory.Remove(item);
        }

        [HarmonyFinalizer]
        public static void Finalizer(Inventory fromInventory, List<ItemDrop.ItemData> __state)
        {
            if (__state == null || __state.Count == 0)
                return;
            foreach (ItemDrop.ItemData item in __state)
            {
                if (!fromInventory.ContainsItem(item))
                    fromInventory.m_inventory.Add(item);
            }
            fromInventory.Changed();
        }

        /// <summary>Whether the item lies in a slot cell of the local player's inventory (not the grid).</summary>
        public static bool InSlot(Inventory inventory, ItemDrop.ItemData item) =>
            item != null && InventoryState.SlotAt(inventory, item.m_gridPos) != null;

        private static List<ItemDrop.ItemData> UnwornSlotItems(Inventory inventory)
        {
            List<ItemDrop.ItemData> found = new List<ItemDrop.ItemData>();
            foreach (ItemDrop.ItemData item in inventory.GetAllItems())
            {
                if (InSlot(inventory, item) && !item.m_equipped)
                    found.Add(item);
            }
            return found;
        }
    }
}
