// SPDX-License-Identifier: GPL-3.0-only
// Portions from BossRules 1.1.8 (sighsorry1029), ae28779955a8979a96a43de373504d3a35f6868f.
// Changed by expDARE, 2026: fixed policy, effective settings, fresh native ownership and session-bound leases.
using System;
using System.Collections.Generic;
using BepInEx.Bootstrap;
using ExtraPotions.Wildblood.Adopted.Patches;
using ExtraPotions.Wildblood.Bosses;
using ExtraPotions.Wildblood.Config;
using ExtraPotions.Wildblood.Integration;
using UnityEngine;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Wildblood.Runtime;

internal static class NativeTamedPressure
{
    const string UntilKey = "wildblood.boss_pressure.until-ms", WorldKey = "wildblood.boss_pressure.world", EpochKey = "wildblood.boss_pressure.epoch";
    static readonly TamedPressureClock<ZDOID> clock = new();
    const int DiscoveryObjectBudget = 128, DiscoveryStepBudget = 128;
    static readonly List<ZDO> sectors = new();
    static readonly List<ZDOID> knownBosses = new();
    static readonly HashSet<ZDOID> knownBossIds = new();
    static ZDOMan? discoveryManager;
    static List<ZDO>[]? discoverySectors;
    static List<ZDO>? discoveryBucket;
    static int discoverySector, discoveryEntry;
    static bool discoveryPortal;
    internal static int DiscoveryObjectsLastTick { get; private set; }
    internal static int DiscoveryStepsLastTick { get; private set; }
    static readonly List<(ZDO Zdo, float Distance, int Order)> candidates = new();
    static bool running;
    static long revision;
    static bool Active => Plugin.NativeRulesReady && ExtraPotions.Wildblood.AdoptedCm.NativeModifierRules.Ready && WildbloodModules.Active(WildbloodModule.Bosses)
        && Plugin.Settings != null && Plugin.Settings.Get(WildbloodSettings.Bosses.TamedPressure)
        && !Chainloader.PluginInfos.ContainsKey("sighsorry.BossRules")
        && CruciblePlugin.Session.AuthorityReady && CruciblePlugin.Session.WorldId != 0
        && !string.IsNullOrEmpty(CruciblePlugin.Session.SessionId)
        && ZNet.instance != null && ZNet.m_world != null && ZNet.instance.GetWorldUID() == CruciblePlugin.Session.WorldId
        && ZNetScene.instance != null && ZDOMan.instance != null;
    static bool Authority => Active && ZNet.instance.IsServer() && CruciblePlugin.Session.IsAuthority;
    static double Now => ZNet.instance.GetTimeSeconds();

    internal static void Reset()
    {
        revision++; clock.Reset();
        if (!running) ClearDiscovery();
    }
    static bool Current(long stamp, long expectedWorld, string expectedEpoch) => revision == stamp && Authority
        && CruciblePlugin.Session.WorldId == expectedWorld && string.Equals(CruciblePlugin.Session.SessionId, expectedEpoch, StringComparison.Ordinal);

    internal static void Tick()
    {
        if (!Authority) { Reset(); return; }
        if (running) return;
        long stamp = revision, expectedWorld = CruciblePlugin.Session.WorldId;
        string expectedEpoch = CruciblePlugin.Session.SessionId;
        running = true;
        try
        {
            double now = Now;
            Discover(stamp, expectedWorld, expectedEpoch);
            if (!Current(stamp, expectedWorld, expectedEpoch)) { Reset(); return; }
            if (clock.ScanDue(now)) Scan(now, stamp, expectedWorld, expectedEpoch);
            if (!Current(stamp, expectedWorld, expectedEpoch)) { Reset(); return; }
            if (!clock.DamageDue(now)) return;
            foreach (var id in clock.Current(now))
            {
                if (!Current(stamp, expectedWorld, expectedEpoch)) { Reset(); return; }
                var zdo = ZDOMan.instance.GetZDO(id);
                if (zdo == null || zdo.m_uid != id || !Eligible(zdo) || !Lease(zdo, now)) { clock.Remove(id); continue; }
                float maximum = zdo.GetFloat(ZDOVars.s_maxHealth, BaseHealth(zdo) * Math.Max(1, zdo.GetInt(ZDOVars.s_level, 1)));
                float damage = maximum * TamedPressure.DamageFraction;
                var game = Game.instance;
                var position = zdo.GetPosition();
                long owner = zdo.GetOwner();
                if (!Finite(damage) || damage <= 0f || owner == 0 || ZRoutedRpc.instance == null || game == null
                    || !Finite(position.x) || !Finite(position.y) || !Finite(position.z))
                { clock.ClearDamage(id); continue; }
                int difficulty = game.GetPlayerDifficulty(position);
                if (!Current(stamp, expectedWorld, expectedEpoch)) { Reset(); return; }
                if (!ReferenceEquals(game, Game.instance) || !zdo.IsValid() || zdo.m_uid != id
                    || zdo.GetOwner() != owner || !Eligible(zdo) || !Lease(zdo, now))
                { clock.ClearDamage(id); continue; }
                if (!clock.TryDamage(id, now, owner, damage, game.m_difficultyScaleMaxPlayers, difficulty,
                    game.m_healthScalePerPlayer, Game.m_playerDamageRate, out float packet)) continue;
                var hit = new HitData { m_hitType = HitData.HitType.Undefined, m_point = position };
                hit.m_damage.m_damage = packet;
                // The native damage RPC decides on the target's current owner; no ownership transfer or new RPC is introduced.
                ZRoutedRpc.instance.InvokeRoutedRPC(owner, id, "RPC_Damage", hit);
            }
        }
        finally
        {
            sectors.Clear(); candidates.Clear(); running = false;
            if (revision != stamp) ClearDiscovery();
        }
    }

    static void ClearDiscovery()
    {
        knownBosses.Clear(); knownBossIds.Clear(); sectors.Clear(); candidates.Clear();
        discoveryManager = null; discoverySectors = null; discoveryBucket = null;
        discoverySector = discoveryEntry = 0; discoveryPortal = false;
        DiscoveryObjectsLastTick = DiscoveryStepsLastTick = 0;
    }

    static bool Boss(ZDO zdo)
    {
        if (!zdo.IsValid() || ZDOMan.instance.GetZDO(zdo.m_uid) != zdo || !Alive(zdo)) return false;
        var prefab = Prefab(zdo); var character = prefab != null ? prefab.GetComponent<Character>() : null;
        return character != null && !character.IsPlayer() && (character.IsBoss() || Utils.GetPrefabName(prefab!) == "Eikthyr");
    }

    static void Discover(long stamp, long expectedWorld, string expectedEpoch)
    {
        var manager = ZDOMan.instance;
        var buckets = manager.m_objectsBySector;
        if (!ReferenceEquals(manager, discoveryManager) || !ReferenceEquals(buckets, discoverySectors))
        {
            ClearDiscovery();
            discoveryManager = manager; discoverySectors = buckets;
        }
        DiscoveryObjectsLastTick = DiscoveryStepsLastTick = 0;
        if (buckets == null || buckets.Length == 0) return;
        var portals = manager.GetPortals();
        // Index both native bucket stores directly; empty sectors and dense buckets have separate budgets.
        while (DiscoveryObjectsLastTick < DiscoveryObjectBudget && DiscoveryStepsLastTick < DiscoveryStepBudget)
        {
            if (!Current(stamp, expectedWorld, expectedEpoch)) return;
            if (discoverySector >= buckets.Length)
            {
                discoverySector = discoveryEntry = 0; discoveryPortal = false; discoveryBucket = null;
                break;
            }
            List<ZDO>? bucket;
            if (discoveryPortal) portals.TryGetValue(new ZoneSystem.SectorIndex((uint)discoverySector), out bucket);
            else bucket = buckets[discoverySector];
            // A replaced list starts at its first entry; in-place removals may wait until the next full pass.
            if (!ReferenceEquals(bucket, discoveryBucket)) { discoveryBucket = bucket; discoveryEntry = 0; }
            if (bucket != null && discoveryEntry < bucket.Count)
            {
                var zdo = bucket[discoveryEntry++];
                DiscoveryObjectsLastTick++;
                if (zdo != null && Boss(zdo) && Current(stamp, expectedWorld, expectedEpoch) && knownBossIds.Add(zdo.m_uid))
                    knownBosses.Add(zdo.m_uid);
                continue;
            }
            DiscoveryStepsLastTick++;
            discoveryEntry = 0; discoveryBucket = null;
            if (discoveryPortal) { discoverySector++; discoveryPortal = false; }
            else discoveryPortal = true;
        }
    }

    static void Scan(double now, long stamp, long expectedWorld, string expectedEpoch)
    {
        if (ZoneSystem.instance == null) return;
        for (int i = knownBosses.Count - 1; i >= 0; i--)
        {
            if (!Current(stamp, expectedWorld, expectedEpoch)) return;
            var id = knownBosses[i];
            var boss = ZDOMan.instance.GetZDO(id);
            if (boss == null || boss.m_uid != id || !Boss(boss))
            {
                knownBosses.RemoveAt(i); knownBossIds.Remove(id); continue;
            }
                var position = boss.GetPosition();
                if (!Finite(position.x) || !Finite(position.y) || !Finite(position.z)) continue;
                sectors.Clear(); candidates.Clear();
                int zoneRange = Mathf.CeilToInt(TamedPressure.Range / ZoneSystem.c_ZoneSize) + 1;
                ZDOMan.instance.FindSectorObjects(ZoneSystem.GetZone(position), new SimulationDistance(zoneRange, 0, classic: true), sectors);
                int order = 0;
                foreach (var target in sectors)
                {
                    if (target == null || target.m_uid == boss.m_uid || !Eligible(target)) continue;
                    var at = target.GetPosition();
                    if (!TamedPressure.InRange(position.x, position.z, at.x, at.z)) continue;
                    float dx = at.x - position.x, dz = at.z - position.z;
                    candidates.Add((target, dx * dx + dz * dz, order++));
                }
                candidates.Sort((a, b) => { int distance = a.Distance.CompareTo(b.Distance); return distance != 0 ? distance : a.Order.CompareTo(b.Order); });
                int applied = 0;
                foreach (var target in candidates)
                {
                    if (!Current(stamp, expectedWorld, expectedEpoch)) return;
                    if (!Eligible(target.Zdo)) continue;
                    double until = clock.Refresh(target.Zdo.m_uid, now);
                    target.Zdo.Set(UntilKey, (long)(until * 1000d));
                    target.Zdo.Set(WorldKey, expectedWorld);
                    target.Zdo.Set(EpochKey, expectedEpoch);
                    if (!Current(stamp, expectedWorld, expectedEpoch)) return;
                    ZDOMan.instance.ForceSendZDO(target.Zdo.m_uid);
                    if (!Current(stamp, expectedWorld, expectedEpoch)) return;
                    if (++applied >= TamedPressure.MaxTargets) break;
                }
        }
    }

    static GameObject? Prefab(ZDO zdo) => ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(zdo.GetPrefab()) : null;
    static float BaseHealth(ZDO zdo) => Math.Max(1f, Prefab(zdo)?.GetComponent<Character>()?.m_health ?? 1f);
    static bool Alive(ZDO zdo)
    {
        if (!zdo.IsValid()) return false;
        var view = ZNetScene.instance.FindInstance(zdo);
        var actor = view != null ? view.GetComponent<Character>() : null;
        if (actor != null && (!view.IsValid() || view.GetZDO() != zdo || actor.IsDead())) return false;
        return zdo.GetFloat(ZDOVars.s_health, zdo.GetFloat(ZDOVars.s_maxHealth, BaseHealth(zdo))) > 0f;
    }
    static bool Eligible(ZDO zdo)
    {
        if (!zdo.IsValid()) return false;
        var prefab = Prefab(zdo); var character = prefab != null ? prefab.GetComponent<Character>() : null;
        return character != null && TamedPressure.Eligible(true, Alive(zdo), character.IsPlayer(),
            zdo.GetBool(ZDOVars.s_tamed), prefab.GetComponent<MonsterAI>() != null);
    }
    static bool Lease(ZDO zdo, double now) => zdo.IsValid() && TamedPressure.ActiveLease(zdo.GetLong(UntilKey) / 1000d, now,
        zdo.GetLong(WorldKey), CruciblePlugin.Session.WorldId, zdo.GetString(EpochKey), CruciblePlugin.Session.SessionId);
    static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    internal static void Apply(Struck struck)
    {
        var hit = struck.Hit;
        if (!Active || hit == null || !hit.HaveAttacker() || !struck.Owned || struck.Victim.IsDead()) return;
        var victim = struck.Victim.m_nview.GetZDO();
        double now = Now;
        bool incoming = victim != null && Eligible(victim) && Lease(victim, now);
        var attacker = struck.Attacker != null && struck.Attacker.m_nview != null && struck.Attacker.m_nview.IsValid()
            ? struck.Attacker.m_nview.GetZDO() : null;
        if (attacker == null && !hit.m_attacker.IsNone()) attacker = ZDOMan.instance.GetZDO(hit.m_attacker);
        bool outgoing = attacker != null && attacker.m_uid == hit.m_attacker && Eligible(attacker) && Lease(attacker, now);
        float multiplier = TamedPressure.Multiplier(true, incoming, outgoing);
        if (multiplier != 1f) hit.ApplyModifier(multiplier);
    }
}
