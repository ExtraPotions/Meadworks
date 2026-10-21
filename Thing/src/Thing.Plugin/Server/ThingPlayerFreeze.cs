// Thing - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: a server-owned freeze for one connected player. The flag is not saved.
using System;
using System.Linq;
using System.Reflection;
using ExtraPotions.Meadworks.Crucible;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;
using ExtraPotions.Thing.Localization;
using ExtraPotions.Thing.Moderation;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Thing.Runtime
{
    public static class ThingPlayerFreeze
    {
        public const string RpcName = "ExtraPotions_Thing_PlayerFreezeV2";
        public const string AckRpcName = "ExtraPotions_Thing_PlayerFreezeAckV2";

        static Harmony? harmony;
        static MeadworksLog? log;
        static ZRoutedRpc? registered;
        static readonly PlayerFreezeLease lease = new PlayerFreezeLease();
        static bool controlsReady;
        static ZNet? connection;
        static long serverPeer;
        static bool generationKnown;
        static long generation = -1;

        public static void Initialize(MeadworksLog logger)
        {
            log = logger;
            if (harmony != null) return;
            var next = new Harmony(Plugin.Guid + ".player-freeze");
            var controls = AccessTools.GetDeclaredMethods(typeof(Player)).Where(method => method.Name == "SetControls").ToArray();
            if (controls.Length == 0) log?.Warning("Thing player freeze: Player.SetControls was missing.");
            controlsReady = controls.Length > 0;
            foreach (var method in controls) controlsReady &= TryPatch(next, method, nameof(BeforeControls), null);
            TryPatch(next, AccessTools.Method(typeof(Player), "FixedUpdate"), null, nameof(AfterMove));
            TryPatch(next, AccessTools.Method(typeof(Player), "UseHotbarItem"), nameof(BlockAction), null);
            TryPatch(next, AccessTools.Method(typeof(Player), "Interact"), nameof(BlockAction), null);
            TryPatch(next, AccessTools.Method(typeof(Player), "UpdatePlacement"), nameof(BlockAction), null);
            harmony = next;
        }

        static bool TryPatch(Harmony next, MethodInfo? method, string? prefix, string? postfix)
        {
            if (method == null)
            {
                log?.Warning("Thing player freeze: a player method was missing, so that input stays unchanged.");
                return false;
            }
            try
            {
                next.Patch(method,
                    prefix == null ? null : new HarmonyMethod(typeof(ThingPlayerFreeze), prefix),
                    postfix == null ? null : new HarmonyMethod(typeof(ThingPlayerFreeze), postfix));
                return true;
            }
            catch (Exception error)
            {
                log?.Warning("Thing player freeze: " + method.Name + " was not patched: " + error.GetType().Name);
                return false;
            }
        }

        public static void Tick()
        {
            var rpc = ZRoutedRpc.instance;
            if (rpc != null && !ReferenceEquals(rpc, registered))
            {
                rpc.Register<long, int>(RpcName, Receive);
                rpc.Register<long, int>(AckRpcName, Acknowledge);
                registered = rpc;
                lease.Clear();
            }
            var net = ZNet.instance;
            if (net == null)
            {
                lease.Clear();
                connection = null;
                serverPeer = 0;
                generationKnown = false;
                generation = -1;
                return;
            }
            var currentServer = rpc?.GetServerPeerID() ?? 0;
            if (!ReferenceEquals(connection, net) || serverPeer != currentServer)
            {
                connection = net;
                serverPeer = currentServer;
                lease.Clear();
            }
            var now = CruciblePlugin.IsReady ? CruciblePlugin.Session.Generation : -1;
            if (!generationKnown)
            {
                generationKnown = true;
                generation = now;
                return;
            }
            if (now == generation) return;
            generation = now;
            lease.Clear();
        }

        public static void Push(PlayerFreezeUpdate update)
        {
            var rpc = ZRoutedRpc.instance;
            if (rpc == null || update.Peer == 0) return;
            rpc.InvokeRoutedRPC(update.Peer, RpcName, update.Revision, update.Frozen ? 1 : 0);
        }

        static void Receive(long sender, long revision, int flag)
        {
            var rpc = ZRoutedRpc.instance;
            var net = ZNet.instance;
            if (rpc == null || net == null || (flag != 0 && flag != 1)) return;
            if (!net.IsServer() && sender != rpc.GetServerPeerID()) return;
            if (net.IsServer() && sender != rpc.m_id) return;
            var on = flag == 1;
            if (on && !controlsReady) return;
            var wasFrozen = lease.IsFrozen(Time.realtimeSinceStartup);
            if (!lease.Receive(revision, on, Time.realtimeSinceStartup)) return;
            rpc.InvokeRoutedRPC(sender, AckRpcName, revision, flag);
            if (on != wasFrozen && log != null) ThingText.Centre(ThingText.Text(on ? "player_frozen" : "player_unfrozen"), log);
        }

        static void Acknowledge(long sender, long revision, int flag)
        {
            if (flag != 0 && flag != 1) return;
            ThingServer.AcknowledgeFreeze(sender, revision, flag == 1);
        }

        static bool Local(Player player) => lease.IsFrozen(Time.realtimeSinceStartup) && player != null && ReferenceEquals(player, Player.m_localPlayer);

        static void BeforeControls(Player __instance, ref Vector3 movedir, ref bool attack, ref bool attackHold, ref bool secondaryAttack,
            ref bool secondaryAttackHold, ref bool block, ref bool blockHold, ref bool jump, ref bool crouch, ref bool run, ref bool autoRun, ref bool dodge)
        {
            if (!Local(__instance)) return;
            movedir = Vector3.zero;
            attack = attackHold = secondaryAttack = secondaryAttackHold = block = blockHold = jump = crouch = run = autoRun = dodge = false;
        }

        static void AfterMove(Player __instance)
        {
            if (!Local(__instance)) return;
            var body = __instance.GetComponent<Rigidbody>();
            if (body == null) return;
            var velocity = body.linearVelocity;
            velocity.x = 0f;
            velocity.z = 0f;
            body.linearVelocity = velocity;
        }

        static bool BlockAction(Player __instance) => !Local(__instance);

        public static void Shutdown()
        {
            harmony?.UnpatchSelf();
            harmony = null;
            registered = null;
            lease.Clear();
            controlsReady = false;
            connection = null;
            serverPeer = 0;
            generationKnown = false;
            generation = -1;
            log = null;
        }
    }
}
