// Solstice - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: clones of the game's own environments, named for the seasonal rules, with a night tint. No copied colour tables.
using System;
using System.Collections.Generic;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Solstice.Config;
using ExtraPotions.Solstice.Integration;
using ExtraPotions.Solstice.Weather;
using UnityEngine;

namespace ExtraPotions.Solstice.Runtime
{
    /// <summary>
    /// Creates the seasonal environment names the biome rules already refer to. Each clone keeps the source environment and
    /// tints night fog and sun. Disabling restores a source sky before removing owned clones.
    /// Tempest's own environments are never used as a source. Main thread only.
    /// </summary>
    public static class SeasonEnvironmentVariants
    {
        const int UpdateAttempts = 120;
        const float Tint = 0.35f;

        static MeadworksLog? log;
        static int attempts;
        static EnvMan? observed;
        static int environmentCount = -1;
        static readonly List<EnvSetup> Owned = new List<EnvSetup>();
        static readonly HashSet<string> Warned = new HashSet<string>(StringComparer.Ordinal);

        public static void Initialize(MeadworksLog logger) => log = logger;

        /// <param name="countUpdate">False from a weather query, which still creates a clone after the per-frame cap.</param>
        public static void Ensure(EnvMan? envMan, bool countUpdate)
        {
            if (envMan == null) return;
            if (!SolsticeModules.Active(SolsticeModule.Seasons) || Plugin.Settings == null || !Plugin.Settings.Get(SolsticeSettings.General.Enabled))
            {
                Restore(envMan);
                return;
            }
            int count = envMan.m_environments?.Count ?? 0;
            if (observed != envMan || environmentCount != count)
            {
                observed = envMan;
                environmentCount = count;
                attempts = 0;
            }
            if (countUpdate && attempts >= UpdateAttempts) return;
            bool pending = false;
            foreach (var variant in EnvironmentVariants.All)
            {
                if (variant.Name.StartsWith("Tempest", StringComparison.Ordinal)) continue;
                if (envMan.GetEnv(variant.Name) != null) continue;
                pending = true;
                var source = Find(envMan, variant.Source);
                if (source == null)
                {
                    if (Warned.Add(variant.Name))
                        log?.Verbose("Seasonal environment " + variant.Name + " has no source " + variant.Source + " yet.");
                    continue;
                }
                var clone = source.Clone();
                clone.m_name = variant.Name;
                if (variant.Source == "Snow" || variant.Source == "SnowStorm" || variant.Source == "Twilight_Snow" || variant.Source == "Twilight_SnowStorm")
                    clone.m_isWet = false;
                if (variant.Name == "Swamp Summer")
                {
                    clone.m_isCold = false;
                    clone.m_isColdAtNight = false;
                }
                var tint = new Color(variant.Red / 255f, variant.Green / 255f, variant.Blue / 255f);
                clone.m_fogColorNight = Color.Lerp(clone.m_fogColorNight, tint, Tint);
                clone.m_sunColorNight = Color.Lerp(clone.m_sunColorNight, tint, Tint);
                if (MeadworksHeadless.AllowsUi) DimLowlandClear(clone, variant.Name);
                envMan.AppendEnvironment(clone);
                Owned.Add(clone);
                SeasonFrame.Mark(SeasonFrameSystem.EnvironmentVariants);
            }
            environmentCount = envMan.m_environments?.Count ?? 0;
            if (countUpdate && pending) attempts++;
        }

        public static void Shutdown()
        {
            Restore(EnvMan.instance);
            attempts = 0;
            Warned.Clear();
            log = null;
        }

        public static void Restore(EnvMan? envMan)
        {
            bool changed = false;
            if (envMan != null)
            {
                var current = Source(envMan, envMan.m_currentEnv);
                var next = Source(envMan, envMan.m_nextEnv);
                var previous = Source(envMan, envMan.m_prevEnv);
                if (current != null || next != null || previous != null)
                {
                    // A transition can still contain a seasonal tint after its displayed name changes.
                    var source = next ?? current ?? Source(envMan, envMan.m_nextEnv ?? envMan.m_currentEnv, true) ?? previous;
                    if (source != null)
                    {
                        envMan.QueueEnvironment(source);
                        envMan.InterpolateEnvironment(envMan.m_transitionDuration);
                        changed = true;
                    }
                }
                if (envMan.m_environments != null)
                    foreach (var clone in Owned)
                        if (envMan.m_environments.Remove(clone)) changed = true;
            }
            if (changed) SeasonFrame.Mark(SeasonFrameSystem.EnvironmentVariants);
            Owned.Clear();
            attempts = 0;
            observed = null;
            environmentCount = -1;
        }

        static EnvSetup? Source(EnvMan envMan, EnvSetup? environment, bool includeVanilla = false)
        {
            if (environment == null) return null;
            foreach (var variant in EnvironmentVariants.All)
                if (string.Equals(variant.Name, environment.m_name, StringComparison.Ordinal)) return Find(envMan, variant.Source);
            return includeVanilla ? environment : null;
        }

        static void DimLowlandClear(EnvSetup clone, string name)
        {
            if (!SeasonSky.DimsLowlandClear(name)) return;
            clone.m_sunColorDay = ScaleColor(clone.m_sunColorDay, SeasonSky.ClearDaySun);
            clone.m_ambColorDay = ScaleColor(clone.m_ambColorDay, SeasonSky.ClearDayAmbient);
            clone.m_fogColorSunDay = ScaleColor(clone.m_fogColorSunDay, SeasonSky.ClearDayFogSun);
            clone.m_lightIntensityDay = SeasonSky.Scale(clone.m_lightIntensityDay, SeasonSky.ClearDayIntensity);
        }

        static Color ScaleColor(Color color, float factor) =>
            new Color(SeasonSky.Scale(color.r, factor), SeasonSky.Scale(color.g, factor), SeasonSky.Scale(color.b, factor), color.a);

        static EnvSetup? Find(EnvMan envMan, string name)
        {
            var direct = envMan.GetEnv(name);
            if (direct != null) return direct;
            var list = envMan.m_environments;
            if (list == null) return null;
            foreach (var env in list)
                if (env != null && string.Equals(env.m_name, name, StringComparison.OrdinalIgnoreCase)) return env;
            return null;
        }
    }
}
