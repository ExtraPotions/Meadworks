// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Original Crucible lifecycle/C05/C09 integration around the adopted camera core.
using ExtraPotions.Meadworks.Crucible.UI;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Timber.Runtime;
using Plugin = ExtraPotions.Timber.Runtime.Plugin;
using HarmonyLib;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace Timber.Camera
{
    public static class CameraModule
    {
        public const string Section = "Build Camera";
        public const string NoStation = "camera_no_station", EntryNeeds = "camera_entry_needs",
            Resting = "camera_resting", Comfort = "camera_comfort";
        static CameraBindings? bindings;
        static bool sourcesReady;
        static SourceMod? blocked;
        static SourceMod? openkeepSource;
        static ConfigEntry<bool>? openkeepEnabled;
        static bool carturPresent;
        static long noticedSession = -1;
        public static bool Allowed => sourcesReady && blocked == null && Plugin.Active && Plugin.AllowsUi && CameraSettings.Enabled.Value;

        public static void Initialize(ConfigFile config)
        {
            bindings = new CameraBindings(config);
            CameraSettings.Bind(bindings);
            CameraPrefs.Bind(bindings);
        }

        // Start runs after all BepInEx Awake methods, so loaded competitors can be inspected safely.
        public static void ResolveSources()
        {
            var cartur = new SourceMod("com.jekkle.valheim.carturbuildcamera", "Cartur's Build Camera");
            var azumatt = new SourceMod("Azumatt.BuildCameraCHE", "Build Camera Custom Hammers Edition");
            var openkeep = new SourceMod("milkyteam.openkeep", "OpenKeep Build Camera");
            if (CruciblePlugin.Services.StepAside.Declared(Plugin.Guid).Count == 0)
                MeadworksStepAside.Declare(Plugin.Guid, Plugin.Name, new[] { cartur, azumatt, openkeep, new SourceMod("com.jekkle.valheim.carturflooring", "Cartur's Flooring") });
            bool carturLoaded = Chainloader.PluginInfos.ContainsKey(cartur.Guid);
            bool azumattLoaded = Chainloader.PluginInfos.ContainsKey(azumatt.Guid);
            carturPresent = carturLoaded || azumattLoaded;
            if (carturPresent) blocked = carturLoaded ? cartur : azumatt;
            else if (Chainloader.PluginInfos.TryGetValue(openkeep.Guid, out var info))
            {
                // A loaded source with no readable setting fails closed; disabled OpenKeep camera does not block Timber.
                var source = info.Instance as BepInEx.BaseUnityPlugin;
                openkeepSource = openkeep;
                if (source != null) source.Config.TryGetEntry<bool>(new ConfigDefinition("11. Build Camera", "Enabled"), out openkeepEnabled);
                if (openkeepEnabled == null || openkeepEnabled.Value) blocked = openkeep;
            }
            sourcesReady = true;
        }

        public static void Tick()
        {
            if (!carturPresent && openkeepSource != null)
            {
                var next = openkeepEnabled == null || openkeepEnabled.Value ? openkeepSource : null;
                if (next != blocked)
                {
                    blocked = next; noticedSession = -1;
                    if (blocked == null) MeadworksStepAside.WithdrawModule(Plugin.Guid, "camera");
                }
            }
            if (!Allowed || Player.m_localPlayer == null) CameraState.Exit();
            if (blocked != null && Player.m_localPlayer != null && noticedSession != CruciblePlugin.Session.Generation)
            {
                noticedSession = CruciblePlugin.Session.Generation;
                MeadworksStepAside.ReportModule(Plugin.Guid, "camera", blocked, "timber", "step_aside_camera");
            }
        }

        public static void Center(string text)
        {
            if (Plugin.AllowsUi && MessageHud.instance != null)
            {
                var source = Plugin.Source;
                if (source != null && source.IsActive && CrucibleUiPlugin.Active)
                    KitNotices.Show(source, new KitNotice(text, KitNoticeKind.Center));
                else MessageHud.instance.ShowMessage(MessageHud.MessageType.Center, text);
            }
        }

        public static void Reset()
        {
            CameraState.Exit();
            CameraPickupHud.Reset();
            CircletLight.Reset();
            CircletSource.Reset();
            bindings?.Dispose(); bindings = null;
            if (CruciblePlugin.IsReady) MeadworksStepAside.WithdrawModule(Plugin.Guid, "camera");
            openkeepSource = null; openkeepEnabled = null; carturPresent = false;
            sourcesReady = false; blocked = null; noticedSession = -1;
        }
    }

    [HarmonyPatch(typeof(Player), nameof(Player.Update))]
    public static class CameraPlayerUpdatePatch
    {
        [HarmonyPostfix]
        public static void Postfix(Player __instance)
        {
            if (Plugin.Ready && Plugin.AllowsUi) CameraToggle.Tick(__instance);
        }
    }
}
