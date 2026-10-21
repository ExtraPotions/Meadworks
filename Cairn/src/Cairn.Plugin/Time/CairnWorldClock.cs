// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using ExtraPotions.Cairn.Config;
using ExtraPotions.Cairn.WorldTime;
using HarmonyLib;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Cairn.Runtime
{
    [HarmonyPatch(typeof(ZNet), "UpdateNetTime")]
    internal static class CairnWorldClock
    {
        [HarmonyPrefix]
        static void Prefix(ZNet __instance, ref float dt)
        {
            var settings = Plugin.Settings;
            if (!Plugin.Ready || settings == null || !ReferenceEquals(__instance, ZNet.instance) || !__instance.IsServer()
                || !CruciblePlugin.Session.IsAuthority || __instance.GetNrOfPlayers() <= 0) return;
            // Ember's postfix receives this same delta, so its sleep boost composes with the shared clock rate.
            dt = WorldClockRate.ScaleDelta(dt, settings.Get(CairnSettings.WorldClock.Rate));
        }
    }
}
