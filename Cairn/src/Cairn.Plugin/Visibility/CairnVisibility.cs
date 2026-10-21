// Cairn - ExtraPotions Meadworks fog, mist and smoke removal (runtime).
// Portions Copyright (c) 2023 Pete Navarra (aka Vapok), NoFogBruh 2.1.2 (NoFogBruh/Features/FogTargetManager.cs and
// NoFogBruh/Features/DisableFogComponent.cs at 6a1bfe69271a0db58e51459767f9cbce5b3ac580, https://github.com/Vapok/NoFogBruh), MIT.
// Portions Copyright (c) 2026 JevMods, BetterFog 1.0.2 (src/BetterFog/Patches/SmokePatches.cs, Patches/FogPatches.cs,
// Features/DistanceFogFeature.cs, Features/MistFeature.cs and Infrastructure/ParticleFade.cs at
// 208646c7edd89c57b93cbb1c934a12830910dcb2, https://github.com/JevMods/BetterFog), MIT.
// The MIT notices are retained in THIRD_PARTY_NOTICES/NoFogBruh-MIT.txt and THIRD_PARTY_NOTICES/BetterFog-MIT.txt.
// Changed by expDARE, 2026: one removal decision per effect from Cairn's toggles, the server's allowance and the Q1 hold;
// scene objects are deactivated while removed and only Cairn's own deactivations are restored; disabled mist emitters
// are re-enabled when their removal ends; the game's fog density is kept for the radius before it is zeroed; smoke is
// removed by its toggle and otherwise scaled by Smoke Strength; distance fog density and mist particle opacity are graded
// live (the game's colors are kept so a changed or restored strength reapplies); post-processing uses the same decisions via CairnPostProcessing (CA05b);
// Jotunn headless checks replaced by Cairn's install-time UI guard.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks) for the changes. SPDX-License-Identifier: GPL-3.0-or-later AND MIT
using System;
using System.Collections.Generic;
using ExtraPotions.Cairn.Config;
using ExtraPotions.Cairn.Visibility;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Cairn.Runtime
{
    /// <summary>Applies the removal toggles (design B, plain toggles). Installed only on a graphical client. Main thread only.</summary>
    public static class CairnVisibility
    {
        private const float ResolveSeconds = 3f;
        private const float ApplySeconds = 1f;

        private sealed class SceneTarget
        {
            public VisibilityTarget Target = null!;
            public GameObject? Object;
            public bool Owned;
        }

        private static readonly List<SceneTarget> Scene = new List<SceneTarget>();
        private static readonly HashSet<MistEmitter> DisabledEmitters = new HashSet<MistEmitter>();
        private static readonly bool[] Removing = new bool[VisibilityEffects.All.Count];

        /// <summary>A mist or fog particle system Cairn grades: its own material copy and the game's colors.</summary>
        private sealed class FadedSystem
        {
            public Material? Material;
            public Color MaterialColor;
            public ParticleSystem.MinMaxGradient StartColor;
            public float Applied = 1f;
        }

        private const string ColorProperty = "_Color";
        private static readonly Dictionary<ParticleSystem, FadedSystem> Faded = new Dictionary<ParticleSystem, FadedSystem>();
        private static GameObject[]? weatherSystems;
        private static float fogStrength = 1f, mistStrength = 1f, smokeStrength = 1f;
        private static readonly int SnowGlintId = Shader.PropertyToID(VisibilityTargets.SnowGlintProperty);
        private static Transform? environmentRoot;
        private static float nextResolve;
        private static float nextApply;
        private static bool installed;

        public static void Install(Harmony harmony)
        {
            Scene.Clear();
            foreach (var target in VisibilityTargets.All)
                if (target.Kind == TargetKind.EnvironmentChild || target.Kind == TargetKind.LocationChild)
                    Scene.Add(new SceneTarget { Target = target });
            harmony.PatchAll(typeof(Patches));
            CairnPostProcessing.Install(harmony);
            installed = true;
        }

        /// <summary>Restores every scene object and emitter Cairn turned off.</summary>
        public static void Uninstall()
        {
            if (!installed) return;
            installed = false;
            foreach (var s in Scene)
                if (s.Owned && s.Object != null) s.Object.SetActive(true);
            foreach (var emitter in DisabledEmitters)
                if (emitter != null) emitter.enabled = true;
            DisabledEmitters.Clear();
            emittersRemoved = false;
            Scene.Clear();
            environmentRoot = null;
            Array.Clear(Removing, 0, Removing.Length);
            fogStrength = mistStrength = smokeStrength = 1f;
            ApplyMist();
            Faded.Clear();
            weatherSystems = null;
        }

        public static bool Removes(VisibilityEffect effect) => installed && Removing[(int)effect];

        /// <summary>Re-decides every effect once a second and applies scene objects; resolves missing objects every 3 s (NoFogBruh's cadence).</summary>
        public static void Tick(float now)
        {
            if (!installed || now < nextApply) return;
            nextApply = now + ApplySeconds;
            var settings = Plugin.Settings;
            var held = GatePolicy.Holds(CairnModules.Facts());
            foreach (var info in VisibilityEffects.All)
                Removing[(int)info.Effect] = settings != null && Plugin.Ready && settings.Removes(info.Effect, held);
            var ready = settings != null && Plugin.Ready;
            fogStrength = ready ? settings!.Strength(CairnSettings.VisibilityStrength.DistanceFog, held, VisibilityEffect.DistanceFog) : 1f;
            mistStrength = ready ? settings!.Strength(CairnSettings.VisibilityStrength.Mist, held, VisibilityEffect.MistlandsMist, VisibilityEffect.ParticleMist) : 1f;
            smokeStrength = ready ? settings!.Strength(CairnSettings.VisibilityStrength.Smoke, held, VisibilityEffect.Smoke) : 1f;
            if (now >= nextResolve)
            {
                nextResolve = now + ResolveSeconds;
                Resolve();
            }
            ApplyScene();
            ApplyEmitters();
            ApplyMist();
        }

        /// <summary>Graded factors in force (tests and fixtures): distance fog density, mist opacity, smoke opacity.</summary>
        public static float FogStrength => installed ? fogStrength : 1f;
        public static float MistStrength => installed ? mistStrength : 1f;
        public static float SmokeStrength => installed ? smokeStrength : 1f;
        public static int GradedSystems => Faded.Count;

        /// <summary>Starts grading a mist particle system (BetterFog MistFeature); the strength in force applies at once.</summary>
        private static void Track(ParticleSystem? system)
        {
            if (system == null || Faded.ContainsKey(system)) return;
            var faded = new FadedSystem { StartColor = system.main.startColor };
            var renderer = system.GetComponent<ParticleSystemRenderer>();
            if (renderer != null && renderer.sharedMaterial != null && renderer.sharedMaterial.HasProperty(ColorProperty))
            {
                faded.Material = renderer.material;
                faded.MaterialColor = faded.Material.GetColor(ColorProperty);
            }
            Faded.Add(system, faded);
            if (mistStrength != 1f) Apply(system, faded, mistStrength);
        }

        private static void ApplyMist()
        {
            List<ParticleSystem>? gone = null;
            foreach (var pair in Faded)
            {
                if (pair.Key == null) { (gone ??= new List<ParticleSystem>()).Add(pair.Key!); continue; }
                if (pair.Value.Applied != mistStrength) Apply(pair.Key, pair.Value, mistStrength);
            }
            if (gone != null) foreach (var key in gone) Faded.Remove(key);
        }

        /// <summary>BetterFog ParticleFade: scale the material color's alpha, or else the start color's, from the game's value.</summary>
        private static void Apply(ParticleSystem system, FadedSystem faded, float factor)
        {
            faded.Applied = factor;
            if (faded.Material != null)
            {
                faded.Material.SetColor(ColorProperty, Scaled(faded.MaterialColor, factor));
                return;
            }
            var start = faded.StartColor;
            switch (start.mode)
            {
                case ParticleSystemGradientMode.Color:
                    start.color = Scaled(start.color, factor);
                    break;
                case ParticleSystemGradientMode.TwoColors:
                    start.colorMin = Scaled(start.colorMin, factor);
                    start.colorMax = Scaled(start.colorMax, factor);
                    break;
                default:
                    return;
            }
            var main = system.main;
            main.startColor = start;
        }

        private static Color Scaled(Color color, float factor)
        {
            color.a *= factor;
            return color;
        }

        private static bool IsFogLike(string name) =>
            name.IndexOf("fog", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("mist", StringComparison.OrdinalIgnoreCase) >= 0;

        /// <summary>The weather's fog-like particle systems (BetterFog MistFeature), tracked once per weather change.</summary>
        private static void TrackWeather()
        {
            var current = EnvMan.instance != null ? EnvMan.instance.m_currentPSystems : null;
            if (current == null || current == weatherSystems) return;
            weatherSystems = current;
            foreach (var weather in current)
            {
                if (weather == null) continue;
                foreach (var system in weather.GetComponentsInChildren<ParticleSystem>(true))
                    if (IsFogLike(system.name)) Track(system);
            }
        }

        private static void Resolve()
        {
            if (environmentRoot == null)
            {
                var root = GameObject.Find(VisibilityTargets.EnvironmentRoot);
                if (root != null) environmentRoot = root.transform;
            }
            List<LocationList>? lists = null;
            foreach (var s in Scene)
            {
                if (s.Object != null) continue;
                if (s.Target.LocationListPrefix == null)
                {
                    var found = environmentRoot != null ? environmentRoot.Find(s.Target.Path) : null;
                    if (found != null) s.Object = found.gameObject;
                    continue;
                }
                lists ??= LocationList.GetAllLocationLists();
                if (lists == null) continue;
                foreach (var list in lists)
                {
                    if (list == null || list.gameObject == null || !list.gameObject.scene.IsValid()) continue;
                    if (!list.gameObject.name.StartsWith(s.Target.LocationListPrefix, StringComparison.Ordinal)) continue;
                    var found = list.transform.Find(s.Target.Path);
                    if (found != null && found.gameObject.scene.IsValid())
                    {
                        s.Object = found.gameObject;
                        break;
                    }
                }
            }
        }

        private static void ApplyScene()
        {
            foreach (var s in Scene)
            {
                if (s.Object == null || !s.Object.scene.IsValid())
                {
                    s.Object = null;
                    s.Owned = false;
                    continue;
                }
                switch (VisibilityPolicy.SceneAction(Removing[(int)s.Target.Effect], s.Object.activeSelf, s.Owned))
                {
                    case TargetAction.Deactivate:
                        s.Object.SetActive(false);
                        s.Owned = true;
                        break;
                    case TargetAction.Restore:
                        s.Object.SetActive(true);
                        s.Owned = false;
                        break;
                }
            }
        }

        private static bool emittersRemoved;

        private static void ApplyEmitters()
        {
            bool removing = Removing[(int)VisibilityEffect.MistEmitters];
            // Turned on: switch off the emitters already running instead of waiting for the next environment change.
            if (removing && !emittersRemoved && EnvMan.instance != null)
                foreach (var emitter in EnvMan.instance.GetComponentsInChildren<MistEmitter>())
                    if (emitter.enabled) { emitter.enabled = false; DisabledEmitters.Add(emitter); }
            emittersRemoved = removing;
            if (removing || DisabledEmitters.Count == 0) return;
            foreach (var emitter in DisabledEmitters)
                if (emitter != null) emitter.enabled = true;
            DisabledEmitters.Clear();
        }

        [HarmonyPatch]
        internal static class Patches
        {
            /// <summary>Keeps the game's fog density for the radius, then removes distance fog (NoFogBruh) and re-applies scene objects on an environment change.</summary>
            [HarmonyPatch(typeof(EnvMan), nameof(EnvMan.SetEnv)), HarmonyPostfix]
            private static void SetEnv()
            {
                CairnRadius.GameFogDensity = RenderSettings.fogDensity;
                if (Removes(VisibilityEffect.DistanceFog)) RenderSettings.fogDensity = 0f;
                else if (installed && fogStrength != 1f) RenderSettings.fogDensity *= fogStrength;
                if (installed) TrackWeather();
            }

            [HarmonyPatch(typeof(EnvMan), nameof(EnvMan.Update)), HarmonyPostfix]
            private static void EnvUpdate()
            {
                if (Removes(VisibilityEffect.SnowGlint)) Shader.SetGlobalFloat(SnowGlintId, 0f);
            }

            [HarmonyPatch(typeof(EnvMan), nameof(EnvMan.SetParticleArrayEnabled)), HarmonyPostfix]
            private static void ParticleArray(GameObject[] psystems)
            {
                if (psystems == null) return;
                var emitters = Removes(VisibilityEffect.MistEmitters);
                var distant = Removes(VisibilityEffect.DistanceFog);
                if (!emitters && !distant) return;
                foreach (var system in psystems)
                {
                    if (system == null) continue;
                    if (emitters)
                    {
                        var emitter = system.GetComponentInChildren<MistEmitter>();
                        if (emitter != null && emitter.enabled)
                        {
                            emitter.enabled = false;
                            DisabledEmitters.Add(emitter);
                        }
                    }
                    if (distant)
                    {
                        var fog = system.GetComponentInChildren<DistantFogEmitter>();
                        if (fog != null) fog.SetEmit(false);
                    }
                }
            }

            [HarmonyPatch(typeof(ParticleMist), nameof(ParticleMist.Update)), HarmonyPrefix]
            private static bool ParticleMistUpdate(ParticleMist __instance)
            {
                if (!Removes(VisibilityEffect.ParticleMist))
                {
                    if (installed) Track(__instance.m_ps);
                    return true;
                }
                if (__instance.m_ps != null && __instance.m_ps.particleCount > 0) __instance.m_ps.Clear();
                return false;
            }

            [HarmonyPatch(typeof(DistantFogEmitter), nameof(DistantFogEmitter.Update)), HarmonyPrefix]
            private static bool DistantFogUpdate(DistantFogEmitter __instance)
            {
                if (!Removes(VisibilityEffect.DistanceFog))
                {
                    if (installed && __instance.m_psystems != null)
                        foreach (var system in __instance.m_psystems) Track(system);
                    return true;
                }
                __instance.SetEmit(false);
                return false;
            }

            [HarmonyPatch(typeof(Smoke), nameof(Smoke.GetAlpha)), HarmonyPostfix]
            private static void SmokeAlpha(ref float __result) =>
                __result = VisibilityPolicy.SmokeAlpha(__result, Removes(VisibilityEffect.Smoke), installed ? smokeStrength : 1f);
        }
    }
}
