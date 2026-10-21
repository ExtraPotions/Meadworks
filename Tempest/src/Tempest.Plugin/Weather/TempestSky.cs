// Tempest - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the event environments on the local client (design 1.2, 4.2, W1, W2, W4, W10) and the personal
// override (W9, Valheim_Admin_Tool's local weather override, behaviour only).
using System;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Tempest.Events;
using ExtraPotions.Tempest.Integration;
using ExtraPotions.Tempest.Localization;
using ExtraPotions.Tempest.Weather;
using HarmonyLib;

namespace ExtraPotions.Tempest.Runtime
{
    /// <summary>
    /// TP04b. At EnvMan start Tempest adds its environments (<see cref="EnvironmentPlan.Clones"/>): clones of the game's
    /// SnowStorm and ThunderStorm (<c>Tempest_Blizzard</c>, <c>Tempest_Storm</c>, and <c>Tempest_LightningStorm</c> without
    /// rain particles or wetness), <c>Tempest_Fog</c> (Misty with dense fog), <c>Tempest_AshStorm</c> (Ashlands_storm) and
    /// <c>Tempest_MeteorShower</c> (Ashlands_meteorshower), the last three visuals only, <c>Tempest_Whiteout</c> (the Deep
    /// North blizzard, Twilight_SnowStorm with thicker fog and full wind), and keeps both blizzard clones' freezing flags on
    /// the Blizzard Freezing setting. The game asks <c>GetEnvironmentOverride</c> each update; when nothing is forced (debug or personal
    /// environment, intro, raid, environment zone, forced environment) and the local player stands in a biome of the
    /// shared event, Tempest answers with the environment of the current phase. Main thread only.
    /// </summary>
    public static class TempestSky
    {
        static MeadworksLog? log;
        static readonly System.Collections.Generic.HashSet<string> Warned = new System.Collections.Generic.HashSet<string>();
        static float nextRetry;
        static string? lastWanted;
        static string? personal;
        static bool personalFrozen;
        static float personalUntil;

        /// <summary>The environment Tempest asked for in the last override call (the filter never removes it).</summary>
        public static string? Applying { get; private set; }

        public static void Initialize(MeadworksLog logger) => log = logger;

        /// <summary>Applies the Blizzard Freezing setting to the blizzard clones (after a settings change).</summary>
        public static void UpdateFlags()
        {
            var envMan = EnvMan.instance;
            var settings = Plugin.Settings;
            if (envMan == null || settings == null) return;
            var (freezing, cold) = EnvironmentPlan.Flags(settings.Get(Config.TempestSettings.Effects.BlizzardFreezing));
            foreach (var name in EnvironmentPlan.FreezingEnvironments)
            {
                var clone = envMan.GetEnv(name);
                if (clone == null) continue;
                clone.m_isFreezing = freezing;
                clone.m_isFreezingAtNight = freezing;
                clone.m_isCold = cold;
                clone.m_isColdAtNight = cold;
            }
        }

        /// <summary>Personal override (W9): the caller checked the cheat. False for an unknown environment.</summary>
        public static bool SetPersonal(string environment, bool freeze)
        {
            var envMan = EnvMan.instance;
            var env = envMan?.GetEnv(environment);
            if (envMan == null || env == null) return false;
            personal = env.m_name;
            personalFrozen = freeze;
            personalUntil = UnityEngine.Time.time + Math.Max(60, envMan.m_environmentDuration);
            envMan.m_debugEnv = personal;
            return true;
        }

        public static void ResetPersonal()
        {
            var envMan = EnvMan.instance;
            if (envMan != null && personal != null && envMan.m_debugEnv == personal) envMan.m_debugEnv = "";
            personal = null;
        }

        public static bool HasPersonal => personal != null;

        /// <summary>Called from Update: ends a personal override when an event reaches the player, when it expires, or when the cheat is gone.</summary>
        public static void Tick()
        {
            if (personal == null) return;
            var settings = Plugin.Settings;
            var envMan = EnvMan.instance;
            if (envMan == null || settings == null || envMan.m_debugEnv != personal)
            {
                personal = null;
                return;
            }
            if (!TempestModules.Active(TempestModule.Override) || !settings.Get(Config.TempestSettings.Cheats.PersonalOverride))
            {
                ResetPersonal();
                return;
            }
            if (TempestModules.Active(TempestModule.Events) && EnvironmentPlan.ClearsPersonalOverride(TempestWorld.Current, TempestWorld.Now, TempestWorld.LocalBiome))
            {
                ResetPersonal();
                if (log != null) TempestText.Centre(TempestText.Text(TempestTextKeys.OverrideCleared), log);
                return;
            }
            if (!personalFrozen && UnityEngine.Time.time >= personalUntil) ResetPersonal();
        }

        public static void Shutdown()
        {
            ResetPersonal();
            Applying = null;
            lastWanted = null;
            log = null;
        }

        /// <param name="warn">False at EnvMan.Awake: the Ashlands environments arrive later from ZoneSystem.SetupLocations
        /// (LocationList.m_environments through EnvMan.AppendEnvironment), so a miss there is expected.</param>
        static void CreateClones(EnvMan envMan, bool warn = true)
        {
            foreach (var (name, sourceName, _) in EnvironmentPlan.Clones)
            {
                if (envMan.GetEnv(name) != null) continue;
                EnvSetup? source = null;
                foreach (var candidate in EnvironmentPlan.SourceNames(name))
                    if ((source = Find(envMan, candidate)) != null) break;
                if (source == null)
                {
                    // Retried after ZoneSystem.SetupLocations and while an event needs it; warned once.
                    if (warn && Warned.Add(name))
                        log?.Warning("Game environment " + sourceName + " not found; " + name + " keeps the game's weather. Available environments: "
                            + AllNames(envMan) + ".");
                    continue;
                }
                var clone = source.Clone();
                clone.m_name = name;
                foreach (var style in EnvironmentPlan.Styles(name))
                {
                    if (style == EnvironmentPlan.CloneStyle.Dry) MakeDry(clone);
                    else if (style == EnvironmentPlan.CloneStyle.DenseFog) MakeDense(clone);
                    else if (style == EnvironmentPlan.CloneStyle.Whiteout) MakeWhiteout(clone);
                    else if (style == EnvironmentPlan.CloneStyle.VisualOnly) KeepVisuals(clone, name);
                }
                envMan.AppendEnvironment(clone);
            }
            UpdateFlags();
        }

        /// <summary>Every environment name EnvMan has, comma-joined and capped, for the not-found warning.</summary>
        static string AllNames(EnvMan envMan)
        {
            const int Cap = 4000;
            var names = new System.Collections.Generic.List<string>();
            foreach (var env in envMan.m_environments ?? new System.Collections.Generic.List<EnvSetup>())
                if (env?.m_name != null) names.Add(env.m_name);
            if (names.Count == 0) return "none";
            var text = names.Count + ": " + string.Join(", ", names);
            return text.Length <= Cap ? text : text.Substring(0, Cap) + " ...";
        }

        /// <summary>An environment by name, exact first, then ignoring case (the game's own names vary in case between lists).</summary>
        static EnvSetup? Find(EnvMan envMan, string? name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            var env = envMan.GetEnv(name);
            if (env != null || envMan.m_environments == null) return env;
            foreach (var candidate in envMan.m_environments)
                if (candidate != null && string.Equals(candidate.m_name, name, StringComparison.OrdinalIgnoreCase)) return candidate;
            return null;
        }

        /// <summary>Dense Fog: day and night fog raised to <see cref="EnvironmentPlan.DenseFogDensity"/> (a denser source value is kept).</summary>
        static void MakeDense(EnvSetup clone)
        {
            clone.m_fogDensityDay = Math.Max(clone.m_fogDensityDay, EnvironmentPlan.DenseFogDensity);
            clone.m_fogDensityNight = Math.Max(clone.m_fogDensityNight, EnvironmentPlan.DenseFogDensity);
        }

        /// <summary>Deep North whiteout: fog and wind raised to the whiteout floors (a denser or windier source value is kept).</summary>
        static void MakeWhiteout(EnvSetup clone)
        {
            clone.m_fogDensityDay = Math.Max(clone.m_fogDensityDay, EnvironmentPlan.WhiteoutFogDensity);
            clone.m_fogDensityNight = Math.Max(clone.m_fogDensityNight, EnvironmentPlan.WhiteoutFogDensity);
            clone.m_windMin = Math.Max(clone.m_windMin, EnvironmentPlan.WhiteoutWind);
            clone.m_windMax = Math.Max(clone.m_windMax, 1f);
        }

        /// <summary>
        /// Drops any effect object that carries a network object, a projectile or an area of effect, so the clone copies the
        /// source's look and sound but never spawns or damages anything by itself (Tempest's own strikes and burns do that).
        /// </summary>
        static void KeepVisuals(EnvSetup clone, string name)
        {
            var systems = clone.m_psystems;
            if (systems == null) return;
            var kept = Array.FindAll(systems, p => p == null
                || (p.GetComponentInChildren<ZNetView>(true) == null && p.GetComponentInChildren<Projectile>(true) == null && p.GetComponentInChildren<Aoe>(true) == null));
            if (kept.Length != systems.Length) log?.Info(name + ": " + (systems.Length - kept.Length) + " gameplay effect(s) of the source left out.");
            clone.m_psystems = kept;
        }

        /// <summary>No rain particles and not wet, so EnvMan.IsWet() is false; lightning, wind, light and sound stay the storm's.</summary>
        static void MakeDry(EnvSetup clone)
        {
            clone.m_isWet = false;
            var systems = clone.m_psystems;
            if (systems == null) return;
            var kept = Array.FindAll(systems, p => p == null || !EnvironmentPlan.IsRainEffect(p.name));
            if (kept.Length == systems.Length) log?.Warning("No rain effect found in ThunderStorm; the lightning storm may still show rain.");
            clone.m_psystems = kept;
        }

        [HarmonyPatch(typeof(EnvMan), nameof(EnvMan.Awake))]
        static class AwakePatch
        {
            static void Postfix(EnvMan __instance)
            {
                try
                {
                    CreateClones(__instance, warn: false);
                }
                catch (Exception error)
                {
                    log?.Warning("Tempest environments not created: " + error.GetType().Name);
                }
            }
        }

        /// <summary>ZoneSystem.Start/GenerateLocations append the location lists' environments (the Ashlands ones among them) after EnvMan.Awake.</summary>
        [HarmonyPatch(typeof(ZoneSystem), "SetupLocations")]
        static class LocationsPatch
        {
            static void Postfix()
            {
                try
                {
                    if (EnvMan.instance != null) CreateClones(EnvMan.instance);
                }
                catch (Exception error)
                {
                    log?.Warning("Tempest environments not created after the location lists: " + error.GetType().Name);
                }
            }
        }

        [HarmonyPatch(typeof(EnvMan), nameof(EnvMan.GetEnvironmentOverride))]
        static class OverridePatch
        {
            [HarmonyPriority(Priority.Low)]
            static void Postfix(EnvMan __instance, ref string __result)
            {
                __result = TempestFilter.FilterOverride(__instance, __result)!;
                Applying = null;
                if (!string.IsNullOrEmpty(__result) || !string.IsNullOrEmpty(__instance.m_forceEnv)) return;
                if (!TempestModules.Active(TempestModule.Events) || Player.m_localPlayer == null) return;
                var wanted = EnvironmentPlan.For(TempestWorld.Current, TempestWorld.Now, TempestWorld.LocalBiome, false);
                wanted = TempestSeasons.EventEnvironment(wanted, name => EnvMan.instance != null && Find(EnvMan.instance, name) != null);
                var env = Find(__instance, wanted);
                // A newly wanted environment (event start or phase change) is looked up at once, then every 10 s.
                if (env == null && EnvironmentPlan.IsEventEnvironment(wanted) && (wanted != lastWanted || UnityEngine.Time.unscaledTime >= nextRetry))
                {
                    lastWanted = wanted;
                    nextRetry = UnityEngine.Time.unscaledTime + 10f;
                    CreateClones(__instance);
                    env = Find(__instance, wanted);
                }
                if (env == null) return;
                Applying = env.m_name;
                __result = env.m_name;
            }
        }
    }
}
