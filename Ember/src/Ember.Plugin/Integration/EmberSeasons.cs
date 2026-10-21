// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Globalization;
using System.Linq;
using System.Text;
using BepInEx.Configuration;
using ExtraPotions.Ember.Config;
using ExtraPotions.Ember.Integration;
using ExtraPotions.Meadworks.Crucible;
using HarmonyLib;
using Jotunn.Managers;
using UnityEngine;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Ember.Runtime
{
    internal static class EmberSeasons
    {
        static ConfigEntry<string>? facts;
        static EmberSyncReceipt receipt = new EmberSyncReceipt();
        static SeasonReading reading;
        static float nextRead;
        static long generation = -1;

        internal static void Initialize(ConfigFile file)
        {
            facts = MeadworksConfig.BindHidden(file, "Seasons", "Effective Facts", "",
                "Managed by Ember: optional season facts from the host. Do not edit.", ConfigScope.ServerAuthoritative);
            SynchronizationManager.OnConfigurationSynchronized += Synchronized;
        }

        static void Synchronized(object sender, Jotunn.Utils.ConfigurationSynchronizationEventArgs args)
        {
            if (args.UpdatedPluginGUIDs != null && args.UpdatedPluginGUIDs.Contains(Plugin.Guid)) receipt.Observe();
            nextRead = 0;
        }

        internal static SeasonReading Current => reading;
        internal static bool NightActive => reading.Active && Plugin.Settings != null &&
            EmberModules.Active(EmberModule.Sleep) && Plugin.Settings.Get(EmberSettings.Seasons.NightLength);
        internal static float Dawn => NightActive ? reading.Dawn : 0.15f;
        internal static float Drain(float current, bool fuelFree) => SeasonRules.Drain(current, reading,
            Plugin.Settings != null && Plugin.Settings.Get(EmberSettings.Seasons.FuelBurn), fuelFree);
        internal static bool Heat(bool current) => SeasonRules.Heat(current, reading,
            Plugin.Settings != null && Plugin.Settings.Get(EmberSettings.Seasons.TorchHeat));

        static string Scope() => "1|" + CruciblePlugin.Session.WorldId.ToString(CultureInfo.InvariantCulture) + "|"
            + Convert.ToBase64String(Encoding.UTF8.GetBytes(CruciblePlugin.Session.SessionId)) + "|";

        internal static void Tick()
        {
            if (!CruciblePlugin.IsReady || !EmberModules.ProductActive(out _) || ZNet.instance == null ||
                !CruciblePlugin.Session.AuthorityReady || CruciblePlugin.Session.WorldId == 0 ||
                string.IsNullOrEmpty(CruciblePlugin.Session.SessionId))
            {
                reading = default;
                generation = -1;
                nextRead = 0;
                return;
            }
            var session = CruciblePlugin.Session;
            if (generation != session.Generation)
            {
                reading = default;
                generation = session.Generation;
                nextRead = 0;
            }
            if (Time.unscaledTime < nextRead) return;
            nextRead = Time.unscaledTime + 0.25f;
            if (session.IsAuthority && ZNet.instance.IsServer())
            {
                var text = "";
                try
                {
                    if (CruciblePlugin.Services.Capabilities.TryResolve<Func<string>>(SeasonRules.Id, SeasonRules.Version, out var provider))
                        text = provider() ?? "";
                }
                catch (Exception) { text = ""; }
                if (!SeasonRules.TryParse(text, out reading)) text = "";
                var carried = text.Length == 0 ? "" : Scope() + text;
                if (facts != null && facts.Value != carried)
                    MeadworksConfig.Amend(facts, Plugin.Guid, ConfigScope.ServerAuthoritative, carried);
            }
            else
            {
                // Never trust the joining client's own Solstice or a previous connection's saved carrier.
                var text = receipt.CurrentGeneration == session.Generation && facts != null
                    ? MeadworksConfig.Effective(facts, Plugin.Guid, ConfigScope.ServerAuthoritative, "") : "";
                var scope = Scope();
                SeasonRules.TryParse(text.StartsWith(scope, StringComparison.Ordinal) ? text.Substring(scope.Length) : "", out reading);
            }
        }

        internal static double NextMorning(EnvMan env) => SeasonRules.NextMorning(
            ZNet.instance.GetTimeSeconds(), env.m_dayLengthSec, Dawn);

        internal static float SleepFraction(EnvMan env)
        {
            if (!NightActive || env.m_debugTimeOfDay || ZNet.instance == null || env.m_dayLengthSec <= 0)
                return env.GetDayFraction();
            return SeasonRules.Rescale((float)(ZNet.instance.GetTimeSeconds() % env.m_dayLengthSec / env.m_dayLengthSec), Dawn);
        }

        internal static void Shutdown()
        {
            SynchronizationManager.OnConfigurationSynchronized -= Synchronized;
            facts = null;
            receipt = new EmberSyncReceipt();
            reading = default;
            generation = -1;
            nextRead = 0;
        }
    }

    [HarmonyPatch]
    internal static class SeasonalNightPatches
    {
        [HarmonyPatch(typeof(EnvMan), nameof(EnvMan.RescaleDayFraction)), HarmonyPrefix]
        static bool Rescale(float fraction, ref float __result)
        {
            if (!EmberSeasons.NightActive) return true;
            __result = SeasonRules.Rescale(fraction, EmberSeasons.Dawn);
            return false;
        }

        [HarmonyPatch(typeof(EnvMan), nameof(EnvMan.GetMorningStartSec)), HarmonyPostfix]
        static void Morning(EnvMan __instance, int day, ref double __result)
        {
            if (EmberSeasons.NightActive)
                __result = ((double)day + EmberSeasons.Dawn) * __instance.m_dayLengthSec;
        }

        [HarmonyPatch(typeof(EnvMan), nameof(EnvMan.SkipToMorning)), HarmonyPrefix]
        static bool Skip(EnvMan __instance)
        {
            if (!EmberSeasons.NightActive || ZNet.instance == null || !ZNet.instance.IsServer()) return true;
            var now = ZNet.instance.GetTimeSeconds();
            __instance.m_skipToTime = EmberSeasons.NextMorning(__instance);
            __instance.m_timeSkipSpeed = (__instance.m_skipToTime - now) / 12.0;
            __instance.m_skipTime = true;
            return false;
        }
    }
}
