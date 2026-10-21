// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using ExtraPotions.Saga.Config;
using ExtraPotions.Saga.Powers;
using ExtraPotions.Saga.Runtime.Adopted.Grindstone;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Saga.Runtime
{
    /// <summary>
    /// Boss kill credit. The owner reports the death while the ZDO still exists. The server accepts that report only
    /// from the current owner of a known boss, then tells each player inside Kill Credit Range. A client applies the
    /// credit only when the sender is the server. A packet from another player is ignored, including the old
    /// player-view <c>saga.BossKill</c>.
    /// </summary>
    public static class BossKillCredit
    {
        public const string ReportRpc = "saga.BossDeath";
        public const string CreditRpc = "saga.BossKill";

        // OnDeath destroys the ZDO. A client-owned boss can deliver that removal before the health value, so a report
        // already tied to the owner is kept briefly and credited if the ZDO then reaches 0 health or disappears.
        const float WaitSeconds = 3f;
        const int MaxPending = 32;

        static readonly HashSet<ZDOID> seen = new HashSet<ZDOID>();
        static readonly List<Pending> pending = new List<Pending>();

        sealed class Pending
        {
            public long Sender;
            public int Prefab;
            public string Boss = "";
            public ZDOID Id;
            public Vector3 Position;
            public float Until;
        }

        [HarmonyPatch(typeof(ZNet), nameof(ZNet.Awake))]
        static class NetAwake
        {
            static void Postfix()
            {
                Clear();
                ZRoutedRpc.instance?.Register<ZPackage>(ReportRpc, OnReport);
                ZRoutedRpc.instance?.Register<ZPackage>(CreditRpc, OnCredit);
            }
        }

        public static void Clear()
        {
            seen.Clear();
            pending.Clear();
        }

        public static void Poll()
        {
            try { Drain(); }
            catch (Exception error) { NativeSaga.Report(error, "boss credit"); }
        }

        public static void Report(Character character)
        {
            try { ReportInner(character); }
            catch (Exception error) { NativeSaga.Report(error, "boss credit"); }
        }

        static void ReportInner(Character character)
        {
            if (!NativeSaga.Active("powers") || character == null || character.m_nview == null || !character.m_nview.IsValid() || !character.m_nview.IsOwner())
                return;
            if (character.GetHealth() > 0f) return;
            var zdo = character.m_nview.GetZDO();
            if (zdo == null) return;
            var name = Utils.GetPrefabName(character.gameObject);
            if (!NativePowers.KnownBossPrefab(name, out _)) return;
            int hash = name.GetStableHashCode();
            if (zdo.GetPrefab() != hash) return;
            var id = zdo.m_uid;
            var pos = character.transform.position;
            var net = ZNet.instance;
            var rpc = ZRoutedRpc.instance;
            if (net == null || rpc == null) return;
            if (net.IsServer()) Accept(ZNet.GetUID(), hash, id, pos);
            else
            {
                long server = net.GetServerPeer()?.m_uid ?? 0L;
                if (server == 0L) return;
                var pkg = new ZPackage();
                pkg.Write(hash);
                pkg.Write(id);
                pkg.Write(pos);
                rpc.InvokeRoutedRPC(server, ReportRpc, pkg);
            }
        }

        static void OnReport(long sender, ZPackage pkg)
        {
            try
            {
                int hash = pkg.ReadInt();
                var id = pkg.ReadZDOID();
                var pos = pkg.ReadVector3();
                Accept(sender, hash, id, pos);
            }
            catch (Exception error) { NativeSaga.Report(error, "boss credit"); }
        }

        static void Accept(long sender, int prefab, ZDOID id, Vector3 pos)
        {
            var net = ZNet.instance;
            if (net == null || !net.IsServer() || id.IsNone() || seen.Contains(id)) return;
            var zdo = ZDOMan.instance != null ? ZDOMan.instance.GetZDO(id) : null;
            if (zdo == null || !zdo.IsValid()) return;
            bool owns = zdo.GetOwner() == sender;
            string boss = "";
            bool known = zdo.GetPrefab() == prefab && NativePowers.KnownBossHash(prefab, out boss);
            bool dead = zdo.GetFloat(ZDOVars.s_health, 1f) <= 0f;
            if (BossKillRules.ServerAccepts(owns, known, dead, true))
            {
                seen.Add(id);
                Forget(id);
                Broadcast(boss, id, pos);
                return;
            }
            if (owns && known) Hold(sender, prefab, boss, id, pos);
        }

        static void Hold(long sender, int prefab, string boss, ZDOID id, Vector3 pos)
        {
            for (int i = 0; i < pending.Count; i++)
                if (pending[i].Id.Equals(id)) return;
            if (pending.Count >= MaxPending) return;
            pending.Add(new Pending
            {
                Sender = sender,
                Prefab = prefab,
                Boss = boss,
                Id = id,
                Position = pos,
                Until = Time.unscaledTime + WaitSeconds,
            });
        }

        static void Drain()
        {
            if (ZNet.instance == null || !ZNet.instance.IsServer() || pending.Count == 0) return;
            if (!NativeSaga.Active("powers"))
            {
                pending.Clear();
                return;
            }
            float now = Time.unscaledTime;
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                var item = pending[i];
                if (seen.Contains(item.Id) || now >= item.Until)
                {
                    pending.RemoveAt(i);
                    continue;
                }
                var zdo = ZDOMan.instance != null ? ZDOMan.instance.GetZDO(item.Id) : null;
                if (zdo == null || !zdo.IsValid())
                {
                    // The owner reported this boss and the ZDO was then removed, which is how OnDeath ends.
                    if (BossKillRules.ServerAccepts(true, NativePowers.IsKnownBoss(item.Boss), true, true))
                    {
                        seen.Add(item.Id);
                        Broadcast(item.Boss, item.Id, item.Position);
                    }
                    pending.RemoveAt(i);
                    continue;
                }
                bool owns = zdo.GetOwner() == item.Sender;
                bool known = zdo.GetPrefab() == item.Prefab && NativePowers.IsKnownBoss(item.Boss);
                bool dead = zdo.GetFloat(ZDOVars.s_health, 1f) <= 0f;
                if (BossKillRules.ServerAccepts(owns, known, dead, true))
                {
                    seen.Add(item.Id);
                    Broadcast(item.Boss, item.Id, item.Position);
                    pending.RemoveAt(i);
                    continue;
                }
                if (!owns || !known) pending.RemoveAt(i);
            }
        }

        static void Forget(ZDOID id)
        {
            for (int i = pending.Count - 1; i >= 0; i--)
                if (pending[i].Id.Equals(id)) pending.RemoveAt(i);
        }

        static void Broadcast(string boss, ZDOID id, Vector3 pos)
        {
            var rpc = ZRoutedRpc.instance;
            if (rpc == null || string.IsNullOrEmpty(boss)) return;
            float range = Plugin.Settings != null
                ? Plugin.Settings.Get(SagaSettings.Powers.KillCreditRange)
                : SagaSettings.Powers.KillCreditRange.Default;
            var candidates = new List<CreditCandidate>();
            var peers = new Dictionary<long, long>();
            var listed = new HashSet<long>();
            var local = Player.m_localPlayer;
            if (local != null)
            {
                var at = local.transform.position;
                candidates.Add(new CreditCandidate(local.GetPlayerID(), at.x, at.y, at.z, !local.IsDead()));
                peers[local.GetPlayerID()] = ZNet.GetUID();
                listed.Add(local.GetPlayerID());
            }
            if (ZNet.instance != null && ZDOMan.instance != null)
            {
                foreach (var info in ZNet.instance.GetPlayerList())
                {
                    if (info.m_characterID.IsNone()) continue;
                    var zdo = ZDOMan.instance.GetZDO(info.m_characterID);
                    if (zdo == null || !zdo.IsValid()) continue;
                    long playerId = zdo.GetLong(ZDOVars.s_playerID);
                    if (playerId == 0L || !listed.Add(playerId)) continue;
                    var at = zdo.GetPosition();
                    bool alive = zdo.GetFloat(ZDOVars.s_health, 1f) > 0f;
                    candidates.Add(new CreditCandidate(playerId, at.x, at.y, at.z, alive));
                    long owner = zdo.GetOwner();
                    if (owner != 0L) peers[playerId] = owner;
                }
            }
            foreach (var playerId in KillCredit.Eligible(pos.x, pos.y, pos.z, range, candidates))
            {
                if (!peers.TryGetValue(playerId, out var peer) || peer == 0L) continue;
                var pkg = new ZPackage();
                pkg.Write(boss);
                pkg.Write(id);
                pkg.Write(pos);
                rpc.InvokeRoutedRPC(peer, CreditRpc, pkg);
            }
        }

        static void OnCredit(long sender, ZPackage pkg)
        {
            try
            {
                string boss = pkg.ReadString();
                var id = pkg.ReadZDOID();
                var pos = pkg.ReadVector3();
                bool known = NativePowers.IsKnownBoss(boss);
                bool first = !NativePowers.AlreadyCredited(id);
                if (!BossKillRules.ClientAccepts(sender == ServerPeer(), known, InRange(pos), first)) return;
                NativePowers.ApplyKill(boss, id);
            }
            catch (Exception error) { NativeSaga.Report(error, "boss credit"); }
        }

        static bool InRange(Vector3 pos)
        {
            var player = Player.m_localPlayer;
            if (player == null || player.IsDead() || Plugin.Settings == null) return false;
            var at = player.transform.position;
            float range = Plugin.Settings.Get(SagaSettings.Powers.KillCreditRange);
            return KillCredit.Eligible(pos.x, pos.y, pos.z, range, new[]
            {
                new CreditCandidate(player.GetPlayerID(), at.x, at.y, at.z, true),
            }).Count > 0;
        }

        static long ServerPeer()
        {
            var net = ZNet.instance;
            if (net == null) return 0L;
            if (net.IsServer()) return ZNet.GetUID();
            var peer = net.GetServerPeer();
            return peer != null ? peer.m_uid : 0L;
        }
    }
}
