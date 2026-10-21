// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using HarmonyLib;
using Mantle.Core;
using Mantle.Layout;
using Mantle.Slots;
using Mantle.Worn;

namespace Mantle.Accessories
{
    /// <summary>
    /// How an item tagged for a ring or belt slot is put on and taken off. The game must never wear it itself (a Megingjord
    /// would become the game's utility item and take a utility slot, or give its effect a second time beside the belt slot), so <c>EquipItem</c>
    /// refuses it, which also stops a saved character's load from wearing it (the game then clears its equipped flag). A right
    /// click on it in the player's own inventory screen puts it into a free cell of its kind (or swaps with the first), and
    /// from its cell takes it out to a free main cell, as for backpacks. The one exception is the game's own belt (the
    /// Megingjord), which <see cref="BeltWear"/> wears through the game's effects while it lies in the belt cell. Only while the layout has a cell of that kind
    /// (<see cref="AccessoryBonus.Claims"/>): with the slot off the item is the ordinary item the game knows.
    /// </summary>
    public static class AccessoryUse
    {
        [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.EquipItem))]
        public static class Equip
        {
            [HarmonyPrefix]
            [HarmonyPriority(Priority.First)]
            public static bool Prefix(Humanoid __instance, ItemDrop.ItemData item, ref bool __result)
            {
                if (!InventoryState.IsLocal(__instance) || !AccessoryBonus.Claims(item))
                    return true;
                if (ExtraUtilities.IsWorn(item))
                    return false;   // the worn game belt: the game's equip has nothing to add (the frame keeps it worn)
                item.m_equipped = false;
                __result = false;
                return false;
            }
        }

        [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.UseItem))]
        public static class Use
        {
            [HarmonyPrefix]
            public static bool Prefix(Humanoid __instance, Inventory inventory, ItemDrop.ItemData item, bool fromInventoryGui)
            {
                if (!InventoryState.IsLocal(__instance) || !AccessoryBonus.Claims(item))
                    return true;
                Inventory own = inventory ?? __instance.GetInventory();
                if (!InventoryState.Manages(own) || !own.ContainsItem(item))
                    return true;
                SlotKind kind = AccessoryItems.KindOf(item).Value;
                IReadOnlyList<Vector2i> cells = InventoryState.CellsOf(kind);
                if (!fromInventoryGui)
                    return IndexOf(cells, item.m_gridPos) < 0;   // a hotbar key on an item lying in its cell: the game's toggle would take a worn belt off for the frame to wear it again
                if (IndexOf(cells, item.m_gridPos) >= 0)
                    TakeOff(own, item);
                else
                    PutOn(own, item, cells);
                BeltWear.Reconcile(__instance, equipEffects: true);   // a game belt is worn or taken off at once
                return false;
            }
        }

        /// <summary>Into the first empty cell of its kind, else swapping with the first cell's item (which goes where this one lay).</summary>
        private static void PutOn(Inventory inventory, ItemDrop.ItemData item, IReadOnlyList<Vector2i> cells)
        {
            Vector2i target = cells[0];
            foreach (Vector2i cell in cells)
            {
                if (inventory.GetItemAt(cell.x, cell.y) == null)
                {
                    target = cell;
                    break;
                }
            }
            ItemDrop.ItemData there = inventory.GetItemAt(target.x, target.y);
            if (there != null && !InventoryState.Layout.IsMain(item.m_gridPos))
            {
                Messages.Center(Words.WrongSlot);   // the item to swap out could not go where this one lies
                return;
            }
            if (there != null)
                there.m_gridPos = item.m_gridPos;
            item.m_gridPos = target;
            inventory.Changed();
        }

        private static int IndexOf(IReadOnlyList<Vector2i> cells, Vector2i cell)
        {
            for (int i = 0; i < cells.Count; i++)
            {
                if (cells[i] == cell)
                    return i;
            }
            return -1;
        }

        private static void TakeOff(Inventory inventory, ItemDrop.ItemData item)
        {
            Vector2i free = MainCells.FindEmpty(inventory, InventoryState.Layout, topFirst: false);
            if (free.x < 0)
            {
                Messages.Center(AccessoryWords.NoRoom);
                return;
            }
            item.m_gridPos = free;
            inventory.Changed();
        }
    }
}
