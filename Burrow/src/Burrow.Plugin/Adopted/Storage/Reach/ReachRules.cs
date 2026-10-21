using System.Collections.Generic;
using Burrow.Core;
using UnityEngine;
using ExtraPotions.Burrow.Embedded.YamlConfig;

namespace Burrow.Reach
{
    /// <summary>
    /// The current reach rules: the applied YAML model, the per player toggle and the gate every patch asks before
    /// it counts or pays. Applying a model also fills <see cref="ContainerRules"/> for the other modules.
    /// </summary>
    public static class ReachRules
    {
        private const string ToggleFlag = "reachOff";

        public static ReachModel Current { get; private set; }

        /// <summary>The YAML apply callback: replaces the model and the shared enabled table.</summary>
        public static void Apply(YamlModel model)
        {
            Current = model as ReachModel;
            ContainerRules.SetEnabled(EnabledTable());
            int listed = Current != null ? Current.Containers.Count : 0;
            int stations = Current != null ? Current.Stations.Count : 0;
            Plugin.Log.LogInfo($"Burrow: reach rules applied, {listed} container and {stations} station prefabs listed.");
        }

        private static Dictionary<string, bool> EnabledTable()
        {
            Dictionary<string, bool> table = new Dictionary<string, bool>();
            if (Current == null)
                return table;
            foreach (KeyValuePair<string, ContainerRule> entry in Current.Containers)
                table[entry.Key] = entry.Value.Enabled;
            return table;
        }

        /// <summary>The module is on, the local player exists and has not toggled it off, and the mode's switch is on.</summary>
        public static bool Active(ReachMode mode)
        {
            if (!ReachSettings.Enabled.Value || Player.m_localPlayer == null || PlayerOff)
                return false;
            switch (mode)
            {
                case ReachMode.Crafting: return ReachSettings.Crafting.Value;
                case ReachMode.Building: return ReachSettings.Building.Value;
                case ReachMode.Upgrading: return ReachSettings.Upgrading.Value;
                case ReachMode.Stations: return ReachSettings.FeedStations.Value;
                default: return false;
            }
        }

        /// <summary>The per character toggle (Burrow.reachOff in the player's custom data).</summary>
        public static bool PlayerOff => CharacterData.GetFlag(ToggleFlag);

        public static void SetPlayerOff(bool off) => CharacterData.SetFlag(ToggleFlag, off);

        /// <summary>The quality level the game passes: 0 for pieces, 1 for a new craft, more for an upgrade.</summary>
        public static ReachMode FromQuality(int qualityLevel)
        {
            if (qualityLevel <= 0)
                return ReachMode.Building;
            return qualityLevel == 1 ? ReachMode.Crafting : ReachMode.Upgrading;
        }

        /// <summary>The file's range when set, else the cfg Range.</summary>
        public static float Range => Current != null && Current.Range.HasValue ? Current.Range.Value : ReachSettings.Range.Value;

        public static ContainerRule RuleFor(string prefabName)
        {
            if (Current != null && prefabName != null && Current.Containers.TryGetValue(prefabName, out ContainerRule rule))
                return rule;
            return ContainerRule.Default;
        }

        public static ContainerRule RuleFor(Container container) => RuleFor(ContainerScan.PrefabName(container));

        public static StationRule StationRuleFor(string prefabName)
        {
            if (Current != null && prefabName != null && Current.Stations.TryGetValue(prefabName, out StationRule rule))
                return rule;
            return StationRule.Default;
        }

        /// <summary>The rule of the station component's net object prefab (the net view's object, as for containers).</summary>
        public static StationRule StationRuleFor(Component station)
        {
            if (station == null)
                return StationRule.Default;
            ZNetView view = station.GetComponentInParent<ZNetView>();
            GameObject root = view != null ? view.gameObject : station.gameObject;
            return StationRuleFor(Utils.GetPrefabName(root));
        }

        /// <summary>The prefab's own range when set, else <see cref="Range"/>.</summary>
        public static float RangeFor(Container container)
        {
            ContainerRule rule = RuleFor(container);
            return rule.Range ?? Range;
        }

        /// <summary>The widest range any rule uses, the radius to scan before the per prefab filter.</summary>
        public static float MaxRange()
        {
            float max = Range;
            if (Current == null)
                return max;
            foreach (ContainerRule rule in Current.Containers.Values)
            {
                if (rule.Range.HasValue && rule.Range.Value > max)
                    max = rule.Range.Value;
            }
            return max;
        }
    }
}
