using System.Collections.Generic;
using System;
using HarmonyLib;
using Mantle.Core;

namespace Mantle.Slots
{
    /// <summary>
    /// Coins added to the player's inventory without a cell (pickups, a trader's change, take all from a chest) go
    /// into the purse first: into the empty purse whole, or onto its stack up to the stack size. What the purse cannot
    /// hold goes on through the game's own add (another coin stack, then a free main cell). The purse is an ordinary
    /// cell of the inventory, so the trader counts and takes its coins with the rest.
    /// </summary>
    [HarmonyPatch(typeof(Inventory), nameof(Inventory.AddItem), new[] { typeof(ItemDrop.ItemData) })]
    public static class CoinPurse
    {
        /// <summary>An add another mod's prefix already made is left alone, or the same item would also land in the purse.</summary>
        [HarmonyPrefix]
        public static bool Prefix(Inventory __instance, ItemDrop.ItemData item, ref bool __result, bool __runOriginal)
        {
            if (!__runOriginal)
                return false;
            if (item == null || !InventoryState.Manages(__instance) || !SlotRules.IsCoins(item) || !TakeIn(__instance, item))
                return true;
            __result = true;
            return false;
        }

        /// <summary>True when the whole stack went into the purse; a part may have gone in when false.</summary>
        private static bool TakeIn(Inventory inventory, ItemDrop.ItemData item)
        {
            IReadOnlyList<Vector2i> cells = InventoryState.CellsOf(SlotKind.Purse);
            if (cells.Count == 0)
                return false;
            ItemDrop.ItemData purse = inventory.GetItemAt(cells[0].x, cells[0].y);
            if (purse == null)
            {
                item.m_gridPos = cells[0];
                inventory.m_inventory.Add(item);
                inventory.Changed();
                return true;
            }
            if (!SlotRules.IsCoins(purse) || purse.m_quality != item.m_quality || purse.m_worldLevel != item.m_worldLevel)
                return false;
            int moved = Math.Min(purse.m_shared.m_maxStackSize - purse.m_stack, item.m_stack);
            if (moved <= 0)
                return false;
            purse.m_stack += moved;
            item.m_stack -= moved;
            inventory.Changed();
            return item.m_stack <= 0;
        }
    }
}
