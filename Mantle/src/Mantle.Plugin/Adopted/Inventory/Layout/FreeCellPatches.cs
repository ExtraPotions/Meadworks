using System.Collections.Generic;
using HarmonyLib;
using Mantle.Backpacks;
using Mantle.Core;
using Mantle.Crafting;

namespace Mantle.Layout
{
    /// <summary>
    /// The game's room questions about the player's inventory answered for the main grid only (<see cref="MainCells"/>):
    /// where a new item goes, how many cells are free, whether an item fits. So pickups, crafting, trader purchases and
    /// the tombstone's easy-fit check never put anything into a slot, and "inventory full" means the grid is full.
    /// The hotbar stays the game's 8 cells of row 0 when the grid is wider (its keys are 1 to 8).
    /// </summary>
    public static class FreeCellPatches
    {
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.FindEmptySlot))]
        public static class FindEmpty
        {
            [HarmonyPrefix]
            public static bool Prefix(Inventory __instance, bool topFirst, ref Vector2i __result)
            {
                if (!InventoryState.Manages(__instance))
                    return true;
                if (!InPlaceUpgrade.FreeSlot(__instance, out __result))
                    __result = MainCells.FindEmpty(__instance, InventoryState.Layout, topFirst);
                return false;
            }
        }

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.GetEmptySlots))]
        public static class EmptyCount
        {
            [HarmonyPrefix]
            public static bool Prefix(Inventory __instance, ref int __result)
            {
                if (!InventoryState.Manages(__instance))
                    return true;
                __result = MainCells.CountEmptyFor(__instance, InventoryState.Layout, null);
                return false;
            }
        }

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.HaveEmptySlot))]
        public static class AnyEmpty
        {
            [HarmonyPrefix]
            public static bool Prefix(Inventory __instance, ref bool __result)
            {
                if (!InventoryState.Manages(__instance))
                    return true;
                __result = MainCells.CountEmptyFor(__instance, InventoryState.Layout, null) > 0;
                return false;
            }
        }

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.CanAddItem), new[] { typeof(ItemDrop.ItemData), typeof(int) })]
        public static class Fits
        {
            [HarmonyPrefix]
            public static bool Prefix(Inventory __instance, ItemDrop.ItemData item, int stack, ref bool __result)
            {
                if (!InventoryState.Manages(__instance))
                    return true;
                __result = InPlaceUpgrade.Fits(__instance, item) || MainCells.CanAdd(__instance, InventoryState.Layout, item, stack);
                return false;
            }
        }

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.GetBoundItems))]
        public static class Hotbar
        {
            [HarmonyPostfix]
            public static void Postfix(Inventory __instance, List<ItemDrop.ItemData> bound)
            {
                if (InventoryState.Manages(__instance))
                    bound.RemoveAll(item => item.m_gridPos.x >= InventorySettings.GameWidth);
            }
        }

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.GetHotbar))]
        public static class HotbarList
        {
            [HarmonyPostfix]
            public static void Postfix(Inventory __instance, List<ItemDrop.ItemData> __result)
            {
                if (InventoryState.Manages(__instance) && __result != null)
                    __result.RemoveAll(item => item != null && item.m_gridPos.x >= InventorySettings.GameWidth);
            }
        }
    }
}
