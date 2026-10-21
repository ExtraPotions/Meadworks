using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Mantle.Backpacks
{
    /// <summary>
    /// A backpack's upgrade asks the game's station level, one more per level (<c>Recipe.GetRequiredStationLevel</c>), but
    /// never more than its station can reach: the Moosehide Pack, made at black forge level 4, would ask level 7 for its
    /// last upgrade of a forge that stops at 6. The highest level is 1 plus each kind of extension built for the station
    /// (the game counts one of each, <c>StationExtension</c>), found among ZNetScene's prefabs once per station; a station
    /// with an extension that stacks has no cap.
    /// </summary>
    [HarmonyPatch(typeof(Recipe), nameof(Recipe.GetRequiredStationLevel))]
    public static class UpgradeStation
    {
        private static readonly Dictionary<string, int> highest = new Dictionary<string, int>();

        [HarmonyPostfix]
        public static void Postfix(Recipe __instance, int quality, ref int __result)
        {
            if (quality <= 1 || __instance.m_craftingStation == null || __instance.m_item == null
                || BackpackCatalog.Of(__instance.m_item.m_itemData) == null)
                return;
            __result = Mathf.Min(__result, Mathf.Max(Highest(__instance.m_craftingStation.m_name), __instance.m_minStationLevel));
        }

        private static int Highest(string station)
        {
            if (highest.TryGetValue(station, out int level))
                return level;
            if (ZNetScene.instance == null)
                return int.MaxValue;
            level = Count(station);
            highest[station] = level;
            return level;
        }

        private static int Count(string station)
        {
            HashSet<string> kinds = new HashSet<string>();
            foreach (GameObject prefab in ZNetScene.instance.m_prefabs)
            {
                StationExtension extension = prefab != null ? prefab.GetComponent<StationExtension>() : null;
                if (extension == null || extension.m_craftingStation == null || extension.m_craftingStation.m_name != station)
                    continue;
                if (extension.m_stack)
                    return int.MaxValue;
                kinds.Add(prefab.name);
            }
            return 1 + kinds.Count;
        }
    }
}