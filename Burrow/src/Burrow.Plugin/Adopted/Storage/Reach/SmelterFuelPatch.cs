using System;
using HarmonyLib;

namespace Burrow.Reach
{
    /// <summary>The smelter's add fuel switch (<c>Smelter.OnAddFuel</c>): one fuel unit is borrowed when the inventory has none.</summary>
    [HarmonyPatch(typeof(Smelter), nameof(Smelter.OnAddFuel))]
    public static class SmelterFuelPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(Smelter __instance, Humanoid user, ItemDrop.ItemData item, ref StationFeed.Loan __state, ref bool __result)
        {
            __state = null;
            if (item != null || !StationFeed.Wanted(user, __instance) || __instance.m_fuelItem == null)
                return true;
            Func<ItemDrop.ItemData, bool> accepts = StationAccepts.Fuel(__instance, __instance.m_fuelItem);
            if (StationFeed.PullHeld)
            {
                __result = StationFeed.Pull(user, accepts);
                return false;
            }
            if (__instance.GetFuel() > __instance.m_maxFuel - 1 || user.GetInventory().HaveItem(__instance.m_fuelItem.m_itemData.m_shared.m_name))
                return true;
            __state = StationFeed.Borrow(user, accepts);
            return true;
        }

        [HarmonyPostfix]
        public static void Postfix(Smelter __instance, Switch sw, Humanoid user, ItemDrop.ItemData item, StationFeed.Loan __state, bool __result)
        {
            StationFeed.Settle(user, __state);
            if (item == null && __result && StationFeed.Wanted(user, __instance))
                StationFeed.Fill(__instance.m_nview, __instance.m_maxFuel, () => __instance.OnAddFuel(sw, user, null));
        }
    }
}
