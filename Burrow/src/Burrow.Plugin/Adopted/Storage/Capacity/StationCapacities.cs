using System;
using System.Collections.Generic;
using UnityEngine;

namespace Burrow.Capacity
{
    /// <summary>
    /// Applies Burrow.Stations*.yml to the station prefabs (Smelter.m_maxOre and m_maxFuel, copied into every new
    /// instance) and to every loaded station of those prefabs, whose own fields the game reads when something is
    /// added and in the hover text. A cap the station does not use (0 in the game) is never set, because 0 is what
    /// tells the game the station runs without items or without fuel. A cap that leaves the file, and every cap
    /// when Capacity is switched off, goes back to the game's value. What a station holds is never touched: a
    /// lower cap only makes the game refuse more until the station is below it.
    /// </summary>
    public static class StationCapacities
    {
        // Prefab name to the caps this module currently sets on it; 0 = that cap is left alone.
        private static readonly Dictionary<string, StationCaps> managed = new Dictionary<string, StationCaps>(StringComparer.OrdinalIgnoreCase);

        public static void ApplyAll()
        {
            if (ZNetScene.instance == null)
                return;
            Dictionary<string, StationCaps> wanted = Wanted();
            Dictionary<string, StationCaps> changes = new Dictionary<string, StationCaps>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, Smelter> prefab in StationPrefabs.All())
            {
                StationCaps vanilla = VanillaCaps.Remember(prefab.Key, prefab.Value);
                wanted.TryGetValue(prefab.Key, out StationCaps asked);
                asked = AtLeastMinimum(asked, vanilla);
                StationCaps caps = Next(prefab.Key, Usable(prefab.Key, asked, vanilla), vanilla);
                if (caps.IsEmpty)
                    continue;
                Set(prefab.Value, caps);
                changes[prefab.Key] = caps;
            }
            foreach (Smelter station in UnityEngine.Object.FindObjectsByType<Smelter>(FindObjectsSortMode.None))
            {
                if (changes.TryGetValue(StationPrefabs.NameOf(station), out StationCaps caps))
                    Set(station, caps);
            }
        }

        /// <summary>Smelter.Awake: a station made from another copy of its prefab gets the caps as well.</summary>
        public static void ApplyTo(Smelter station)
        {
            if (station == null || managed.Count == 0 || !CapacitySettings.Enabled.Value)
                return;
            if (managed.TryGetValue(StationPrefabs.NameOf(station), out StationCaps caps))
                Set(station, caps);
        }

        /// <summary>The caps the file asks for, none when the module is off.</summary>
        private static Dictionary<string, StationCaps> Wanted()
        {
            StationsModel model = StationsFile.Set != null ? StationsFile.Set.Current as StationsModel : null;
            if (!CapacitySettings.Enabled.Value || model == null)
                return new Dictionary<string, StationCaps>(StringComparer.OrdinalIgnoreCase);
            return model.Caps;
        }

        /// <summary>
        /// Station Minimum Capacity for each cap the file leaves unset: raised to the minimum only where the game's
        /// value is lower and the station uses that cap (Burrow change).
        /// </summary>
        private static StationCaps AtLeastMinimum(StationCaps asked, StationCaps vanilla)
        {
            int minimum = CapacitySettings.Enabled.Value && CapacitySettings.StationMinimum != null ? CapacitySettings.StationMinimum.Value : 0;
            int items = asked.Items > 0 ? asked.Items : vanilla.Items > 0 && vanilla.Items < minimum ? minimum : 0;
            int fuel = asked.Fuel > 0 ? asked.Fuel : vanilla.Fuel > 0 && vanilla.Fuel < minimum ? minimum : 0;
            return new StationCaps(items, fuel);
        }

        /// <summary>The asked caps without those the station does not use; each one dropped is warned about.</summary>
        private static StationCaps Usable(string name, StationCaps asked, StationCaps vanilla)
        {
            int items = asked.Items;
            int fuel = asked.Fuel;
            if (items > 0 && vanilla.Items == 0)
            {
                Plugin.Log.LogWarning($"Burrow: {name} takes no items (the game's cap is 0); its 'items: {items}' in Burrow.Stations*.yml is ignored.");
                items = 0;
            }
            if (fuel > 0 && vanilla.Fuel == 0)
            {
                Plugin.Log.LogWarning($"Burrow: {name} takes no fuel (the game's cap is 0); its 'fuel: {fuel}' in Burrow.Stations*.yml is ignored.");
                fuel = 0;
            }
            return new StationCaps(items, fuel);
        }

        /// <summary>What to write now: the wanted caps, and the game's value for a cap that was set before and no longer is.</summary>
        private static StationCaps Next(string name, StationCaps target, StationCaps vanilla)
        {
            managed.TryGetValue(name, out StationCaps before);
            if (target.IsEmpty)
                managed.Remove(name);
            else
                managed[name] = target;
            int items = target.Items > 0 ? target.Items : before.Items > 0 ? vanilla.Items : 0;
            int fuel = target.Fuel > 0 ? target.Fuel : before.Fuel > 0 ? vanilla.Fuel : 0;
            return new StationCaps(items, fuel);
        }

        private static void Set(Smelter station, StationCaps caps)
        {
            if (caps.Items > 0)
                station.m_maxOre = caps.Items;
            if (caps.Fuel > 0)
                station.m_maxFuel = caps.Fuel;
        }
    }
}
