// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using ExtraPotions.Bestiary.Fixes;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Kraken
{
    /// <summary>
    /// The kraken is a boss (the big health bar), so the game may count it among the bosses in a fight: the count the
    /// world's "no portals while a boss is up" setting reads, which only a boss's death takes back off. Its AI never
    /// raises the count (it cannot be alerted the game's way), but should anything else have, a kraken that leaves
    /// without dying takes itself off it as a death would. Only the server writes that count, from the server's own
    /// number. A client that owns the kraken asks. It does not send a count.
    /// </summary>
    internal static class KrakenBoss
    {
        private const string ReleaseRpc = "ecp_kraken_boss_release";
        private const string ReleasedRpc = "ecp_kraken_boss_released";
        private const float AskAgain = 2f;

        private static readonly int KrakenHash = KrakenPrefabs.Creature.GetStableHashCode();
        private static readonly HashSet<ZDOID> Done = new HashSet<ZDOID>();
        private static readonly Dictionary<ZDOID, float> Waiting = new Dictionary<ZDOID, float>();
        private static ZRoutedRpc? bound;

        /// <summary>Register the release calls on this world's routed RPC. Safe to call more than once.</summary>
        public static void Bind()
        {
            ZRoutedRpc? rpc = ZRoutedRpc.instance;
            if (rpc == null || ReferenceEquals(rpc, bound))
            {
                return;
            }
            rpc.Register<ZDOID>(ReleaseRpc, OnRelease);
            rpc.Register<ZDOID>(ReleasedRpc, OnReleased);
            bound = rpc;
            Done.Clear();
            Waiting.Clear();
        }

        /// <summary>True when the boss count has been released, or there was nothing to release, and the kraken may be removed.</summary>
        public static bool Released(Character kraken)
        {
            Bind();
            ZDO? zdo = kraken != null && kraken.m_nview != null && kraken.m_nview.IsValid() ? kraken.m_nview.GetZDO() : null;
            if (zdo == null)
            {
                return true;
            }
            ZDOID id = zdo.m_uid;
            if (Done.Contains(id))
            {
                if (zdo.GetBool(ZDOVars.s_bossCount))
                {
                    zdo.Set(ZDOVars.s_bossCount, false);
                }
                return true;
            }
            if (!zdo.GetBool(ZDOVars.s_bossCount))
            {
                return true;
            }
            if (ZNet.instance != null && ZNet.instance.IsServer())
            {
                return Apply(zdo, owns: true);
            }
            Ask(zdo);
            return false;
        }

        private static void Ask(ZDO zdo)
        {
            ZRoutedRpc? rpc = ZRoutedRpc.instance;
            if (rpc == null)
            {
                return;
            }
            ZDOID id = zdo.m_uid;
            float now = Time.time;
            if (Waiting.TryGetValue(id, out float sent) && now - sent < AskAgain)
            {
                return;
            }
            rpc.InvokeRoutedRPC(rpc.GetServerPeerID(), ReleaseRpc, id);
            Waiting[id] = now;
        }

        private static void OnRelease(long sender, ZDOID id)
        {
            if (ZNet.instance == null || !ZNet.instance.IsServer() || ZDOMan.instance == null)
            {
                return;
            }
            if (Done.Contains(id))
            {
                Reply(sender, id);
                return;
            }
            ZDO? zdo = ZDOMan.instance.GetZDO(id);
            bool owns = zdo != null && sender == zdo.GetOwner();
            if (Apply(zdo, owns))
            {
                Reply(sender, id);
            }
        }

        private static void OnReleased(long sender, ZDOID id)
        {
            if (!FromServer(sender))
            {
                return;
            }
            Done.Add(id);
            Waiting.Remove(id);
            ZDO? zdo = ZDOMan.instance != null ? ZDOMan.instance.GetZDO(id) : null;
            if (zdo != null && zdo.GetBool(ZDOVars.s_bossCount))
            {
                zdo.Set(ZDOVars.s_bossCount, false);
            }
        }

        /// <summary>Server only. One decrement of the server's own count, for a kraken this sender owns that was counted.</summary>
        private static bool Apply(ZDO? zdo, bool owns)
        {
            if (zdo == null || ZoneSystem.instance == null || ZNet.instance == null || !BossCountRules.MayWrite(ZNet.instance.IsServer()))
            {
                return false;
            }
            if (Done.Contains(zdo.m_uid))
            {
                zdo.Set(ZDOVars.s_bossCount, false);
                return true;
            }
            bool counted = zdo.GetBool(ZDOVars.s_bossCount);
            bool kraken = zdo.GetPrefab() == KrakenHash;
            if (!BossCountRules.Accept(true, owns, false, counted, kraken))
            {
                return false;
            }
            float count = 0f;
            ZoneSystem.instance.GetGlobalKey(GlobalKeys.activeBosses, out count);
            ZoneSystem.instance.SetGlobalKey(GlobalKeys.activeBosses, BossCountRules.Decrement(count));
            zdo.Set(ZDOVars.s_bossCount, false);
            Done.Add(zdo.m_uid);
            return true;
        }

        private static void Reply(long sender, ZDOID id) =>
            ZRoutedRpc.instance?.InvokeRoutedRPC(sender, ReleasedRpc, id);

        private static bool FromServer(long sender)
        {
            if (ZNet.instance == null || ZRoutedRpc.instance == null)
            {
                return false;
            }
            if (ZNet.instance.IsServer())
            {
                return sender == ZRoutedRpc.instance.m_id;
            }
            ZNetPeer? server = ZNet.instance.GetServerPeer();
            return server != null && sender == server.m_uid;
        }
    }
}
