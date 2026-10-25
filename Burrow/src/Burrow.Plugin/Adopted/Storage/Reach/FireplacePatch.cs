using System;
using HarmonyLib;
using UnityEngine;

namespace Burrow.Reach
{
    /// <summary>
    /// Fires and torches refuel through <c>Fireplace.Interact</c> (the hot tub is a <c>Smelter</c>, see SmelterFuelPatch). The game's own toggle (a fire that can
    /// be turned off, plain Use while burning) and hold repeat rules are kept; when the inventory has no fuel and
    /// the fire is not full, one unit is borrowed before the game looks. Fill never repeats the toggle path: a
    /// fire that can be turned off is only filled through the game's alt use, where the game itself refuels it.
    /// </summary>
    [HarmonyPatch(typeof(Fireplace), nameof(Fireplace.Interact))]
    public static class FireplacePatch
    {
        [HarmonyPrefix]
        public static bool Prefix(Fireplace __instance, Humanoid user, bool hold, bool alt, ref StationFeed.Loan __state, ref bool __result)
        {
            __state = null;
            if (!StationFeed.Wanted(user, __instance) || !CanRefuel(__instance) || Repeating(__instance, hold))
                return true;
            float fuel = __instance.m_nview.GetZDO().GetFloat(ZDOVars.s_fuel);
            if (Toggles(__instance, hold, alt, fuel))
                return true;
            Func<ItemDrop.ItemData, bool> accepts = StationAccepts.Fuel(__instance, __instance.m_fuelItem);
            if (!hold && StationFeed.PullHeld)
            {
                __result = StationFeed.Pull(user, accepts);
                return false;
            }
            string fuelName = __instance.m_fuelItem?.m_itemData?.m_shared?.m_name;
            if (string.IsNullOrEmpty(fuelName) || user == null || user.GetInventory() == null)
                return true;
            if (Mathf.CeilToInt(fuel) >= __instance.m_maxFuel || user.GetInventory().HaveItem(fuelName))
                return true;
            __state = StationFeed.Borrow(user, accepts);
            return true;
        }

        [HarmonyPostfix]
        public static void Postfix(Fireplace __instance, Humanoid user, bool hold, bool alt, StationFeed.Loan __state, bool __result)
        {
            StationFeed.Settle(user, __state);
            if (!__result || hold || !StationFeed.Wanted(user, __instance) || !CanRefuel(__instance))
                return;
            if (__instance.m_canTurnOff && !alt)
                return;
            StationFeed.Fill(__instance.m_nview, (int)__instance.m_maxFuel, () => __instance.Interact(user, false, alt));
        }

        private static bool CanRefuel(Fireplace fireplace)
        {
            return fireplace != null && fireplace.m_canRefill && !fireplace.m_infiniteFuel
                && fireplace.m_fuelItem != null && fireplace.m_fuelItem.m_itemData?.m_shared != null
                && fireplace.m_nview != null && fireplace.m_nview.IsValid() && fireplace.m_nview.GetZDO() != null;
        }

        /// <summary>The game's toggle rule: plain Use on a burning fire that can be turned off switches it, adds nothing.</summary>
        private static bool Toggles(Fireplace fireplace, bool hold, bool alt, float fuel)
        {
            return fireplace.m_canTurnOff && !hold && !alt && fuel > 0f;
        }

        /// <summary>The game's hold rule: a held Use only counts every repeat interval.</summary>
        private static bool Repeating(Fireplace fireplace, bool hold)
        {
            if (!hold)
                return false;
            return fireplace.m_holdRepeatInterval <= 0f || Time.time - fireplace.m_lastUseTime < fireplace.m_holdRepeatInterval;
        }
    }
}
