// Tempest - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: which game environment a player gets in each phase (design 1.2, 4.2; WildWeather behaviour only).
using System;
using System.Collections.Generic;
using ExtraPotions.Tempest.Events;

namespace ExtraPotions.Tempest.Weather
{
    /// <summary>Blizzard Freezing (design 4.2, Q3): Freezing as in the Mountains, Cold only, or neither.</summary>
    public enum FreezeLevel { Freezing, Cold, None }

    /// <summary>
    /// The environment Tempest asks the game for (design 1.2), per phase, for the local player's biome. Warning shows the
    /// game's <c>Misty</c> haze, then light snow or light rain for the last <see cref="BuildUpSeconds"/>; Active uses
    /// Tempest's clones of the game's <c>SnowStorm</c> and <c>ThunderStorm</c> (so the freezing level and the weather filter
    /// touch only Tempest's events); Clearing returns through the light variant. A lightning storm (dry) uses a third clone of
    /// <c>ThunderStorm</c> without rain or wetness and builds up and clears through the haze instead of light rain. Dense Fog
    /// copies <c>Misty</c> with far denser fog and builds up and clears through the haze; an Ash Storm copies
    /// <c>Ashlands_storm</c> and builds up through the Ashlands mist and ash rain; a Meteor Shower copies the visuals of
    /// <c>Ashlands_meteorshower</c>, keeps the game's own weather through its warning (which can last until nightfall) and
    /// clears straight back to it. A blizzard in the Deep North is a whiteout: a copy of <c>Twilight_SnowStorm</c> with thicker
    /// fog and full wind, built up and cleared through <c>Twilight_Snow</c>. A forced
    /// environment (dungeon, raid, boss, another mod) always wins (W10).
    /// </summary>
    public static class EnvironmentPlan
    {
        public const string BlizzardEnvironment = "Tempest_Blizzard";
        public const string StormEnvironment = "Tempest_Storm";
        public const string DryStormEnvironment = "Tempest_LightningStorm";
        public const string FogEnvironment = "Tempest_Fog";
        public const string AshStormEnvironment = "Tempest_AshStorm";
        public const string MeteorShowerEnvironment = "Tempest_MeteorShower";
        public const string WhiteoutEnvironment = "Tempest_Whiteout";
        public const string Haze = "Misty";
        public const string LightSnow = "Snow";
        public const string LightRain = "LightRain";
        public const string AshHaze = "Ashlands_misty";
        public const string AshRain = "Ashlands_ashrain";
        public const string NorthSnow = "Twilight_Snow";
        public const int BuildUpSeconds = 60;

        /// <summary>
        /// Fog density of the Dense Fog clone, day and night (the game's environments range about 0.001 to 0.15): sight fades
        /// out at roughly 15 to 20 meters. Tuned in the native check; a source value above it is kept.
        /// </summary>
        public const float DenseFogDensity = 0.16f;

        /// <summary>
        /// Fog density floor of the Deep North whiteout, day and night: thicker than the game's twilight snowstorm but short
        /// of Dense Fog, so the way home stays readable at about 30 meters. Its wind is raised to at least
        /// <see cref="WhiteoutWind"/>. A denser or windier source value is kept.
        /// </summary>
        public const float WhiteoutFogDensity = 0.1f;

        public const float WhiteoutWind = 0.9f;

        public static IReadOnlyList<string> Names { get; } = Array.AsReadOnly(new[]
        {
            BlizzardEnvironment, StormEnvironment, DryStormEnvironment, FogEnvironment, AshStormEnvironment, MeteorShowerEnvironment,
            WhiteoutEnvironment, Haze, LightSnow, LightRain, AshHaze, AshRain, NorthSnow,
        });

        /// <summary>Tempest's own event environments (always allowed by the weather filter).</summary>
        public static IReadOnlyList<string> EventEnvironments { get; } = Array.AsReadOnly(new[]
            { BlizzardEnvironment, StormEnvironment, DryStormEnvironment, FogEnvironment, AshStormEnvironment, MeteorShowerEnvironment, WhiteoutEnvironment });

        /// <summary>The clones whose freezing and cold flags follow Blizzard Freezing.</summary>
        public static IReadOnlyList<string> FreezingEnvironments { get; } = Array.AsReadOnly(new[] { BlizzardEnvironment, WhiteoutEnvironment });

        /// <summary>A blizzard reaching a player in the Deep North (the whiteout and its twilight build-up).</summary>
        public static bool Whiteout(EventKind kind, WeatherBiome biome) => kind == EventKind.Blizzard && biome == WeatherBiome.DeepNorth;

        public static string EventEnvironment(EventKind kind)
        {
            switch (kind)
            {
                case EventKind.Blizzard: return BlizzardEnvironment;
                case EventKind.LightningStorm: return DryStormEnvironment;
                case EventKind.DenseFog: return FogEnvironment;
                case EventKind.AshStorm: return AshStormEnvironment;
                case EventKind.MeteorShower: return MeteorShowerEnvironment;
                default: return StormEnvironment;
            }
        }

        public static string EventEnvironment(TempestEvent e) => EventEnvironment(e.Kind);

        /// <summary>The Active environment for a player in <paramref name="biome"/>: the whiteout for a Deep North blizzard.</summary>
        public static string EventEnvironment(EventKind kind, WeatherBiome biome) => Whiteout(kind, biome) ? WhiteoutEnvironment : EventEnvironment(kind);

        /// <summary>How a clone changes its source.</summary>
        public enum CloneStyle { Copy, Dry, DenseFog, VisualOnly, Whiteout }

        /// <summary>Tempest's environment clones: name, the game environment copied, and whether rain and wetness are removed.</summary>
        public static IReadOnlyList<(string Name, string Source, bool Dry)> Clones { get; } = Array.AsReadOnly(new[]
        {
            (BlizzardEnvironment, "SnowStorm", false),
            (StormEnvironment, "ThunderStorm", false),
            (DryStormEnvironment, "ThunderStorm", true),
            (FogEnvironment, "Misty", false),
            (AshStormEnvironment, "Ashlands_storm", false),
            (MeteorShowerEnvironment, "Ashlands_meteorshower", false),
            (WhiteoutEnvironment, "Twilight_SnowStorm", false),
        });

        /// <summary>
        /// What the plugin does to each clone: Dry removes rain and wetness; DenseFog raises the fog density to
        /// <see cref="DenseFogDensity"/>; VisualOnly drops any effect that carries a network object or other gameplay part, so
        /// only the sky, light, particles and sound are copied (all the new clones are copied this way); Whiteout raises the
        /// fog to <see cref="WhiteoutFogDensity"/> and the wind to <see cref="WhiteoutWind"/>.
        /// </summary>
        public static IReadOnlyList<CloneStyle> Styles(string name)
        {
            switch (name)
            {
                case DryStormEnvironment: return new[] { CloneStyle.Dry };
                case FogEnvironment: return new[] { CloneStyle.DenseFog, CloneStyle.VisualOnly };
                case AshStormEnvironment:
                case MeteorShowerEnvironment: return new[] { CloneStyle.VisualOnly };
                case WhiteoutEnvironment: return new[] { CloneStyle.Whiteout, CloneStyle.VisualOnly };
                default: return new[] { CloneStyle.Copy };
            }
        }

        /// <summary>
        /// The game environments a clone may copy: the clone's <see cref="Clones"/> source, spelled as the game names it (the
        /// Ashlands names are lowercase after the underscore). The plugin falls back to ignoring case.
        /// </summary>
        public static IReadOnlyList<string> SourceNames(string clone)
        {
            foreach (var c in Clones) if (c.Name == clone) return new[] { c.Source };
            return Array.Empty<string>();
        }

        /// <summary>A Tempest event environment name (case-insensitive).</summary>
        public static bool IsEventEnvironment(string? name)
        {
            if (name == null) return false;
            foreach (var e in EventEnvironments) if (string.Equals(e, name, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        /// <summary>A rain particle system of the game's storm (removed from the dry clone).</summary>
        public static bool IsRainEffect(string? name) => name != null && name.IndexOf("rain", StringComparison.OrdinalIgnoreCase) >= 0;

        /// <summary>The game environment each clone copies at start.</summary>
        public static string Source(EventKind kind)
        {
            var name = EventEnvironment(kind);
            foreach (var c in Clones) if (c.Name == name) return c.Source;
            throw new ArgumentException("Unknown kind.", nameof(kind));
        }

        /// <summary>The build-up and clearing environment of a kind (null: the game's own weather).</summary>
        public static string? Light(EventKind kind)
        {
            switch (kind)
            {
                case EventKind.Blizzard: return LightSnow;
                case EventKind.Thunderstorm: return LightRain;
                case EventKind.AshStorm: return AshRain;
                case EventKind.MeteorShower: return null;
                default: return Haze;
            }
        }

        /// <summary>The build-up and clearing environment for a player in <paramref name="biome"/>: twilight snow before a whiteout.</summary>
        public static string? Light(EventKind kind, WeatherBiome biome) => Whiteout(kind, biome) ? NorthSnow : Light(kind);

        /// <summary>The early warning environment of a kind (null: the game's own weather).</summary>
        public static string? Warning(EventKind kind)
        {
            switch (kind)
            {
                case EventKind.AshStorm: return AshHaze;
                case EventKind.MeteorShower: return null;
                default: return Haze;
            }
        }

        /// <summary>The environment for a player in <paramref name="biome"/> at <paramref name="now"/>, or null for the game's own.</summary>
        public static string? For(TempestEvent? e, double now, WeatherBiome biome, bool gameForced)
        {
            if (e == null || gameForced || !e.Affects(biome)) return null;
            var state = Timeline.At(e, now);
            switch (state.Phase)
            {
                case EventPhase.Warning:
                    return state.SecondsLeft > BuildUpSeconds ? Warning(e.Kind) : Light(e.Kind, biome);
                case EventPhase.Active: return EventEnvironment(e.Kind, biome);
                case EventPhase.Clearing: return Light(e.Kind, biome);
                default: return null;
            }
        }

        /// <summary>The clone's freezing and cold flags (the game's <c>EnvSetup.m_isFreezing</c> and <c>m_isCold</c>).</summary>
        public static (bool Freezing, bool Cold) Flags(FreezeLevel level)
        {
            switch (level)
            {
                case FreezeLevel.Freezing: return (true, true);
                case FreezeLevel.Cold: return (false, true);
                default: return (false, false);
            }
        }

        /// <summary>A personal weather override (W9) is cleared once an event has started in the player's biome.</summary>
        public static bool ClearsPersonalOverride(TempestEvent? e, double now, WeatherBiome biome) =>
            e != null && e.Affects(biome) && Timeline.At(e, now).Phase != EventPhase.Idle;
    }
}
