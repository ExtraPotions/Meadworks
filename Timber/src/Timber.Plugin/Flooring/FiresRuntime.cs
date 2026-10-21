// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Original C01/C03/C04/C09/headless lifecycle for the adopted OpenKeep fire placement mechanism.
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using ExtraPotions.Meadworks.Crucible;
using HarmonyLib;
using Timber.Camera;
using UnityEngine;
using Plugin = ExtraPotions.Timber.Runtime.Plugin;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace Timber.Flooring
{
    public static class FiresRuntime
    {
        static CameraBindings? bindings;
        static CameraValue<bool>? enabled;
        static CameraValue<bool>? roofs;
        static CameraValue<bool>? smelters;
        static CameraValue<bool>? rainProtection;
        static bool appliedRoofs;
        static bool appliedSmelters;
        static bool resolved, flooringPresent, applied;
        static bool openkeepPresent;
        static ConfigEntry<string>? openkeepWood;
        static int sceneId, prefabCount = -1;
        static float next;
        static long noticed = -1;
        static SourceMod? source;
        public static bool Active => resolved && Plugin.Active && !flooringPresent &&
            !(openkeepPresent && (openkeepWood == null || !string.IsNullOrWhiteSpace(openkeepWood.Value))) && enabled != null && enabled.Value;
        public static bool RoofsActive => resolved && Plugin.Active && !flooringPresent && roofs != null && roofs.Value;
        public static bool SmeltersActive => resolved && Plugin.Active && !flooringPresent && smelters != null && smelters.Value;
        public static bool RainProtectionActive => resolved && Plugin.Active && !flooringPresent && rainProtection != null && rainProtection.Value;

        public static void Initialize(ConfigFile config)
        {
            bindings = new CameraBindings(config);
            enabled = bindings.Bind("Flooring", "Fires On Floors", true,
                "Allow Fireplace and Fire comfort-group pieces on floors. Other placement, support, smoke and fire-spread rules remain native.");
            roofs = bindings.Bind("Flooring", "Floors Are Roofs", true,
                "Floors count as a roof for the space below them, sheltering it from rain. Off: floors work as in the game.");
            smelters = bindings.Bind("Flooring", "Smelters On Floors", true,
                "Smelters can be placed on floors, and a smelter on a floor does not break from lack of support. Other damage and material costs are unchanged.");
            rainProtection = bindings.Bind("Flooring", "Floors Dont Decay", true,
                "Floors do not take rain damage. They still look wet, need support, and can burn or be damaged by fire, ash and attacks.");
        }
        public static void ResolveSources()
        {
            flooringPresent = Chainloader.PluginInfos.ContainsKey("com.jekkle.valheim.carturflooring");
            openkeepPresent = Chainloader.PluginInfos.TryGetValue("milkyteam.openkeep", out var info);
            var plugin = openkeepPresent ? info.Instance as BepInEx.BaseUnityPlugin : null;
            if (plugin != null) plugin.Config.TryGetEntry<string>(new ConfigDefinition("8. Homestead", "Build On Wood"), out openkeepWood);
            resolved = true;
        }
        public static void Tick()
        {
            if (Time.unscaledTime < next) return;
            next = Time.unscaledTime + .5f;
            var blocked = flooringPresent ? new SourceMod("com.jekkle.valheim.carturflooring", "Cartur's Flooring") :
                openkeepPresent && (openkeepWood == null || !string.IsNullOrWhiteSpace(openkeepWood.Value)) ?
                    new SourceMod("milkyteam.openkeep", "OpenKeep Build On Wood") : null;
            if (source?.Guid != blocked?.Guid)
            {
                source = blocked; noticed = -1;
                if (source == null) MeadworksStepAside.WithdrawModule(Plugin.Guid, "fires_on_floors");
            }
            if (source != null && (Player.m_localPlayer != null || !Plugin.AllowsUi) && noticed != CruciblePlugin.Session.Generation)
            {
                noticed = CruciblePlugin.Session.Generation;
                MeadworksStepAside.ReportModule(Plugin.Guid, flooringPresent ? "flooring" : "fires_on_floors", source, "timber",
                    flooringPresent ? "step_aside_flooring" : "step_aside_fires");
            }
            var scene = ZNetScene.instance;
            int id = scene != null ? scene.GetInstanceID() : 0;
            int count = scene != null ? scene.m_namedPrefabs.Count : 0;
            bool active = Active;
            bool roofActive = RoofsActive;
            bool smeltersActive = SmeltersActive;
            if (id != sceneId || count != prefabCount || active != applied || roofActive != appliedRoofs || smeltersActive != appliedSmelters)
            {
                sceneId = id; prefabCount = count; applied = active; appliedRoofs = roofActive; appliedSmelters = smeltersActive;
                FirePlacement.ApplyAll();
                FloorRoofTags.ApplyAll();
            }
        }
        public static void Reset()
        {
            FirePlacement.Reset();
            FloorRoofTags.Reset();
            bindings?.Dispose(); bindings = null; enabled = null; roofs = null; smelters = null; rainProtection = null; appliedRoofs = appliedSmelters = false;
            if (CruciblePlugin.IsReady)
            {
                MeadworksStepAside.WithdrawModule(Plugin.Guid, "fires_on_floors");
                MeadworksStepAside.WithdrawModule(Plugin.Guid, "flooring");
            }
            resolved = flooringPresent = openkeepPresent = applied = false;
            openkeepWood = null; source = null; sceneId = 0; prefabCount = -1; next = 0; noticed = -1;
        }

        [HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.Awake))]
        static class SceneReady
        {
            [HarmonyPostfix, HarmonyPriority(Priority.Last)]
            static void Postfix()
            {
                if (!Plugin.Ready) return;
                FirePlacement.ApplyAll(); FloorRoofTags.ApplyAll();
            }
        }

        [HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.OnDestroy))]
        static class SceneEnding
        {
            [HarmonyPrefix]
            static void Prefix()
            {
                FirePlacement.Reset();
                FloorRoofTags.Reset();
                sceneId = 0; prefabCount = -1; next = 0;
            }
        }
    }
}
