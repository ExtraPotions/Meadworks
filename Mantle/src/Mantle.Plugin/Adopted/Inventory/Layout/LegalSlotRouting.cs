using HarmonyLib;
using Mantle.Core;
using Mantle.Crafting;
using Mantle.Slots;

namespace Mantle.Layout
{
    [HarmonyPatch(typeof(Inventory), nameof(Inventory.AddItem), new[] { typeof(ItemDrop.ItemData) })]
    public static class LegalSlotRouting
    {
        [HarmonyPrefix, HarmonyPriority(Priority.Last)]
        public static bool Prefix(Inventory __instance, ItemDrop.ItemData item, ref bool __result, bool __runOriginal)
        {
            if (!__runOriginal || item == null || !InventoryState.Manages(__instance)) return true;
            var layout = InventoryState.Layout;
            var next = MainCells.FindEmpty(__instance, layout, false);
            if (next.y > 0 || next.x >= 8 || (next.x >= 0 && InPlaceUpgrade.IsCrafting)) return true;
            var legal = MainCells.LegalExtraCells(layout, item);
            if (legal.Count == 0) return true;
            int initial = item.m_stack;
            if (SlotFill.TakeIn(__instance, item, legal)) { __result = true; return false; }
            if (item.m_stack < initial)
            {
                var empty = MainCells.FindEmpty(__instance, layout, false);
                bool placed = empty.x >= 0 && empty.y >= 0;
                if (placed)
                {
                    item.m_gridPos = empty;
                    if (!__instance.ContainsItem(item))
                        __instance.m_inventory.Add(item);
                }
                __instance.Changed();
                // Like the game's own partial add: false with the rest still on item.m_stack, so callers return or drop it.
                __result = placed;
                return false;
            }
            return true;
        }
    }
}
