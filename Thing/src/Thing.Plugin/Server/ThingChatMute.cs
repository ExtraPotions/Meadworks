// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: reject native chat at its authenticated server ingress, before local dispatch and forwarding.
using System;
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Thing.Localization;
using HarmonyLib;
using UnityEngine;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Thing.Runtime
{
    internal static class ThingChatMute
    {
        static readonly int GlobalChat = "ChatMessage".GetStableHashCode();
        static readonly int CharacterChat = "Say".GetStableHashCode();
        static readonly Dictionary<ZRpc, float> notices = new Dictionary<ZRpc, float>();
        static Harmony? harmony;

        internal static void Initialize()
        {
            if (harmony != null) return;
            var next = new Harmony(Plugin.Guid + ".chat-mute");
            try
            {
                next.Patch(AccessTools.Method(typeof(ZRoutedRpc), "RPC_RoutedRPC"),
                    prefix: new HarmonyMethod(typeof(ThingChatMute), nameof(BeforeIncoming)));
                harmony = next;
            }
            catch { next.UnpatchSelf(); throw; }
        }

        static bool BeforeIncoming(ZRpc __0, ZPackage __1)
        {
            var net = ZNet.instance;
            if (!ThingServer.CurrentAuthority || !Plugin.Ready || !CruciblePlugin.IsReady || !CruciblePlugin.Session.AuthorityReady
                || !ThingModules.ProductActive(out _) || net == null || __0 == null || !__0.IsConnected()) return true;
            var peer = net.GetPeer(__0);
            if (peer == null || !ReferenceEquals(peer.m_rpc, __0) || !peer.IsReady() || peer.m_socket == null) return true;
            var id = peer.m_socket.GetHostName();
            if (string.IsNullOrEmpty(id) || !ThingServer.Ledger.IsMuted(id!, DateTime.UtcNow)) return true;
            var position = __1.GetPos();
            var chat = false;
            try
            {
                // Read fixed native header fields only; sender IDs in this payload never establish moderation identity.
                __1.ReadLong(); __1.ReadLong(); __1.ReadLong();
                var target = __1.ReadZDOID();
                var method = __1.ReadInt();
                chat = method == GlobalChat && target.IsNone() || method == CharacterChat && !target.IsNone();
                if (!chat) return true;
                if (__1.Size() > 65536) return Deny(peer);
                var length = __1.ReadInt();
                var minimum = method == GlobalChat ? 16 : 4;
                if (length < minimum || length > __1.Size() - __1.GetPos()) return Deny(peer);
                // Native package bytes follow the length. Inspect no user strings and allocate no second package.
                if (method == GlobalChat) __1.ReadVector3();
                var type = __1.ReadInt();
                if (method == GlobalChat && type == (int)Talker.Type.Ping) return true;
                return Deny(peer);
            }
            catch { return chat ? Deny(peer) : true; }
            finally { __1.SetPos(position); }
        }

        static bool Deny(ZNetPeer peer)
        {
            var now = Time.realtimeSinceStartup;
            if (!notices.TryGetValue(peer.m_rpc, out var previous) || now - previous >= 2)
            {
                foreach (var rpc in notices.Keys.Where(rpc => !rpc.IsConnected()).ToArray()) notices.Remove(rpc);
                if (notices.Count >= 256) notices.Clear();
                notices[peer.m_rpc] = now;
                try { ThingNotices.Send(peer.m_uid, ThingTextKeys.YouAreMuted); } catch { /* Notice failure must not forward rejected chat. */ }
            }
            return false;
        }

        internal static void Reset() => notices.Clear();
        internal static void Shutdown() { harmony?.UnpatchSelf(); harmony = null; Reset(); }
    }
}
