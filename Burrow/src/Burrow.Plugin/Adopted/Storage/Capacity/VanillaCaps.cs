using System;
using System.Collections.Generic;
using UnityEngine;

namespace Burrow.Capacity
{
    /// <summary>The caps every station prefab had before Burrow touched it, remembered the first time it is seen.</summary>
    public static class VanillaCaps
    {
        private static readonly Dictionary<string, StationCaps> byPrefab = new Dictionary<string, StationCaps>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Remembers the prefab component's current caps as vanilla when the prefab is new, and returns the vanilla caps.</summary>
        public static StationCaps Remember(string prefabName, Smelter prefab)
        {
            if (!byPrefab.TryGetValue(prefabName, out StationCaps caps))
            {
                caps = new StationCaps(Mathf.Max(0, prefab.m_maxOre), Mathf.Max(0, prefab.m_maxFuel));
                byPrefab[prefabName] = caps;
            }
            return caps;
        }
    }
}
