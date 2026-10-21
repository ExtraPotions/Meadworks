using System.Collections.Generic;
using HarmonyLib;
using Mantle.Core;
using Mantle.Layout;
using Mantle.Slots;

namespace Mantle.Backpacks
{
    /// <summary>
    /// Right click on one of Mantle's backpacks in the player's own inventory wears it or takes it off, as the game does
    /// for armour: from the grid the game's EquipItem wears it (<see cref="BackpackEquip"/>) and it goes into the Backpack
    /// slot (what lay there, the pack worn before, takes its cell), from the slot it moves into a free cell of the grid
    /// above the backpack's own rows, which go with it, and comes off. The game's own toggle would take it off into any
    /// cell, its own rows included. Only from the inventory screen; a hotbar key equips it through the game's toggle.
    /// </summary>
    [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.UseItem))]
    public static class BackpackUse
    {
        [HarmonyPrefix]
        public static bool Prefix(Humanoid __instance, Inventory inventory, ItemDrop.ItemData item, bool fromInventoryGui)
        {
            if (!fromInventoryGui || BackpackCatalog.Of(item) == null || !InventoryState.IsLocal(__instance))
                return true;
            Inventory own = inventory ?? __instance.GetInventory();
            IReadOnlyList<Vector2i> cells = InventoryState.CellsOf(SlotKind.Backpack);
            if (!InventoryState.Manages(own) || cells.Count == 0 || !own.ContainsItem(item))
                return true;
            if (item.m_gridPos == cells[0])
                TakeOff(__instance, own, item);
            else if (!__instance.EquipItem(item))
                PutOn(own, item, cells[0]);
            return false;
        }

        /// <summary>Into the slot without wearing it, where the backpacks are off: the slot then only holds a pack.</summary>
        private static void PutOn(Inventory inventory, ItemDrop.ItemData item, Vector2i slot)
        {
            ItemDrop.ItemData there = inventory.GetItemAt(slot.x, slot.y);
            if (there != null)
                there.m_gridPos = item.m_gridPos;
            item.m_gridPos = slot;
            inventory.Changed();
        }

        /// <summary>
        /// Bottom row first, as the game places what it picks up, skipping the cells that leave with the backpack. No room:
        /// the pack stays on and nothing changes.
        /// </summary>
        private static void TakeOff(Humanoid humanoid, Inventory inventory, ItemDrop.ItemData item)
        {
            Vector2i cell = MainCells.FindEmptyOffPack(inventory, InventoryState.Layout);
            if (cell.x < 0)
            {
                Messages.Center(BackpackWords.NoRoom);
                return;
            }
            item.m_gridPos = cell;
            inventory.Changed();
            humanoid.UnequipItem(item);
        }
    }
}
