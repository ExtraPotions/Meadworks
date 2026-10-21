using System;
using HarmonyLib;
using Burrow.Core;

namespace Burrow.Reach
{
    /// <summary>
    /// The "From storage: n" line of station hover texts. Smelters and ovens hover through their switches
    /// (<c>Switch.GetHoverText</c>, which calls the station's own hover callback or a fixed text); fires,
    /// fermenters and plain cooking stations through their own <c>GetHoverText</c>.
    /// </summary>
    public static class StationHover
    {
        public static bool Active => ReachRules.Active(ReachMode.Stations);

        /// <summary>Feeding is on and the station's prefab is not disabled in the stations: map.</summary>
        public static bool Shows(UnityEngine.Component station) => Active && ReachRules.StationRuleFor(station).Enabled;

        public static string Line(Func<ItemDrop.ItemData, bool> accepts)
        {
            return "\n" + Language.Localize("$burrow_fromstorage") + " " + ReachCount.CountMatching(accepts);
        }

        /// <summary>What the switch's station accepts through that switch, or null for a switch that adds nothing
        /// or a station disabled in the stations: map.</summary>
        public static Func<ItemDrop.ItemData, bool> SwitchAccepts(Switch sw)
        {
            Smelter smelter = sw.GetComponentInParent<Smelter>();
            if (smelter != null)
            {
                if (!ReachRules.StationRuleFor(smelter).Enabled)
                    return null;
                if (smelter.m_addOreSwitch == sw)
                    return StationAccepts.SmelterOre(smelter);
                return smelter.m_addWoodSwitch == sw && smelter.m_fuelItem != null ? StationAccepts.Fuel(smelter, smelter.m_fuelItem) : null;
            }
            CookingStation station = sw.GetComponentInParent<CookingStation>();
            if (station == null || !ReachRules.StationRuleFor(station).Enabled)
                return null;
            if (station.m_addFoodSwitch == sw)
                return StationAccepts.CookingFood(station);
            return station.m_addFuelSwitch == sw && station.m_fuelItem != null ? StationAccepts.Fuel(station, station.m_fuelItem) : null;
        }
    }

    [HarmonyPatch(typeof(Switch), nameof(Switch.GetHoverText))]
    public static class SwitchHoverPatch
    {
        [HarmonyPostfix]
        public static void Postfix(Switch __instance, ref string __result)
        {
            if (!StationHover.Active || string.IsNullOrEmpty(__result))
                return;
            Func<ItemDrop.ItemData, bool> accepts = StationHover.SwitchAccepts(__instance);
            if (accepts != null)
                __result += StationHover.Line(accepts);
        }
    }

    [HarmonyPatch(typeof(Fireplace), nameof(Fireplace.GetHoverText))]
    public static class FireplaceHoverPatch
    {
        [HarmonyPostfix]
        public static void Postfix(Fireplace __instance, ref string __result)
        {
            if (!StationHover.Shows(__instance) || string.IsNullOrEmpty(__result) || !__instance.m_canRefill || __instance.m_infiniteFuel || __instance.m_fuelItem == null)
                return;
            __result += StationHover.Line(StationAccepts.Fuel(__instance, __instance.m_fuelItem));
        }
    }

    [HarmonyPatch(typeof(Fermenter), nameof(Fermenter.GetHoverText))]
    public static class FermenterHoverPatch
    {
        [HarmonyPostfix]
        public static void Postfix(Fermenter __instance, ref string __result)
        {
            if (!StationHover.Shows(__instance) || string.IsNullOrEmpty(__result) || __instance.GetContent() != 0)
                return;
            if (!PrivateArea.CheckAccess(__instance.transform.position, 0f, false))
                return;
            __result += StationHover.Line(StationAccepts.FermenterBase(__instance));
        }
    }

    [HarmonyPatch(typeof(CookingStation), nameof(CookingStation.GetHoverText))]
    public static class CookingStationHoverPatch
    {
        [HarmonyPostfix]
        public static void Postfix(CookingStation __instance, ref string __result)
        {
            if (!StationHover.Shows(__instance) || string.IsNullOrEmpty(__result))
                return;
            __result += StationHover.Line(StationAccepts.CookingFood(__instance));
        }
    }
}
