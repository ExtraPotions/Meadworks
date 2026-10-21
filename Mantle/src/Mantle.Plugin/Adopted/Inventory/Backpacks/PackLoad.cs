using System.Collections.Generic;
using HarmonyLib;
using Mantle.Core;
using Mantle.Layout;

namespace Mantle.Backpacks
{
    /// <summary>
    /// What lies in a worn pack's slots weighs less with the pack's level (<see cref="BackpackLevels.Load"/>): the game
    /// sums the inventory's weight after every change (<c>Inventory.UpdateTotalWeight</c>, called from <c>Changed</c>),
    /// and the postfix takes the pack's share off that sum, so the weight shown, encumbrance and the grave check all agree.
    /// The pack is looked up in its slot now, not through a cached watch, which hears of the change only after this sum.
    /// The local player's own inventory only; every client weighs its own player.
    /// </summary>
    [HarmonyPatch(typeof(Inventory), nameof(Inventory.UpdateTotalWeight))]
    public static class PackLoad
    {
        [HarmonyPostfix]
        public static void Postfix(Inventory __instance)
        {
            ItemDrop.ItemData pack = Backpack.InSlot(__instance);
            if (pack == null)
                return;
            float lighter = 1f - BackpackLevels.Load(pack.m_quality);
            if (lighter > 0f)
                __instance.m_totalWeight -= InPack(__instance, InventoryState.Layout) * lighter;
        }

        private static float InPack(Inventory inventory, InventoryLayout layout)
        {
            float weight = 0f;
            List<ItemDrop.ItemData> items = inventory.GetAllItems();
            for (int i = 0; i < items.Count; i++)
            {
                if (layout.IsPackCell(items[i].m_gridPos))
                    weight += items[i].GetWeight();
            }
            return weight;
        }
    }
}