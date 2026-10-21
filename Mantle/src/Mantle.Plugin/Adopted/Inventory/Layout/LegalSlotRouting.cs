using HarmonyLib;
using Mantle.Core;
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
            if (next.y > 0 || next.x >= 8) return true;
            var legal = MainCells.LegalExtraCells(layout, item);
            if (SlotFill.TakeIn(__instance, item, legal)) { __result = true; return false; }
            return true;
        }
    }
}
