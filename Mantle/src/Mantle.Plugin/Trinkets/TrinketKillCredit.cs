// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Mantle.Trinkets;

internal static class TrinketKillCredit
{
    private const string Hello = "Mantle_Trinkets_Hello_v1";
    private const string Ack = "Mantle_Trinkets_Ack_v1";
    private const string Gate = "Mantle_Trinkets_Gate_v1";
    private const string Death = "Mantle_Trinkets_Death_v1";
    private const string Award = "Mantle_Trinkets_Award_v1";
    private sealed class PeerState { internal bool Supported; internal float NextHello; internal int Attempts; }
    private sealed class Pending { internal string Prefab; internal float Multiplier; }
    private static readonly Dictionary<ZNetPeer, PeerState> peers = new();
    private static readonly DeathCreditLedger serverCredits = new();
    private static readonly DeathCreditLedger receivedCredits = new();
    private static readonly Queue<Pending> pending = new();
    private static readonly List<ZNetPeer> departed = new();
    private static ZNet session;
    private static bool serverGate;
    private static bool previousGate;
    private static Harmony edgeHarmony;
    private static MethodInfo edgeMethod;
    private static bool granting;
    internal static bool TuningSupported { get; private set; }

    internal static void Tick()
    {
        if (session != ZNet.instance)
        {
            ClearSession();
            session = ZNet.instance;
        }
        if (!session || !Plugin.WorldReady) { TuningSupported = false; return; }
        var current = session.GetPeers();
        departed.Clear();
        foreach (var peer in peers.Keys)
            if (!current.Contains(peer)) departed.Add(peer);
        foreach (var gone in departed) { Unregister(gone); peers.Remove(gone); }
        departed.Clear();
        foreach (var peer in current)
        {
            if (peers.ContainsKey(peer)) continue;
            peers.Add(peer, new PeerState());
            peer.m_rpc.Register<int>(Hello, OnHello);
            peer.m_rpc.Register<int>(Ack, OnAck);
            peer.m_rpc.Register<bool>(Gate, OnGate);
            peer.m_rpc.Register<ZPackage>(Death, OnDeathReport);
            peer.m_rpc.Register<ZPackage>(Award, OnAward);
        }
        foreach (var pair in peers)
        {
            // A greeting can precede the other side's registration during join; bounded retries avoid that race.
            if (!pair.Value.Supported && pair.Value.Attempts < 3 && Time.unscaledTime >= pair.Value.NextHello)
            {
                pair.Value.Attempts++;
                pair.Value.NextHello = Time.unscaledTime + 5f;
                pair.Key.m_rpc.Invoke(Hello, 1);
            }
        }
        bool supported = session.IsServer();
        if (supported)
        {
            foreach (var state in peers.Values) supported &= state.Supported;
        }
        else
        {
            var server = session.GetServerPeer();
            supported = server != null && peers.TryGetValue(server, out var state) && state.Supported && serverGate;
        }
        TuningSupported = supported;
        if (session.IsServer() && (supported != previousGate || current.Count != 0))
        {
            // Send only on transitions or a new handshake, not every frame.
            if (supported != previousGate)
                foreach (var peer in current) if (peers[peer].Supported) peer.m_rpc.Invoke(Gate, supported);
            previousGate = supported;
        }
        EnsureEdgeBoundary();
        while (pending.Count > 0)
        {
            var credit = pending.Dequeue();
            var player = Player.m_localPlayer;
            if (!TuningSupported || !player || player.IsDead() || !player.IsOwner()
                || !TrinketRuntime.TryGetEquippedNative(player, out string prefab) || prefab != credit.Prefab
                || !TrinketRuntime.TryGetKillFraction(player, out float fraction)
                || !TrinketRuntime.TryGetEffectiveMaximum(player, out float maximum)) continue;
            float gain = KillGain.Calculate(fraction, maximum, credit.Multiplier);
            if (gain <= 0f || !EdgeBoundaryAvailable()) continue;
            // LateUpdate is outside attack/block scopes; Edge's explicit boundary also covers a still-active secondary.
            granting = true;
            try { player.AddAdrenaline(gain); }
            finally { granting = false; }
        }
    }

    private static void OnHello(ZRpc rpc, int version)
    {
        if (version != 1 || !MarkSupported(rpc)) return;
        rpc.Invoke(Ack, 1);
        if (session.IsServer()) SendGateToSupported();
    }
    private static void OnAck(ZRpc rpc, int version)
    {
        if (version == 1 && MarkSupported(rpc) && session.IsServer()) SendGateToSupported();
    }
    private static bool MarkSupported(ZRpc rpc)
    {
        var peer = FindPeer(rpc);
        if (peer == null || !peers.TryGetValue(peer, out var state)) return false;
        state.Supported = true;
        return true;
    }
    private static void SendGateToSupported()
    {
        bool supported = true;
        foreach (var state in peers.Values) supported &= state.Supported;
        foreach (var pair in peers) if (pair.Value.Supported) pair.Key.m_rpc.Invoke(Gate, supported);
    }
    private static void OnGate(ZRpc rpc, bool value)
    {
        if (session && !session.IsServer() && ReferenceEquals(session.GetServerPeer()?.m_rpc, rpc)) serverGate = value;
    }
    private static ZNetPeer FindPeer(ZRpc rpc)
    {
        if (!session) return null;
        foreach (var peer in session.GetPeers()) if (ReferenceEquals(peer.m_rpc, rpc)) return peer;
        return null;
    }

    internal static void Report(Character victim)
    {
        if (!TuningSupported || !TrinketSettings.KillRewardsEnabled || !victim || victim.IsPlayer() || victim.IsTamed() || victim.GetHealth() > 0f
            || !victim.m_nview || !victim.m_nview.IsOwner() || victim.m_lastHit?.GetAttacker() is not Player killer
            || !killer.m_nview || !BaseAI.IsEnemy(killer, victim)
            || !(victim.m_enemyAdrenalineMultiplier > 0f) || float.IsInfinity(victim.m_enemyAdrenalineMultiplier)) return;
        var pkg = new ZPackage();
        pkg.Write(victim.m_nview.GetZDO().m_uid);
        pkg.Write(killer.m_nview.GetZDO().m_uid);
        pkg.Write(victim.m_enemyAdrenalineMultiplier);
        if (session.IsServer()) { pkg.SetPos(0); ValidateDeath(ZNet.GetUID(), pkg); }
        else session.GetServerPeer()?.m_rpc.Invoke(Death, pkg);
    }
    private static void OnDeathReport(ZRpc rpc, ZPackage pkg)
    {
        var peer = FindPeer(rpc);
        if (!session || !session.IsServer() || peer == null || !peers.TryGetValue(peer, out var state) || !state.Supported) return;
        ValidateDeath(peer.m_uid, pkg);
    }
    private static void ValidateDeath(long owner, ZPackage pkg)
    {
        if (!TuningSupported || !TrinketSettings.KillRewardsEnabled || pkg.Size() > 128 || ZDOMan.instance == null || !ZNetScene.instance) return;
        try
        {
            ZDOID victimId = pkg.ReadZDOID(), killerId = pkg.ReadZDOID();
            float multiplier = pkg.ReadSingle();
            if (victimId == ZDOID.None || killerId == ZDOID.None || victimId == killerId
                || !(multiplier > 0f) || float.IsInfinity(multiplier)) return;
            ZDO victim = ZDOMan.instance.GetZDO(victimId), killer = ZDOMan.instance.GetZDO(killerId);
            if (victim == null || killer == null || victim.GetOwner() != owner || victim.GetBool(ZDOVars.s_tamed)) return;
            // Native death simulation belongs to this ZDO's current owner. The server validates that report,
            // prefab hostility and recipient identity; this is not remote execution attestation.
            GameObject victimObject = ZNetScene.instance.FindInstance(victimId);
            Character target = victimObject ? victimObject.GetComponent<Character>() : null;
            GameObject victimPrefab = ZNetScene.instance.GetPrefab(victim.GetPrefab());
            Character template = victimPrefab ? victimPrefab.GetComponent<Character>() : null;
            GameObject killerPrefab = ZNetScene.instance.GetPrefab(killer.GetPrefab());
            Player playerTemplate = killerPrefab ? killerPrefab.GetComponent<Player>() : null;
            if (!template || template.IsPlayer() || !playerTemplate) return;
            Character validatedTarget = target ? target : template;
            if (validatedTarget.IsTamed() || !BaseAI.IsEnemy(playerTemplate, validatedTarget)
                || multiplier != validatedTarget.m_enemyAdrenalineMultiplier) return;
            // m_lastHit is written only on the simulating owner. Use it on this peer whenever it is present.
            // With no local hit, a report can credit only that owner's own character.
            bool sawLastHit = false;
            if (target && target.m_nview)
            {
                if (target.GetHealth() > 0f) return;
                Character observedKiller = target.m_lastHit?.GetAttacker();
                if (target.m_nview.IsOwner() || (observedKiller && observedKiller.m_nview))
                {
                    if (!observedKiller || !observedKiller.m_nview
                        || observedKiller.m_nview.GetZDO().m_uid != killerId) return;
                    sawLastHit = true;
                }
            }
            if (!sawLastHit && killer.GetOwner() != owner) return;
            long recipient = 0;
            if (Player.m_localPlayer && Player.m_localPlayer.m_nview
                && Player.m_localPlayer.m_nview.GetZDO().m_uid == killerId) recipient = ZNet.GetUID();
            else foreach (var peer in session.GetPeers())
                if (peer.m_characterID == killerId && peers.TryGetValue(peer, out var state) && state.Supported) { recipient = peer.m_uid; break; }
            if (recipient == 0 || killer.GetOwner() != recipient) return;
            string trinket = NativeTrinket(killer.GetInt(ZDOVars.s_trinketItem));
            if (trinket == null || TrinketSettings.EffectiveTable == null
                || !TrinketSettings.EffectiveTable.TryGet(trinket, out TrinketRow row) || row.KillFraction <= 0f
                || !serverCredits.TryClaim(victimId.ToString(), recipient)) return;
            var award = new ZPackage();
            award.Write(victimId.ToString()); award.Write(killerId); award.Write(trinket); award.Write(multiplier);
            if (recipient == ZNet.GetUID()) { award.SetPos(0); ReceiveAward(award); }
            else session.GetPeer(recipient)?.m_rpc.Invoke(Award, award);
        }
        catch (Exception e) { Plugin.Log.LogDebug("Rejected trinket death report: " + e.Message); }
    }
    private static void OnAward(ZRpc rpc, ZPackage pkg)
    {
        if (session && !session.IsServer() && ReferenceEquals(session.GetServerPeer()?.m_rpc, rpc)) ReceiveAward(pkg);
    }
    private static void ReceiveAward(ZPackage pkg)
    {
        if (!TuningSupported || pkg.Size() > 256 || pending.Count >= 256) return;
        try
        {
            string id = pkg.ReadString(); ZDOID recipient = pkg.ReadZDOID(); string trinket = pkg.ReadString(); float multiplier = pkg.ReadSingle();
            Player local = Player.m_localPlayer;
            if (id.Length > 80 || !local || !local.m_nview || local.m_nview.GetZDO().m_uid != recipient
                || !(multiplier > 0f) || float.IsInfinity(multiplier) || !receivedCredits.TryClaim(id, ZNet.GetUID())) return;
            if (!TrinketRuntime.TryGetEquippedNative(local, out string equipped) || equipped != trinket) return;
            pending.Enqueue(new Pending { Prefab = trinket, Multiplier = multiplier });
        }
        catch (Exception e) { Plugin.Log.LogDebug("Rejected trinket award: " + e.Message); }
    }

    private static string NativeTrinket(int hash)
    {
        if (hash == 0) return null;
        foreach (string id in TrinketTable.NativeIds) if (id.GetStableHashCode() == hash) return id;
        return null;
    }

    private static bool EdgeBoundaryAvailable() => !BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("com.meadworks.edge") || edgeMethod != null;
    private static void EnsureEdgeBoundary()
    {
        if (edgeMethod != null || !BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue("com.meadworks.edge", out var edge)) return;
        Type type = edge.Instance?.GetType().Assembly.GetType("ExtraPotions.Edge.Runtime.Secondaries.Native.SecondaryAttackAdrenalineSystem");
        var method = type == null ? null : AccessTools.Method(type, "TryModify", new[] { typeof(Character), typeof(float).MakeByRefType() });
        if (method == null) return;
        edgeHarmony = new Harmony(Plugin.PluginGuid + ".trinket-kill-boundary");
        edgeHarmony.Patch(method, prefix: new HarmonyMethod(typeof(TrinketKillCredit), nameof(EdgePrefix)) { priority = Priority.First });
        edgeMethod = method;
    }
    private static bool EdgePrefix(ref bool __result)
    {
        if (!granting) return true;
        __result = true;
        return false;
    }
    private static void Unregister(ZNetPeer peer)
    {
        foreach (string name in new[] { Hello, Ack, Gate, Death, Award }) peer.m_rpc.Unregister(name);
    }
    private static void ClearSession()
    {
        foreach (var peer in peers.Keys) Unregister(peer);
        peers.Clear(); departed.Clear(); serverCredits.Clear(); receivedCredits.Clear(); pending.Clear();
        serverGate = previousGate = TuningSupported = false;
    }
    internal static void Dispose()
    {
        ClearSession(); session = null;
        edgeHarmony?.UnpatchSelf(); edgeHarmony = null; edgeMethod = null; granting = false;
    }
}

[HarmonyPatch(typeof(Character), nameof(Character.OnDeath))]
internal static class TrinketDeathPatch
{
    private static void Prefix(Character __instance)
    {
        try { TrinketKillCredit.Report(__instance); }
        catch (Exception e) { Plugin.Log.LogWarning("Mantle skipped an invalid trinket death credit: " + e.Message); }
    }
}
