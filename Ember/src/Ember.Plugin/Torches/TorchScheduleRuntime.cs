// Adapted from OpenKeep/OpenKeep/src/Homestead/TorchNight.cs, TorchSwitch.cs, TorchKeep.cs and TorchHoverPatch.cs.
// Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later.
// Pin: 684671f98de54db6ea6652092ec041f44016c37d
// Changed by expDARE, 2026: default-off module scope, pre-debit phase updates and existing authenticated owner commands.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Bootstrap;
using ExtraPotions.Ember.Config;
using ExtraPotions.Ember.Integration;
using ExtraPotions.Ember.Torches;
using ExtraPotions.Meadworks.Crucible;
using HarmonyLib;

namespace ExtraPotions.Ember.Runtime
{
    public static class TorchScheduleRuntime
    {
        public const string PhaseKey = "meadworks.ember.torch.phase", KeepKey = "meadworks.ember.torch.keep";
        public const string SourceGuid = "milkyteam.openkeep";
        static string? listText;
        static HashSet<int> listed = new HashSet<int>();
        static bool? reportedSource;
        public static bool SourcePresent => Chainloader.PluginInfos.ContainsKey(SourceGuid);
        public static bool Enabled => Plugin.Settings != null && EmberModules.Active(EmberModule.Torches)
            && Plugin.Settings.Get(EmberSettings.Torches.TorchesNightOnly) && !SourcePresent;

        internal static void Initialize(ProductRegistration registration) => TorchKeepCommands.Initialize(registration);

        internal static bool Listed(ZDO? zdo)
        {
            if (zdo == null) return false;
            string text = Plugin.Settings?.Get(EmberSettings.Torches.TorchPieces) ?? "";
            if (text != listText)
            {
                listText = text;
                listed = new HashSet<int>(FirePieceList.Parse(text).Select(name => name.GetStableHashCode()));
            }
            return listed.Contains(zdo.GetPrefab());
        }

        internal static bool PlayerFire(Fireplace fire, bool mustOwn)
        {
            if (!fire || fire.gameObject.layer == Piece.s_ghostLayer) return false;
            var view = fire.GetComponent<ZNetView>();
            var piece = fire.GetComponent<Piece>();
            return view && view.IsValid() && (!mustOwn || view.IsOwner()) && piece && piece.IsPlacedByPlayer();
        }

        public static bool Switchable(Fireplace fire)
        {
            if (!Enabled || !PlayerFire(fire, false)) return false;
            var view = fire.GetComponent<ZNetView>();
            return view != null && view.IsValid() && Listed(view.GetZDO());
        }

        public static bool? NightNow()
        {
            var env = EnvMan.instance;
            if (!env || !ZNet.instance || Plugin.Settings == null) return null;
            float fraction = env.GetDayFraction();
            if (!env.m_debugTimeOfDay)
            {
                float day = env.m_dayLengthSec;
                if (!(day > 0) || float.IsInfinity(day)) return null;
                float clock = (float)(ZNet.instance.GetTimeSeconds() % day / day);
                if (!TorchSchedule.Settled(fraction, env.RescaleDayFraction(clock))) return null;
            }
            float margin = Plugin.Settings!.Get(EmberSettings.Torches.TorchMargin) / 24f;
            return TorchSchedule.IsNight(fraction, env.RescaleDayFraction(EmberSeasons.Dawn + margin), env.RescaleDayFraction(1f - EmberSeasons.Dawn - margin));
        }

        public static void Apply(Fireplace fire)
        {
            if (!PlayerFire(fire, true) || !ZNet.instance || !ZNetScene.instance) return;
            var view = fire.GetComponent<ZNetView>();
            var zdo = view != null && view.IsValid() ? view.GetZDO() : null;
            if (zdo == null) return;
            var change = TorchSchedule.Step(Enabled, Listed(zdo), zdo.GetBool(KeepKey), zdo.GetInt(PhaseKey), Enabled ? NightNow() : null);
            if (!change.Write) return;
            if (change.On.HasValue) SetOn(fire, change.On.Value);
            zdo.Set(PhaseKey, change.Phase);
        }

        internal static void SetOn(Fireplace fire, bool on)
        {
            var view = fire.GetComponent<ZNetView>();
            if (view && view.IsValid() && view.IsOwner() && (view.GetZDO().GetInt(ZDOVars.s_state, 1) == 1) != on)
                view.InvokeRPC("RPC_ToggleOn");
        }

        internal static void Tick()
        {
            bool source = SourcePresent;
            if (reportedSource != source)
            {
                reportedSource = source;
                if (source) MeadworksStepAside.ReportModule(Plugin.Guid, "torch_schedule", new SourceMod(SourceGuid, "OpenKeep"));
                else MeadworksStepAside.WithdrawModule(Plugin.Guid, "torch_schedule");
            }
            TorchKeepCommands.Tick();
        }

        internal static void Shutdown()
        {
            TorchKeepCommands.Shutdown();
            foreach (var fire in TorchRuntime.Registered.ToArray())
            {
                if (!PlayerFire(fire, true) || !ZNet.instance || !ZNetScene.instance) continue;
                var view = fire.GetComponent<ZNetView>();
                var zdo = view != null && view.IsValid() ? view.GetZDO() : null;
                if (zdo == null) continue;
                if (zdo.GetInt(PhaseKey) == TorchSchedule.Day) SetOn(fire, true);
                if (zdo.GetInt(PhaseKey) != 0) zdo.Set(PhaseKey, 0);
            }
            listText = null; listed.Clear(); reportedSource = null;
            if (ExtraPotions.Meadworks.Crucible.Plugin.IsReady) MeadworksStepAside.WithdrawModule(Plugin.Guid, "torch_schedule");
        }
    }

    [HarmonyPatch(typeof(Fireplace), nameof(Fireplace.GetHoverText))]
    internal static class TorchScheduleHover
    {
        [HarmonyPostfix]
        static void Postfix(Fireplace __instance, ref string __result)
        {
            var player = Player.m_localPlayer;
            if (string.IsNullOrEmpty(__result) || !player || Plugin.Settings == null || !TorchScheduleRuntime.Switchable(__instance)
                || !PrivateArea.CheckAccess(__instance.transform.position, 0f, false)) return;
            var view = __instance.GetComponent<ZNetView>();
            var zdo = view != null && view.IsValid() ? view.GetZDO() : null;
            if (zdo == null) return;
            if (zdo.GetInt(TorchScheduleRuntime.PhaseKey) == TorchSchedule.Day && zdo.GetInt(ZDOVars.s_state, 1) != 1)
                __result += "\n" + EmberText.Text("torch_nightfall");
            var key = Plugin.Settings.Shortcut(EmberSettings.Torches.TorchSwitchKey);
            if (key.MainKey != UnityEngine.KeyCode.None)
                __result += "\n[<color=yellow><b>" + string.Join(" + ", key.Modifiers.Select(k => k.ToString()).Concat(new[] { key.MainKey.ToString() })) + "</b></color>] "
                    + EmberText.Text(zdo.GetBool(TorchScheduleRuntime.KeepKey) ? "torch_schedule" : "torch_keep");
        }
    }
}
