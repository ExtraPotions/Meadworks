// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Original integration of the game's clock; VAT is a behaviour only reference, with no mod code adopted.
using System;
using System.Linq;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Thing.Admin;
using ExtraPotions.Thing.Commands;
using ExtraPotions.Thing.Config;
using ExtraPotions.Thing.Integration;
using ExtraPotions.Thing.Moderation;
using ExtraPotions.Thing.WorldAdmin;
using HarmonyLib;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Thing.Runtime
{
    public static class ThingTime
    {
        static readonly GlobalTimeState state = new GlobalTimeState();
        static Harmony? harmony;
        static ZNet? bound;
        static long generation = -1;
        public static bool Frozen => state.Frozen;
        internal static void Initialize()
        {
            harmony = new Harmony(Plugin.Guid + ".global-time");
            harmony.Patch(AccessTools.Method(typeof(ZNet), "UpdateNetTime"), prefix: new HarmonyMethod(typeof(ThingTime), nameof(BeforeClock)));
        }
        static void BeforeClock(ZNet __instance, ref float dt)
        {
            if (state.Frozen && ReferenceEquals(__instance, bound) && Maintain()) dt = 0;
        }
        static bool Maintain()
        {
            var net = ZNet.instance;
            if (!state.Frozen) return false;
            if (!Plugin.Ready || net == null || !net.IsServer() || !ReferenceEquals(net, bound)
                || generation != CruciblePlugin.Session.Generation || !CruciblePlugin.Session.AuthorityReady
                || !ThingModules.Active(ThingModule.WorldTools) || Plugin.Settings == null)
            { state.Release(); bound = null; return false; }
            if (state.ShouldRelease(id => net.GetPeers().Any(peer => peer != null && peer.IsReady() && peer.m_rpc != null
                    && peer.m_rpc.IsConnected() && peer.m_socket?.GetHostName() == id),
                    id => net.IsAdmin(id) || AdminPermissions.Covers(ThingAccess.Of(id), ThingAction.GlobalTime),
                    Plugin.Settings.Get(ThingSettings.World.GlobalTimeReleaseOnLeave)))
            {
                ThingServer.Audit(new Actor(state.SetBy, ""), ThingAction.ReleaseTime, default, AuditOutcome.Done, "time freeze owner left or lost admin rights");
                state.Release(); bound = null; return false;
            }
            return true;
        }
        internal static void Tick() { if (state.Frozen) Maintain(); }
        internal static string Run(ParsedCommand command, Actor admin, bool localAuthority)
        {
            var net = ZNet.instance;
            var env = EnvMan.instance;
            if (!Plugin.Ready || net == null || !net.IsServer() || !ThingServer.Active || !CruciblePlugin.Session.AuthorityReady
                || !ThingModules.Active(ThingModule.WorldTools) || env == null
                || (!localAuthority && !net.IsAdmin(admin.Id) && !AdminPermissions.Covers(ThingAccess.Of(admin.Id), command.Action)))
                return ThingText.Text("time_unavailable");
            if (command.Action == ThingAction.ReleaseTime)
            {
                state.Release(); bound = null;
                ThingServer.Audit(admin, ThingAction.ReleaseTime, default, AuditOutcome.Done, "global time resumed");
                return ThingText.Text("time_resumed");
            }
            var fraction = TimeOfDay.Parse(command.Text);
            var seconds = fraction.HasValue ? TimeOfDay.AtCurrentDay(net.GetTimeSeconds(), env.m_dayLengthSec, fraction.Value) : null;
            if (!seconds.HasValue) return ThingText.Text("time_unavailable");
            net.SetNetTime(seconds.Value);
            state.Apply(localAuthority ? "" : admin.Id, fraction!.Value, command.Freeze);
            bound = net; generation = CruciblePlugin.Session.Generation;
            ThingServer.Audit(admin, ThingAction.GlobalTime, default, AuditOutcome.Done, command.Freeze ? "global time set and frozen" : "global time set");
            return ThingText.Format(command.Freeze ? "time_frozen" : "time_set", TimeOfDay.Format(fraction.Value));
        }
        internal static void Shutdown()
        {
            state.Release(); bound = null; generation = -1;
            harmony?.UnpatchSelf(); harmony = null;
        }
    }
}
