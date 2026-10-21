// Cairn - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original, clean room: reads the live environment list and rolls the next period with the game's random state,
// restored afterward. Tempest is optional: Allows and Fallback are called by reflection when that plugin is loaded.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using ExtraPotions.Cairn.Hud;
using ExtraPotions.Cairn.Localization;
using UnityEngine;

namespace ExtraPotions.Cairn.Runtime
{
    /// <summary>The next weather for the clock line. A client uses the shared world clock, so the period matches the host.</summary>
    public static class CairnForecast
    {
        public static ForecastLine Current { get; private set; } = ForecastLine.None;

        static ForecastLine cached = ForecastLine.None;
        static long cachedPeriod = long.MinValue;
        static int cachedX, cachedZ;
        static bool cachedAsh, cachedDeep, cacheValid, warned;
        static float nextRoll;

        public static ForecastLine Read(EnvMan? env, bool enabled, bool scheduleFree)
        {
            if (!enabled || !scheduleFree || env == null)
            {
                cacheValid = false;
                Current = ForecastLine.None;
                return Current;
            }
            try { Current = Predict(env); }
            catch (Exception error)
            {
                cacheValid = false;
                Current = ForecastLine.None;
                if (!warned)
                {
                    warned = true;
                    Debug.LogWarning("Cairn forecast unavailable: " + error.GetType().Name + " " + error.Message);
                }
            }
            return Current;
        }

        public static string Text(ForecastLine line)
        {
            if (!line.Visible) return "";
            var name = CairnText.Text(line.NameKey);
            var minutes = line.Minutes.ToString(CultureInfo.InvariantCulture);
            if (line.WindPoint.Length == 0) return CairnText.Format(CairnTextKeys.HudForecast, name, minutes);
            var point = CairnText.Text(CairnTextKeys.CompassPoint(line.WindPoint));
            var wind = point + " " + line.WindPercent.ToString(CultureInfo.InvariantCulture) + "%";
            return CairnText.Format(CairnTextKeys.HudForecastWind, name, minutes, wind);
        }

        static ForecastLine Predict(EnvMan env)
        {
            var znet = ZNet.instance;
            var camera = Utils.GetMainCamera();
            if (znet == null || camera == null) return ForecastLine.None;
            // A raid, debug environment, intro, altar zone or forced environment is what will happen. Tempest's own event
            // is already on its row. Calling GetEnvironmentOverride would run Tempest's postfix, which clears its apply flag.
            if (Held(env, Player.m_localPlayer)) return ForecastLine.None;
            var position = camera.transform.position;
            // The game's Deep North check passes the camera's height as the second argument.
            var ashlands = WorldGenerator.IsAshlands(position.x, position.z);
            var deepnorth = WorldGenerator.IsDeepnorth(position.x, position.y);
            var seconds = (long)znet.GetTimeSeconds();
            var duration = env.m_environmentDuration;
            var period = WeatherForecast.Period(seconds, duration);
            var qx = (int)(position.x / 64f);
            var qz = (int)(position.z / 64f);
            var minutes = WeatherForecast.MinutesUntil(WeatherForecast.SecondsUntilChange(seconds, duration));
            if (cacheValid && cachedPeriod == period && cachedX == qx && cachedZ == qz && cachedAsh == ashlands && cachedDeep == deepnorth
                && Time.unscaledTime < nextRoll)
                return cached.Visible ? new ForecastLine(true, cached.NameKey, minutes, cached.WindPoint, cached.WindPercent, cached.Category) : ForecastLine.None;

            nextRoll = Time.unscaledTime + 1f;
            cachedPeriod = period;
            cachedX = qx;
            cachedZ = qz;
            cachedAsh = ashlands;
            cachedDeep = deepnorth;
            cacheValid = true;
            var biome = env.GetBiome();
            // The live list is this season's. A period that starts after the season changes is still rolled from it.
            var available = env.GetAvailableEnvironments(biome);
            var entries = new List<ForecastEntry>();
            if (available != null)
            {
                foreach (var entry in available)
                {
                    if (entry == null || string.IsNullOrEmpty(entry.m_environment)) continue;
                    entries.Add(new ForecastEntry(entry.m_environment, entry.m_weight, entry.m_ashlandsOverride, entry.m_deepnorthOverride));
                }
            }
            var allows = TempestGate.Allows();
            var fallback = allows == null ? null : TempestGate.FallbackName(env, biome);
            var filtered = WeatherForecast.Filtered(entries, allows, fallback);
            var total = WeatherForecast.WeightSum(filtered);
            var changes = WeatherForecast.Upcoming(seconds, duration, 1, entries, ashlands, deepnorth, next => Roll(next, total), allows, fallback);
            if (changes.Count == 0)
            {
                cached = ForecastLine.None;
                return cached;
            }
            var change = changes[0];
            var setup = Find(env, change.Name);
            var category = WeatherCategories.Of(change.Name, setup != null ? setup.m_fogDensityDay : 0f, setup != null && setup.m_isWet, false);
            var token = WeatherCategories.EventToken(change.Name);
            var nameKey = token != null ? CairnTextKeys.TempestToken(token) : CairnTextKeys.Weather(category);
            var windPoint = "";
            var windPercent = 0;
            if (setup != null)
            {
                var at = seconds < 0 ? change.SecondsUntil : seconds + change.SecondsUntil;
                var wind = WeatherForecast.Wind(setup.m_windMin, setup.m_windMax, SampleWind(at, env.m_windPeriodDuration));
                windPoint = WeatherForecast.WindPoint(wind.Angle);
                windPercent = WeatherForecast.WindPercent(wind.Intensity);
            }
            cached = new ForecastLine(true, nameKey, minutes, windPoint, windPercent, category);
            return cached;
        }

        static bool Held(EnvMan env, Player? player)
        {
            var current = env.GetCurrentEnvironment();
            if (current != null && WeatherCategories.EventToken(current.m_name) != null) return true;
            if (Allowed(env.m_debugEnv) || Allowed(env.m_forceEnv)) return true;
            if (player != null && player.InIntro() && Allowed(env.m_introEnvironment)) return true;
            var events = RandEventSystem.instance;
            if (events != null && Allowed(events.GetEnvOverride())) return true;
            if (player != null && WorldGenerator.instance != null)
            {
                var alts = WorldGenerator.instance.GetBiomeSector(player.transform.position).AltBiomes;
                if (alts != null)
                    foreach (var alt in alts)
                        if (alt != null && Allowed(alt.m_forceEnvironment)) return true;
            }
            var persistent = PersistentEventSystem.instance;
            if (persistent != null && Allowed(persistent.GetEnvironmentOverride())) return true;
            return Allowed(EnvZone.GetEnvironment());
        }

        static bool Allowed(string? name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            var allows = TempestGate.Allows();
            return allows == null || allows(name);
        }

        static EnvSetup? Find(EnvMan env, string name)
        {
            var list = env.m_environments;
            if (list == null) return null;
            for (var i = 0; i < list.Count; i++)
            {
                var setup = list[i];
                if (setup != null && string.Equals(setup.m_name, name, StringComparison.Ordinal)) return setup;
            }
            return null;
        }

        static float Roll(long period, float total)
        {
            var state = UnityEngine.Random.state;
            try
            {
                UnityEngine.Random.InitState(unchecked((int)period));
                return total > 0f ? UnityEngine.Random.Range(0f, total) : 0f;
            }
            finally { UnityEngine.Random.state = state; }
        }

        static float[] SampleWind(long time, long windDuration)
        {
            var values = new float[8];
            var state = UnityEngine.Random.state;
            try
            {
                var octaves = new[] { 1, 2, 4, 8 };
                for (var i = 0; i < octaves.Length; i++)
                {
                    UnityEngine.Random.InitState(WeatherForecast.WindSeed(time, windDuration, octaves[i]));
                    values[i * 2] = UnityEngine.Random.value;
                    values[i * 2 + 1] = UnityEngine.Random.value;
                }
            }
            finally { UnityEngine.Random.state = state; }
            return values;
        }

        /// <summary>Optional Tempest filter. Missing while Tempest is not loaded, so the native list is used.</summary>
        static class TempestGate
        {
            static Type? type;
            static bool found;
            static MethodInfo? allows;
            static MethodInfo? fallback;
            static Func<string, bool>? allowsCall;

            static void Look()
            {
                if (found) return;
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (!string.Equals(assembly.GetName().Name, "Tempest", StringComparison.Ordinal)) continue;
                    type = assembly.GetType("ExtraPotions.Tempest.Runtime.TempestFilter");
                    break;
                }
                // A miss stays cached. Tempest is loaded before gameplay, so later HUD frames must not scan again.
                found = AssemblyScan.CacheMiss();
                if (type == null) return;
                allows = type.GetMethod("Allows", BindingFlags.Public | BindingFlags.Static);
                fallback = type.GetMethod("Fallback", BindingFlags.Public | BindingFlags.Static);
            }

            public static Func<string, bool>? Allows()
            {
                Look();
                if (allows == null) return null;
                return allowsCall ??= name =>
                {
                    try { return (bool)allows.Invoke(null, new object?[] { name })!; }
                    catch (Exception) { return true; }
                };
            }

            public static string? FallbackName(EnvMan env, BiomeSector biome)
            {
                Look();
                if (fallback == null) return null;
                try
                {
                    var setup = fallback.Invoke(null, new object[] { env, biome });
                    var field = setup?.GetType().GetField("m_name");
                    return field?.GetValue(setup) as string;
                }
                catch (Exception) { return null; }
            }
        }
    }
}
