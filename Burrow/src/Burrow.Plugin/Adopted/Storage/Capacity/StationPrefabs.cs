using System;
using System.Collections.Generic;
using UnityEngine;

namespace Burrow.Capacity
{
    /// <summary>
    /// The station prefabs of the scene: every ZNetScene prefab with the game's Smelter component on it or on a
    /// child (smelters, kilns, blast furnaces, refineries, spinning wheels, windmills, hot tubs, the battering ram
    /// and any other mod's station built the same way), by prefab name.
    /// </summary>
    public static class StationPrefabs
    {
        public static Dictionary<string, Smelter> All()
        {
            Dictionary<string, Smelter> prefabs = new Dictionary<string, Smelter>(StringComparer.OrdinalIgnoreCase);
            ZNetScene scene = ZNetScene.instance;
            if (scene == null || scene.m_prefabs == null)
                return prefabs;
            foreach (GameObject prefab in scene.m_prefabs)
            {
                Smelter station = prefab != null ? prefab.GetComponentInChildren<Smelter>(true) : null;
                if (station != null && !prefabs.ContainsKey(prefab.name))
                    prefabs[prefab.name] = station;
            }
            return prefabs;
        }

        /// <summary>The prefab name of a station's net object (the game's own m_nview, which may sit on a parent), without "(Clone)".</summary>
        public static string NameOf(Smelter station)
        {
            if (station == null)
                return "";
            ZNetView view = station.m_nview != null ? station.m_nview : station.GetComponentInParent<ZNetView>();
            return Utils.GetPrefabName(view != null ? view.gameObject : station.gameObject);
        }
    }
}
