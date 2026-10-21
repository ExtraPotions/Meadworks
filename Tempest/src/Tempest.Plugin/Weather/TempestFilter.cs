// Tempest - ExtraPotions Meadworks weather filter hooks.
// Portions Copyright (c) 2023 Pete Navarra (aka Vapok), NoFogBruh 2.1.2 (NoFogBruh/Features/WeatherEventsComponent.cs at
// 6a1bfe69271a0db58e51459767f9cbce5b3ac580, https://github.com/Vapok/NoFogBruh), MIT.
// The MIT notice is retained in THIRD_PARTY_NOTICES/NoFogBruh-MIT.txt.
// Changed by expDARE, 2026: the EnvMan hooks (GetAvailableEnvironments, GetEnvironmentOverride, both QueueEnvironment
// overloads, SetForceEnvironment, SetEnv) and the settings-changed reapply read Tempest's C01 weather filter through the
// portable WeatherFilter; the available list is replaced by a filtered copy instead of being edited in place; reflection on
// EnvMan fields became direct access through the publicized game assembly; dynamic switch registration (Awake,
// AppendEnvironment) is replaced by the Disabled Environments list; the hooks act only while Tempest's Filter module is
// active (not while NoFogBruh is installed) and never remove the environment Tempest itself is applying.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). This file as a whole: SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Tempest.Integration;
using ExtraPotions.Tempest.Weather;
using HarmonyLib;

namespace ExtraPotions.Tempest.Runtime
{
    /// <summary>TP11b: the server weather filter on the game's environment manager. Main thread only.</summary>
    public static class TempestFilter
    {
        static WeatherFilter? cached;

        /// <summary>The filter now, or null while the Filter module is off (NoFogBruh installed, product off).</summary>
        public static WeatherFilter? Current
        {
            get
            {
                var settings = Plugin.Settings;
                if (settings == null || !TempestModules.Active(TempestModule.Filter)) return null;
                return cached ??= settings.Filter();
            }
        }

        /// <summary>Called when any setting changed: the next read rebuilds the filter.</summary>
        public static void Invalidate() => cached = null;

        public static void Shutdown() => cached = null;

        public static bool Allows(string? environment)
        {
            var filter = Current;
            if (filter == null || string.IsNullOrEmpty(environment)) return true;
            if (environment == TempestSky.Applying) return true;
            return filter.Allows(environment);
        }

        /// <summary>NoFogBruh's GetEnvironmentOverride rule: a switched-off override is dropped (and a switched-off debug environment cleared).</summary>
        public static string? FilterOverride(EnvMan envMan, string? result)
        {
            if (string.IsNullOrEmpty(result) || Allows(result)) return result;
            if (!string.IsNullOrEmpty(envMan.m_debugEnv) && !Allows(envMan.m_debugEnv)) envMan.m_debugEnv = "";
            return null;
        }

        /// <summary>NoFogBruh's fallback: the biome's first clear environment, else Clear, else the game's default.</summary>
        public static EnvSetup? Fallback(EnvMan envMan, BiomeSector? biome)
        {
            if (envMan == null) return null;
            IEnumerable<string>? names = null;
            if (biome != null)
            {
                var setup = envMan.GetBiomeEnvSetup(biome.Biome);
                names = setup?.m_environments?.Where(e => e != null).Select(e => e.m_environment);
            }
            var name = WeatherFilter.Fallback(names, n => envMan.GetEnv(n) != null, envMan.GetDefaultEnv()?.m_name);
            return name == null ? null : envMan.GetEnv(name);
        }

        /// <summary>
        /// NoFogBruh's OnWeatherSettingChanged: clears a switched-off debug or forced environment, restarts the weather
        /// period, and swaps a switched-off next or current environment for the fallback at once.
        /// </summary>
        public static void Reapply()
        {
            var envMan = EnvMan.instance;
            if (envMan == null || Current == null) return;
            if (!string.IsNullOrEmpty(envMan.m_debugEnv) && !Allows(envMan.m_debugEnv)) envMan.m_debugEnv = "";
            if (!string.IsNullOrEmpty(envMan.m_forceEnv) && !Allows(envMan.m_forceEnv)) envMan.m_forceEnv = "";
            envMan.m_environmentPeriod = -1L;
            var fallback = Fallback(envMan, null);
            if (fallback == null) return;
            if (envMan.m_nextEnv != null && !Allows(envMan.m_nextEnv.m_name)) envMan.m_nextEnv = fallback;
            var current = envMan.GetCurrentEnvironment();
            if (current != null && !Allows(current.m_name))
            {
                envMan.m_forceInstantEnvSwitchTimer = 2f;
                envMan.m_currentEnv = fallback;
                envMan.QueueEnvironment(fallback);
            }
        }

        [HarmonyPatch(typeof(EnvMan), nameof(EnvMan.GetAvailableEnvironments))]
        static class GetAvailableEnvironmentsPatch
        {
            static void Postfix(EnvMan __instance, BiomeSector biome, ref List<EnvEntry> __result)
            {
                if (__result == null || __result.Count == 0 || Current == null) return;
                if (__result.All(e => e == null || Allows(e.m_environment))) return;
                var kept = __result.Where(e => e != null && Allows(e.m_environment)).ToList();
                if (kept.Count == 0)
                {
                    var fallback = Fallback(__instance, biome);
                    if (fallback == null) return;
                    kept.Add(new EnvEntry { m_environment = fallback.m_name, m_weight = 1f, m_env = fallback });
                }
                __result = kept;
            }
        }

        [HarmonyPatch(typeof(EnvMan), nameof(EnvMan.QueueEnvironment), new[] { typeof(string) })]
        static class QueueEnvironmentNamePatch
        {
            static void Prefix(EnvMan __instance, ref string name)
            {
                if (string.IsNullOrEmpty(name) || Allows(name)) return;
                var fallback = Fallback(__instance, null);
                if (fallback != null) name = fallback.m_name;
            }
        }

        [HarmonyPatch(typeof(EnvMan), nameof(EnvMan.QueueEnvironment), new[] { typeof(EnvSetup) })]
        static class QueueEnvironmentSetupPatch
        {
            static void Prefix(EnvMan __instance, ref EnvSetup env)
            {
                if (env == null || Allows(env.m_name)) return;
                var fallback = Fallback(__instance, null);
                if (fallback != null) env = fallback;
            }
        }

        [HarmonyPatch(typeof(EnvMan), nameof(EnvMan.SetForceEnvironment))]
        static class SetForceEnvironmentPatch
        {
            static void Prefix(EnvMan __instance, ref string env)
            {
                if (string.IsNullOrEmpty(env) || Allows(env)) return;
                var fallback = Fallback(__instance, null);
                if (fallback != null) env = fallback.m_name;
            }
        }

        [HarmonyPatch(typeof(EnvMan), nameof(EnvMan.SetEnv), new[] { typeof(EnvSetup), typeof(float), typeof(float), typeof(float), typeof(float), typeof(float) })]
        static class SetEnvPatch
        {
            static void Prefix(EnvMan __instance, ref EnvSetup env)
            {
                if (env == null || Allows(env.m_name)) return;
                var fallback = Fallback(__instance, null);
                if (fallback != null) env = fallback;
            }
        }
    }
}
