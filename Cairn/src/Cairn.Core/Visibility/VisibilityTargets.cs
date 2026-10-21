// Cairn - ExtraPotions Meadworks visibility targets.
// Portions Copyright (c) 2023 Pete Navarra (aka Vapok), NoFogBruh 2.1.2 (NoFogBruh/Features/FogTargetManager.cs and
// NoFogBruh/Features/DisableFogComponent.cs at 6a1bfe69271a0db58e51459767f9cbce5b3ac580, https://github.com/Vapok/NoFogBruh), MIT.
// Portions Copyright (c) 2026 JevMods, BetterFog 1.0.2 (src/BetterFog/Patches/SmokePatches.cs at
// 208646c7edd89c57b93cbb1c934a12830910dcb2, https://github.com/JevMods/BetterFog), MIT.
// The MIT notices are retained in THIRD_PARTY_NOTICES/NoFogBruh-MIT.txt and THIRD_PARTY_NOTICES/BetterFog-MIT.txt.
// Changed by expDARE, 2026: the scene paths and switch hooks became a portable table keyed by Cairn's removal toggles;
// Fog Component and Distant Fog feed Distance Fog, Troll Cave and Mountain Cave mist feed Cave Mist (design B1, B8); the
// smoke hook removes smoke instead of scaling it (plain toggle, user decision 2026-10-08).
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks) for the changes. SPDX-License-Identifier: GPL-3.0-or-later AND MIT
using System;
using System.Collections.Generic;
using System.Linq;

namespace ExtraPotions.Cairn.Visibility
{
    /// <summary>How a target is removed.</summary>
    public enum TargetKind
    {
        /// <summary>A child of <c>_GameMain/_Environment</c> set inactive.</summary>
        EnvironmentChild,
        /// <summary>A child of the first loaded <c>LocationList</c> whose name starts with the prefix, set inactive.</summary>
        LocationChild,
        /// <summary><c>RenderSettings.fogDensity</c> set to 0 after <c>EnvMan.SetEnv</c>.</summary>
        FogDensity,
        /// <summary>The post-processing fog command buffer removed (<c>PostProcessingBehaviour.OnPreRender</c>).</summary>
        PostProcessingFog,
        /// <summary>The post-processing ambient occlusion command buffer removed.</summary>
        PostProcessingAmbientOcclusion,
        /// <summary><c>MistEmitter</c> components of the environment particle systems disabled (<c>EnvMan.SetParticleArrayEnabled</c>).</summary>
        MistEmitter,
        /// <summary><c>DistantFogEmitter</c> stops emitting (<c>SetEmit(false)</c>, <c>Update</c> skipped).</summary>
        DistantFogEmitter,
        /// <summary><c>ParticleMist.Update</c> skipped and its particles cleared.</summary>
        ParticleMist,
        /// <summary>Global shader float <c>_SnowGlintStrength</c> set to 0 after <c>EnvMan.Update</c>.</summary>
        SnowGlintShader,
        /// <summary><c>Smoke.GetAlpha</c> returns 0.</summary>
        SmokeAlpha,
    }

    public enum TargetSource { NoFogBruh, BetterFog }

    public sealed class VisibilityTarget
    {
        internal VisibilityTarget(VisibilityEffect effect, TargetKind kind, TargetSource source, string? path = null, string? locationListPrefix = null)
        {
            Effect = effect;
            Kind = kind;
            Source = source;
            Path = path;
            LocationListPrefix = locationListPrefix;
        }

        public VisibilityEffect Effect { get; }
        public TargetKind Kind { get; }
        public TargetSource Source { get; }

        /// <summary>Scene path relative to the environment root or the location list; null for component hooks.</summary>
        public string? Path { get; }

        public string? LocationListPrefix { get; }

        public override string ToString() => Effect + ": " + Kind + (Path == null ? "" : " " + (LocationListPrefix == null ? "" : LocationListPrefix + ":") + Path);
    }

    public static class VisibilityTargets
    {
        public const string EnvironmentRoot = "_GameMain/_Environment";
        public const string SnowGlintProperty = "_SnowGlintStrength";

        private static VisibilityTarget Env(VisibilityEffect e, string path) => new VisibilityTarget(e, TargetKind.EnvironmentChild, TargetSource.NoFogBruh, path);
        private static VisibilityTarget Loc(VisibilityEffect e, string prefix, string path) => new VisibilityTarget(e, TargetKind.LocationChild, TargetSource.NoFogBruh, path, prefix);
        private static VisibilityTarget Hook(VisibilityEffect e, TargetKind kind, TargetSource source = TargetSource.NoFogBruh) => new VisibilityTarget(e, kind, source);

        /// <summary>Every target, grouped by effect in enum order.</summary>
        public static IReadOnlyList<VisibilityTarget> All { get; } = Array.AsReadOnly(new[]
        {
            Hook(VisibilityEffect.DistanceFog, TargetKind.FogDensity),
            Env(VisibilityEffect.DistanceFog, "FollowPlayer/Mist"),
            Hook(VisibilityEffect.DistanceFog, TargetKind.PostProcessingFog),
            Env(VisibilityEffect.DistanceFog, "Distant_fog_planes"),
            Hook(VisibilityEffect.DistanceFog, TargetKind.DistantFogEmitter),
            Env(VisibilityEffect.GroundMist, "FollowPlayer/GroundMist"),
            Env(VisibilityEffect.FogClouds, "FollowPlayer/FogClouds"),
            Hook(VisibilityEffect.MistEmitters, TargetKind.MistEmitter),
            Env(VisibilityEffect.OceanMist, "OceanMist"),
            Loc(VisibilityEffect.MistlandsMist, "_LocationList_Mistlands", "environment_effects/FollowPlayer/Mistlands_Globalmist"),
            Hook(VisibilityEffect.ParticleMist, TargetKind.ParticleMist),
            Env(VisibilityEffect.CaveMist, "FollowPlayer/InteriorDust"),
            Loc(VisibilityEffect.CaveMist, "_LocationList_MountainCaves", "environment/followplayer/env_mountain_cave/mist"),
            Loc(VisibilityEffect.CaveMist, "_LocationList_MountainCaves", "environment/followplayer/env_mountain_cave_hildir/mist"),
            Env(VisibilityEffect.SnowstormFog, "FollowPlayer/SnowStorm"),
            Env(VisibilityEffect.SnowstormFog, "FollowPlayer/Blizzard"),
            Hook(VisibilityEffect.SnowGlint, TargetKind.SnowGlintShader),
            Loc(VisibilityEffect.AshlandsMist, "_LocationList_Ashlands", "environment_effects/FollowPlayer/Ashlands_Misty"),
            Loc(VisibilityEffect.AshlandsFader, "_LocationList_Ashlands", "environment_effects/FollowPlayer/Ashlands_FaderFX"),
            Loc(VisibilityEffect.DeepNorthMist, "_LocationList_DeepNorth", "environment_effects/FollowPlayer/DeepNorth_Misty"),
            Loc(VisibilityEffect.DeepNorthFader, "_LocationList_DeepNorth", "environment_effects/FollowPlayer/DeepNorth_FaderFX"),
            Hook(VisibilityEffect.AmbientOcclusion, TargetKind.PostProcessingAmbientOcclusion),
            Hook(VisibilityEffect.Smoke, TargetKind.SmokeAlpha, TargetSource.BetterFog),
        });

        private static readonly IReadOnlyList<VisibilityTarget>[] ByEffect = Enum.GetValues(typeof(VisibilityEffect)).Cast<VisibilityEffect>()
            .Select(e => (IReadOnlyList<VisibilityTarget>)All.Where(t => t.Effect == e).ToList().AsReadOnly()).ToArray();

        public static IReadOnlyList<VisibilityTarget> Of(VisibilityEffect effect) => ByEffect[(int)effect];
    }
}
