using System;
using HarmonyLib;
using Mantle.Core;

namespace Mantle.Ring
{
    /// <summary>
    /// Keys added to the player's inventory without a cell (pickups, a key made at the galdr table, a trader, a click move
    /// from a chest) go into their ring cell first, the way coins go to the purse: into the empty cell whole, or onto the
    /// key already there up to the stack size when it is the same world level. What the cell cannot hold goes on through
    /// the game's own add (another stack of that key, then a free main cell). A take all is sorted out after it
    /// (<see cref="Slots.TakeAllRouting"/>).
    /// </summary>
    [HarmonyPatch(typeof(Inventory), nameof(Inventory.AddItem), new[] { typeof(ItemDrop.ItemData) })]
    public static class KeyRouting
    {
        /// <summary>An add another mod's prefix already made is left alone, or the same item would also land on the ring.</summary>
        [HarmonyPrefix]
        public static bool Prefix(Inventory __instance, ItemDrop.ItemData item, ref bool __result, bool __runOriginal)
        {
            if (!__runOriginal)
                return false;
            if (item == null || !InventoryState.Manages(__instance) || !TakeIn(__instance, item))
                return true;
            __result = true;
            return false;
        }

        /// <summary>
        /// Moves what fits of a key into its ring cell; true when none is left outside it. The key is either not in the
        /// inventory yet (an add) or in a main cell (a take all): an empty ring cell takes it whole, as the same item.
        /// </summary>
        public static bool TakeIn(Inventory inventory, ItemDrop.ItemData item)
        {
            Vector2i cell = KeyRing.CellOf(item);
            if (cell.x < 0)
                return false;
            ItemDrop.ItemData there = inventory.GetItemAt(cell.x, cell.y);
            if (there == null)
            {
                Place(inventory, item, cell);
                return true;
            }
            if (there == item || !there.IsSameType(item))
                return false;
            int moved = Math.Min(there.m_shared.m_maxStackSize - there.m_stack, item.m_stack);
            if (moved <= 0)
                return false;
            there.m_stack += moved;
            item.m_stack -= moved;
            inventory.Changed();
            return item.m_stack <= 0;
        }

        private static void Place(Inventory inventory, ItemDrop.ItemData item, Vector2i cell)
        {
            item.m_gridPos = cell;
            if (!inventory.ContainsItem(item))
                inventory.m_inventory.Add(item);
            inventory.Changed();
        }
    }
}
