// Cairn - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the visibility effects of design section 1.1 B as plain removal toggles (user decision 2026-10-08).
using System;
using System.Collections.Generic;

namespace ExtraPotions.Cairn.Visibility
{
    /// <summary>The fog, mist and smoke effects Cairn can remove, in setting order (design B1 to B16).</summary>
    public enum VisibilityEffect
    {
        DistanceFog, GroundMist, FogClouds, MistEmitters, OceanMist, MistlandsMist, ParticleMist, CaveMist,
        SnowstormFog, SnowGlint, AshlandsMist, AshlandsFader, DeepNorthMist, DeepNorthFader, AmbientOcclusion, Smoke,
    }

    /// <summary>One effect: its setting name and the phrase its setting descriptions use.</summary>
    public sealed class VisibilityEffectInfo
    {
        internal VisibilityEffectInfo(VisibilityEffect effect, string name, string description)
        {
            Effect = effect;
            Name = name;
            Description = description;
        }

        public VisibilityEffect Effect { get; }

        /// <summary>Setting name part, for example <c>Distance Fog</c> in <c>Remove Distance Fog</c>.</summary>
        public string Name { get; }

        public string Description { get; }

        public override string ToString() => Name;
    }

    public static class VisibilityEffects
    {
        private static VisibilityEffectInfo E(VisibilityEffect effect, string name, string description) => new VisibilityEffectInfo(effect, name, description);

        /// <summary>Every effect in enum order.</summary>
        public static IReadOnlyList<VisibilityEffectInfo> All { get; } = Array.AsReadOnly(new[]
        {
            E(VisibilityEffect.DistanceFog, "Distance Fog", "the distance fog in every biome and weather"),
            E(VisibilityEffect.GroundMist, "Ground Mist", "the ground mist near the player"),
            E(VisibilityEffect.FogClouds, "Fog Clouds", "the low fog clouds"),
            E(VisibilityEffect.MistEmitters, "Mist Emitters", "the weather mist emitters"),
            E(VisibilityEffect.OceanMist, "Ocean Mist", "the mist over the ocean"),
            E(VisibilityEffect.MistlandsMist, "Mistlands Mist", "the Mistlands mist"),
            E(VisibilityEffect.ParticleMist, "Particle Mist", "the volumetric particle mist in mist areas"),
            E(VisibilityEffect.CaveMist, "Cave Mist", "the mist inside troll caves and mountain caves"),
            E(VisibilityEffect.SnowstormFog, "Snowstorm Fog", "the snowstorm and blizzard fog"),
            E(VisibilityEffect.SnowGlint, "Snow Glint", "the blinding glint on snowfields"),
            E(VisibilityEffect.AshlandsMist, "Ashlands Mist", "the smoky Ashlands mist"),
            E(VisibilityEffect.AshlandsFader, "Ashlands Fader", "the Ashlands fader haze"),
            E(VisibilityEffect.DeepNorthMist, "Deep North Mist", "the Deep North mist"),
            E(VisibilityEffect.DeepNorthFader, "Deep North Fader", "the Deep North fader haze"),
            E(VisibilityEffect.AmbientOcclusion, "Ambient Occlusion", "the ambient occlusion haze"),
            E(VisibilityEffect.Smoke, "Smoke", "the smoke of fires"),
        });

        public static VisibilityEffectInfo Of(VisibilityEffect effect) => All[(int)effect];
    }
}
