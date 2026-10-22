// Solstice - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Adapted from Seasons 1.10.6 by shudnal (https://github.com/shudnal/Seasons), commit 4054af6e8823d787921d673607e4942554872b66,
// Unlicense (public domain), SeasonState/SeasonState.cs (ApplySeasonBiomeEnvironmentRules, UpdateCurrentEnvironment). See THIRD_PARTY_NOTICES/Seasons-Unlicense.txt.
// Changed by expDARE, 2026: the seasonal add, replace and remove rules are applied to the list the game asks for
// (EnvMan.GetAvailableEnvironments) instead of rebuilding EnvMan.m_biomes, so the game's own data is never modified and Harmony unpatching
// restores everything; seasonal names the game does not ship are cloned and tinted before the rules run; Tempest's environments are untouched; the weather reset at a season start yields to Tempest and to forced environments.
using System;
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Meadworks.Contracts;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Solstice.Calendar;
using ExtraPotions.Solstice.Integration;
using ExtraPotions.Solstice.Weather;
using HarmonyLib;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;
using WeatherProvider = System.Func<System.Tuple<int, int, float, int, bool, int>>;

namespace ExtraPotions.Solstice.Runtime
{
    /// <summary>
    /// Seasonal weather. Solstice changes only the biome weather lists, never the running weather, so it cannot fight a Tempest event (whose
    /// environment the game asks for ahead of any list) or a forced environment. The one direct touch, the reset that makes the game choose
    /// weather again when a season starts, is deferred (defensively: the game already prefers an override on that path) while a Tempest event reaches the local player or the game has a forced or debug
    /// environment, for at most an hour (<see cref="WeatherYield.MaxDeferSeconds"/>). Tempest is found through the Crucible capability
    /// <c>meadworks.weather.events</c>; without Tempest the reset runs at once. Main thread only.
    /// </summary>
    public static class SolsticeWeather
    {
        /// <summary>Seconds between looks for a provider while none is registered.</summary>
        const float LookInterval = 5f;

        static MeadworksLog? log;
        static readonly EnvironmentRefresh Refresh = new EnvironmentRefresh();
        static WeatherProvider? provider;
        static float nextLook;
        static SeasonEnvironmentRules? rules;
        static Season? rulesSeason;
        static readonly HashSet<string> Reported = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>What the last <see cref="Tick"/> decided (for the status command and diagnostics).</summary>
        public static WeatherYieldDecision LastDecision { get; private set; }

        public static bool RefreshPending => Refresh.Pending;

        public static void Initialize(MeadworksLog logger) => log = logger;

        public static void RequestRefresh() => Refresh.Request(UnityEngine.Time.unscaledTimeAsDouble);

        public static void Tick()
        {
            if (!Refresh.Pending) return;
            var envMan = EnvMan.instance;
            if (envMan == null || SolsticeSeasons.Effects == null)
            {
                Refresh.Cancel();
                return;
            }
            var forced = !string.IsNullOrEmpty(envMan.m_forceEnv) || !string.IsNullOrEmpty(envMan.m_debugEnv);
            bool running = false, local = false;
            if (WeatherEventsContract.TryRead(Provider(), out var state))
            {
                running = state.Running;
                local = state.LocalAffected;
            }
            LastDecision = WeatherYield.Decide(running, local, forced);
            if (Refresh.Tick(UnityEngine.Time.unscaledTimeAsDouble, LastDecision))
            {
                envMan.m_environmentPeriod = -1L;
                log?.Verbose("Weather reset for the new season.");
            }
        }

        static WeatherProvider? Provider()
        {
            if (provider != null || UnityEngine.Time.unscaledTime < nextLook) return provider;
            nextLook = UnityEngine.Time.unscaledTime + LookInterval;
            try { CruciblePlugin.Services.Capabilities.TryResolve(WeatherEventsContract.Id, WeatherEventsContract.ContractVersion, out provider); }
            catch (Exception) { provider = null; }
            return provider;
        }

        /// <summary>The seasonal rules for the current season (cached per season).</summary>
        static SeasonEnvironmentRules? Rules()
        {
            var season = SolsticeSeasons.Current;
            if (season == null || SolsticeSeasons.Effects == null) return null;
            if (rules == null || rulesSeason != season)
            {
                rules = SeasonBiomeEnvironments.For(season.Value);
                rulesSeason = season;
            }
            return rules;
        }

        public static void Shutdown()
        {
            Refresh.Cancel();
            provider = null;
            nextLook = 0f;
            rules = null;
            rulesSeason = null;
            Reported.Clear();
            log = null;
        }

        [HarmonyPatch(typeof(EnvMan), nameof(EnvMan.GetAvailableEnvironments))]
        static class AvailableEnvironmentsPatch
        {
            /// <summary>Runs ahead of Tempest's weather filter, which then sees the seasonal list.</summary>
            [HarmonyPriority(Priority.High)]
            static void Postfix(EnvMan __instance, BiomeSector biome, ref List<EnvEntry> __result)
            {
                try
                {
                    var seasonRules = Rules();
                    SeasonEnvironmentVariants.Ensure(__instance, false);
                    if (seasonRules == null || seasonRules.Count == 0 || __result == null || biome == null) return;
                    long started = SeasonFrame.Stamp();
                    var setup = __instance.GetBiomeEnvSetup(biome.Biome);
                    var baseline = __result.Where(e => e != null).Select(e => new EnvironmentWeight(e.m_environment, e.m_weight, e));
                    var applied = EnvironmentRules.Apply(seasonRules, setup?.m_name ?? "", biome.Biome.ToString(), baseline,
                        name => __instance.GetEnv(name) != null, name => ReportMissing(name));
                    var list = new List<EnvEntry>(applied.Count);
                    foreach (var weight in applied)
                    {
                        if (weight.Source is EnvEntry kept) list.Add(kept);
                        else list.Add(new EnvEntry { m_environment = weight.Name, m_weight = weight.Weight, m_env = __instance.GetEnv(weight.Name) });
                    }
                    __result = list;
                    SeasonFrame.Add(SeasonFrameSystem.WeatherListSwap, started);
                }
                catch (Exception error)
                {
                    log?.Warning("Seasonal weather not applied: " + error.GetType().Name + ": " + error.Message);
                }
            }

            static void ReportMissing(string name)
            {
                if (Reported.Add(name)) log?.Verbose("Seasonal environment " + name + " does not exist in this game; the vanilla weather stays.");
            }
        }
    }
}
