// Burrow - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the synced Station Extension Range Multiplier (user decision 2026-10-12 23:31).
using BepInEx.Configuration;
using Burrow.Reach;
using ExtraPotions.Burrow;
using ExtraPotions.Burrow.Embedded.PatchGuard;
using ExtraPotions.Burrow.Embedded.SyncedConfig;
using HarmonyLib;

namespace Burrow.Stations
{
    /// <summary>
    /// Upgrade tools (StationExtension) connect to their crafting station within the vanilla distance of their prefab
    /// times the synced multiplier. The game reads the instance field StationExtension.m_maxStationDistance in every
    /// path (FindExtensions for the station level, FindClosestStationInRange for the connection line and the
    /// placement ghost's "too far" check), so each instance gets the value in Awake (ghosts included) and every live
    /// instance again when the value changes, the server's push included. Prefabs are never changed.
    /// </summary>
    public static class StationExtensionReach
    {
        public static ConfigEntry<float> Multiplier { get; private set; }
        private static readonly StationExtensionRange range = new StationExtensionRange();

        public static void Bind(SyncedConfiguration synced)
        {
            Multiplier = synced.Bind(ReachSettings.Section, "Station Extension Range Multiplier", StationExtensionRange.Default,
                "Upgrade tools (anvils, tanning rack, chopping block, adze and the other station extensions) connect to their crafting station this many times farther than in vanilla. 1 is vanilla.",
                acceptableValues: new AcceptableValueRange<float>(StationExtensionRange.Min, StationExtensionRange.Max));
            Multiplier.SettingChanged += Guard.Wrap("station extension range", (_, _) => ApplyAll(Multiplier.Value));
        }

        private static void Apply(StationExtension extension, float multiplier)
        {
            if (extension != null)
                extension.m_maxStationDistance = range.Effective(Utils.GetPrefabName(extension.gameObject), extension.m_maxStationDistance, multiplier);
        }

        private static void ApplyAll(float multiplier)
        {
            foreach (StationExtension extension in StationExtension.m_allExtensions)
                Apply(extension, multiplier);
        }

        /// <summary>Puts every live extension back to its vanilla distance.</summary>
        public static void Shutdown()
        {
            if (Multiplier != null)
                ApplyAll(StationExtensionRange.Min);
            Multiplier = null;
        }

        [HarmonyPatch(typeof(StationExtension), nameof(StationExtension.Awake))]
        private static class AwakePatch
        {
            [HarmonyPostfix]
            private static void Postfix(StationExtension __instance)
            {
                if (Multiplier != null)
                    Apply(__instance, Multiplier.Value);
            }
        }
    }
}
