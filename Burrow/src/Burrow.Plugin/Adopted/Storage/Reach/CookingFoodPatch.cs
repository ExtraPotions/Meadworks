using System;
using HarmonyLib;

namespace Burrow.Reach
{
    /// <summary>
    /// Cooking stations and ovens add food through <c>CookingStation.OnInteract</c> (reached from Interact and
    /// from the oven's food switch). Done items are picked up first as in the game; otherwise, with no cookable
    /// item in the inventory, one unit is borrowed when the fire burns and a slot is free.
    /// </summary>
    [HarmonyPatch(typeof(CookingStation), nameof(CookingStation.OnInteract))]
    public static class CookingFoodPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(CookingStation __instance, Humanoid user, ref StationFeed.Loan __state, ref bool __result)
        {
            __state = null;
            if (!StationFeed.Wanted(user, __instance) || __instance.HaveDoneItem())
                return true;
            Func<ItemDrop.ItemData, bool> accepts = StationAccepts.CookingFood(__instance);
            if (StationFeed.PullHeld)
            {
                __result = StationFeed.Pull(user, accepts);
                return false;
            }
            if (__instance.FindCookableItem(user.GetInventory()) != null)
                return true;
            if ((__instance.m_requireFire && !__instance.IsFireLit()) || __instance.GetFreeSlot() == -1)
                return true;
            __state = StationFeed.Borrow(user, accepts);
            return true;
        }

        [HarmonyPostfix]
        public static void Postfix(CookingStation __instance, Humanoid user, StationFeed.Loan __state, bool __result)
        {
            StationFeed.Settle(user, __state);
            if (__result && StationFeed.Wanted(user, __instance) && !__instance.HaveDoneItem())
                StationFeed.Fill(__instance.m_nview, __instance.m_slots.Length, () => __instance.OnInteract(user));
        }
    }
}
