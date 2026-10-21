// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original F3 integration, design 3.4. Reuses existing LuckPolicy/KillerAttribution/LuckDelta,
// native player-view RPC leases and the ownership-backed pending-death validation pattern already adopted from CM.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using ExtraPotions.Wildblood.Adopted.Runtime;
using ExtraPotions.Wildblood.Config;
using ExtraPotions.Wildblood.Luck;
using ExtraPotions.Wildblood.Runtime;
using HarmonyLib;
using UnityEngine;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Wildblood.AdoptedLuck;

internal static class LuckAttributionRuntime
{
    internal const string Rpc = "wildblood.luck";
    internal const string WorldKey = "wildblood.luck.world";
    internal const string PityProtocolKey = "wildblood.luck.weighted.protocol";
    private const int MaximumRows = 128, MaximumClaims = 256, MaximumPacketBytes = 8192;
    private static readonly HashSet<ZDOID> pending = new();
    private static readonly Dictionary<ZDOID, float> completed = new();
    private static long generation = -1;

    internal sealed class Credit
    {
        internal Player Player = null!;
        internal ZNetView View = null!;
        internal ZDOID Dead, Attacker;
        internal Vector3 Position;
        internal long World, Generation, Owner;
        internal LuckTable Table = null!;
        internal bool WeightedSupported;
        internal LuckDeathAdmission.DeathScope? Decision;
    }

    private static bool Session(out long world, out long currentGeneration)
    {
        world = 0; currentGeneration = CruciblePlugin.Session.Generation;
        if (ZNet.instance == null || ZNet.m_world == null || !CruciblePlugin.Session.AuthorityReady ||
            StorageManager.CurrentMode != LuckMode.Attributed) return false;
        world = ZNet.instance.GetWorldUID();
        return world != 0 && CruciblePlugin.Session.WorldId == world;
    }

    // Explicitly invoked by the activation owner after synchronization; not a new component or polling service.
    internal static void Tick()
    {
        if (!Session(out long world, out long currentGeneration)) return;
        if (generation != currentGeneration)
        {
            Reset();
            generation = currentGeneration;
        }
        if (Player.m_localPlayer != null) StorageManager.EnsureLoaded(Player.m_localPlayer, world);
        // A player whose view this owner still holds is skipped; Register would find the same lease and return.
        foreach (Player player in Player.GetAllPlayers())
            if (player == null || player.m_nview == null || !player.m_nview.IsValid() || !NativeRpcOwnership.OwnsView(player.m_nview, NativeRpcOwner.LuckPlayer)) Register(player!);
        Prune();
    }

    internal static void Register(Player player)
    {
        if (player == null || !LuckDeathAdmission.Session(out long world, out long currentGeneration)) return;
        ZNetView? view = player.m_nview;
        if (view == null || !view.IsValid()) return;
        NativeRpcOwnership.View(view, NativeRpcOwner.LuckPlayer,
            () => {
                view.Register<ZPackage>(Rpc, (sender, package) => Receive(player, view, world, currentGeneration, sender, package));
                view.Register<ZPackage>(LuckDeathAdmission.Rpc, (sender, package) => {
                    if (package != null && package.Size() <= LuckDecisionCodec.MaximumPacketBytes)
                        LuckDeathAdmission.Receive(player, view, sender, package.GetArray());
                });
            }, Rpc, LuckDeathAdmission.Rpc);
    }

    internal static void Reset()
    {
        if (!LuckDeathAdmission.HasUnsettled) NativeRpcOwnership.Release(NativeRpcOwner.LuckPlayer);
        pending.Clear(); completed.Clear(); generation = -1;
    }

    internal static Dictionary<int, int> NativeCounters(LuckTable table)
    {
        return NativeLuckCounters.TryConvert(table, p => p.GetStableHashCode(), out var result)
            ? result : new Dictionary<int, int>();
    }

    internal static Player? Resolve(Character? attacker)
    {
        if (attacker == null) return null;
        if (attacker is Player direct) return direct;
        if (!attacker.IsTamed()) return null;
        var nearby = new List<NearbyPlayer>();
        foreach (Player player in Player.GetAllPlayers())
            if (player != null && !player.IsDead())
                nearby.Add(new NearbyPlayer(player.GetPlayerID(), Vector3.Distance(player.transform.position, attacker.transform.position)));
        GameObject? following = attacker.GetComponent<MonsterAI>()?.GetFollowTarget();
        Player? followed = following != null ? following.GetComponent<Player>() : null;
        float range = Plugin.Settings?.Get(WildbloodSettings.Luck.TameCreditRange) ?? 30f;
        long? id = KillerAttribution.Resolve(KillFacts.ByTame(followed?.GetPlayerID()), nearby, range);
        if (!id.HasValue) return null;
        foreach (Player player in Player.GetAllPlayers())
            if (player != null && player.GetPlayerID() == id.Value) return player;
        return null;
    }

    internal static bool TryCredit(CharacterDrop drop, out Credit? credit)
    {
        credit = null;
        if (!Session(out long world, out long currentGeneration)) return false;
        Character? character = drop.m_character;
        ZNetView? dyingView = character?.m_nview;
        if (character == null || dyingView == null || !dyingView.IsValid() || !dyingView.IsOwner() ||
            !Finite(character.transform.position)) return false;
        Character? attacker = character.m_lastHit?.GetAttacker(); // actual pinned SYLB last-hit boundary
        Player? player = Resolve(attacker);
        if (attacker == null || player == null) return false;
        Register(player);
        ZNetView? view = player.m_nview;
        if (view == null || !NativeRpcOwnership.OwnsView(view, NativeRpcOwner.LuckPlayer)) return false;
        ZDO? published = view.GetZDO();
        if (published == null || published.GetLong(WorldKey, 0) != world) return false;
        LuckTable table;
        if (player == Player.m_localPlayer)
        {
            if (!StorageManager.TryGetTable(player, world, out table)) return false;
        }
        else
        {
            string text = published.GetString(LuckCodec.Prefix, "");
            if (Encoding.UTF8.GetByteCount(text) > LuckCodec.PublishBytes || !StorageManager.CanPreserveFormat(text)) return false;
            table = LuckCodec.Parse(text);
        }
        if (!NativeLuckCounters.TryConvert(table, p => p.GetStableHashCode(), out _)) return false;
        var names = new Dictionary<int, string>();
        foreach (var row in table.Entries)
            names[NativeLuckCounters.Bucket(row.Prefab.GetStableHashCode(), row.Chance)] = row.Prefab;
        foreach (var row in drop.m_drops)
        {
            if (row == null || row.m_prefab == null) continue;
            float chance = CharacterDropPatch.EffectiveChance(drop, row);
            if (!(chance > 0f && chance <= 0.3f)) continue;
            int key = NativeLuckCounters.Bucket(row.m_prefab.name.GetStableHashCode(), chance);
            if (names.TryGetValue(key, out string prefab) && prefab != row.m_prefab.name) return false;
            names[key] = row.m_prefab.name;
        }
        ZDOID dead = character.GetZDOID(), source = attacker.GetZDOID();
        if (dead.IsNone() || source.IsNone() || published.GetOwner() == 0) return false;
        credit = new Credit { Player = player, View = view, Dead = dead, Attacker = source,
            Position = character.transform.position, World = world, Generation = currentGeneration,
            Owner = dyingView.GetZDO().GetOwner(), Table = table,
            WeightedSupported = published.GetInt(PityProtocolKey, 0) == 1 };
        return true;
    }

    internal static void Send(Credit credit, List<LuckDelta> deltas)
    {
        if (StorageManager.CurrentMode == LuckMode.Attributed) return;
        if (!Session(out long world, out long currentGeneration) || world != credit.World || currentGeneration != credit.Generation ||
            Plugin.Settings?.Get(WildbloodSettings.Cheats.LuckyKills) == true ||
            !NativeRpcOwnership.OwnsView(credit.View, NativeRpcOwner.LuckPlayer) || deltas.Count > MaximumRows) return;
        if (deltas.Exists(delta => delta.Weighted) && (!credit.WeightedSupported || credit.View.GetZDO()?.GetInt(PityProtocolKey, 0) != 1)) return;
        var package = new ZPackage();
        package.Write(world); package.Write(credit.Dead); package.Write(credit.Attacker); package.Write(credit.Position);
        package.Write(deltas.Count);
        foreach (LuckDelta delta in deltas) package.Write(delta.ToWire());
        if (package.Size() > MaximumPacketBytes || credit.Owner != ZNet.GetUID()) return;
        ZDO? source = ZDOMan.instance != null ? ZDOMan.instance.GetZDO(credit.Dead) : null;
        if (source == null || source.GetOwner() != credit.Owner) return;
        package.SetPos(0);
        if (credit.Player == Player.m_localPlayer && credit.View.IsOwner())
            Receive(credit.Player, credit.View, world, currentGeneration, credit.Owner, package);
        else credit.View.InvokeRPC(credit.View.GetZDO().GetOwner(), Rpc, package);
    }

    internal static void SendDecision(Player player, long target, byte[] bytes)
    {
        if (player == null || player.m_nview == null || !player.m_nview.IsValid() || bytes == null
            || bytes.Length > LuckDecisionCodec.MaximumPacketBytes || !NativeRpcOwnership.OwnsView(player.m_nview, NativeRpcOwner.LuckPlayer)) return;
        if (target == ZNet.GetUID()) LuckDeathAdmission.Receive(player, player.m_nview, ZNet.GetUID(), bytes);
        else player.m_nview.InvokeRPC(target, LuckDeathAdmission.Rpc, new ZPackage(bytes));
    }

    private static bool OwnedLocal(Player player, ZNetView view, long world, long capturedGeneration)
    {
        return player != null && player == Player.m_localPlayer && ReferenceEquals(player.m_nview, view) && view.IsValid() && view.IsOwner() &&
            NativeRpcOwnership.OwnsView(view, NativeRpcOwner.LuckPlayer) &&
            Session(out long currentWorld, out long currentGeneration) && world == currentWorld && capturedGeneration == currentGeneration &&
            Plugin.Settings?.Get(WildbloodSettings.Cheats.LuckyKills) != true;
    }

    private static void Receive(Player player, ZNetView view, long world, long capturedGeneration, long sender, ZPackage package)
    {
        if (StorageManager.CurrentMode == LuckMode.Attributed || LuckDeathAdmission.Holds(player, world)) return;
        if (!OwnedLocal(player, view, world, capturedGeneration) || package == null || package.Size() > MaximumPacketBytes ||
            ZDOMan.instance == null || ZNetScene.instance == null || ZNet.instance == null) return;
        ZDOID deadId = ZDOID.None;
        bool reserved = false;
        try
        {
            if (package.ReadLong() != world) return;
            deadId = package.ReadZDOID();
            ZDOID attackerId = package.ReadZDOID();
            Vector3 position = package.ReadVector3();
            int count = package.ReadInt();
            Prune();
            if (deadId.IsNone() || attackerId.IsNone() || count < 1 || count > MaximumRows || !Finite(position) ||
                completed.ContainsKey(deadId) || pending.Contains(deadId) || pending.Count >= MaximumClaims ||
                completed.Count + pending.Count >= MaximumClaims) return;
            // Routed RPC sender is authenticated by the game's transport. On clients, remote clients are not ZNet peers;
            // validate the replicated dying ZDO owner instead of requiring a nonexistent direct client-to-client peer.
            ZDO? dead = ZDOMan.instance.GetZDO(deadId);
            if (sender == 0 || dead == null || dead.GetOwner() != sender || !Finite(dead.GetPosition()) ||
                (dead.GetPosition() - position).sqrMagnitude > 16f) return;
            if (ZNet.instance.IsServer() && sender != ZNet.GetUID())
            {
                ZNetPeer? peer = ZNet.instance.GetPeer(sender);
                if (peer == null || !peer.IsReady()) return;
            }
            GameObject? sourceObject = ZNetScene.instance.FindInstance(attackerId);
            Character? attacker = sourceObject != null ? sourceObject.GetComponent<Character>() : null;
            if (Resolve(attacker) != player) return;
            GameObject? prefab = ZNetScene.instance.GetPrefab(dead.GetPrefab());
            CharacterDrop? sourceDrop = prefab != null ? prefab.GetComponent<CharacterDrop>() : null;
            Character? sourceCharacter = prefab != null ? prefab.GetComponent<Character>() : null;
            if (sourceDrop == null || sourceCharacter == null || sourceCharacter.IsPlayer()) return;
            var deltas = new List<LuckDelta>(count);
            var pityCatalog = NativeWeightedPity.Catalog();
            var seen = new HashSet<int>();
            for (int i = 0; i < count; i++)
            {
                string wire = package.ReadString();
                if (wire.Length > 256 || !LuckDelta.TryParse(wire, out LuckDelta delta) ||
                    (delta.Weighted && view.GetZDO()?.GetInt(PityProtocolKey, 0) != 1) ||
                    !seen.Add(NativeLuckCounters.Bucket(delta.Prefab.GetStableHashCode(), delta.Chance)) ||
                    !ValidDeltaWithCatalog(sourceDrop, dead, delta, pityCatalog)) return;
                deltas.Add(delta);
            }
            if (package.GetPos() != package.Size()) return;
            pending.Add(deadId); reserved = true;
            ZDOMan authority = ZDOMan.instance;
            ZNet.instance.StartCoroutine(AfterDeathSync(player, view, world, capturedGeneration, sender, deadId, position, authority, deltas, pityCatalog));
            reserved = false;
        }
        catch (Exception error) { Plugin.NativeLogger?.LogDebug("Luck claim refused: " + error.GetType().Name); }
        finally { if (reserved) pending.Remove(deadId); }
    }

    private static bool ValidDelta(CharacterDrop source, ZDO dead, LuckDelta delta)
        => ValidDeltaWithCatalog(source, dead, delta, NativeWeightedPity.Catalog());

    internal static bool ValidDeltaWithCatalog(CharacterDrop source, ZDO dead, LuckDelta delta, IReadOnlyList<PitySource>? pityCatalog, int? capturedLevel = null)
    {
        if (!(delta.Chance > 0f && delta.Chance <= 0.3f) || delta.NewCounter < -1 || delta.Consumed < 0) return false;
        if (delta.Weighted)
        {
            if (pityCatalog == null) return false;
            foreach (var row in source.m_drops)
                if (row != null && row.m_prefab != null && row.m_prefab.name == delta.Prefab && row.m_chance == delta.Chance &&
                    NativeWeightedPity.SafeSource(source, row, pityCatalog) &&
                    WeightedPity.TryWeight((capturedLevel ?? dead.GetInt(ZDOVars.s_level, 1)), row.m_levelMultiplier, out int weight))
                    return delta.NewCounter == -1 ? delta.Consumed == weight
                        : delta.Consumed == 0 && delta.NewCounter <= NativeLuckCounters.MaximumReset(delta.Chance);
            return false;
        }
        int rows = 0;
        var chances = new List<float>();
        foreach (var row in source.m_drops)
            if (row != null && row.m_prefab != null && row.m_prefab.name == delta.Prefab)
            {
                float scaled = row.m_levelMultiplier ? row.m_chance * Mathf.Max(1, (int)Mathf.Pow(2f, (capturedLevel ?? dead.GetInt(ZDOVars.s_level, 1)) - 1)) : row.m_chance;
                chances.Add(row.m_chance);
                chances.Add(scaled);
                if (NativeLuckCounters.Bucket(0, row.m_chance) == NativeLuckCounters.Bucket(0, delta.Chance) ||
                    NativeLuckCounters.Bucket(0, scaled) == NativeLuckCounters.Bucket(0, delta.Chance)) rows++;
            }
        if (rows == 0 || !NativeLuckCounters.TryMaximumReset(delta.Chance, chances, out double maximum)) return false;
        if (delta.NewCounter == -1) return delta.Consumed > 0 && delta.Consumed <= rows;
        return delta.Consumed == 0 && delta.NewCounter <= maximum;
    }

    // Same bounded CM pattern: claim accepted against a live owned ZDO; only disappearance after that validation
    // may confirm destruction. An already missing source on initial receipt is deliberately unverifiable/refused.
    private static IEnumerator AfterDeathSync(Player player, ZNetView view, long world, long capturedGeneration,
        long sender, ZDOID deadId, Vector3 position, ZDOMan authority, List<LuckDelta> deltas, IReadOnlyList<PitySource>? pityCatalog)
    {
        float deadline = Time.realtimeSinceStartup + 2f;
        try
        {
            while (Time.realtimeSinceStartup <= deadline)
            {
                if (LuckDeathAdmission.Holds(player, world)) yield break;
                if (!OwnedLocal(player, view, world, capturedGeneration) || !ReferenceEquals(ZDOMan.instance, authority)) yield break;
                ZDO? dead = authority.GetZDO(deadId);
                if (dead != null && (dead.GetOwner() != sender || !Finite(dead.GetPosition()) ||
                    (dead.GetPosition() - position).sqrMagnitude > 16f)) yield break;
                GameObject? instance = ZNetScene.instance != null ? ZNetScene.instance.FindInstance(deadId) : null;
                Character? character = instance != null ? instance.GetComponent<Character>() : null;
                bool died = dead == null || dead.GetBool(ZDOVars.s_dead, false) || dead.GetFloat(ZDOVars.s_health, float.PositiveInfinity) <= 0f ||
                    (character != null && (character.IsDead() || character.GetHealth() <= 0f));
                if (died)
                {
                    if (!StorageManager.TryGetTable(player, world, out LuckTable table)) yield break;
                    var prepared = LuckCodec.Parse(LuckCodec.Write(table));
                    foreach (LuckDelta delta in deltas)
                    {
                        if (delta.Weighted)
                        {
                            if (pityCatalog == null || !NativeWeightedPity.Apply(prepared, pityCatalog, delta)) yield break;
                        }
                        else prepared.Apply(delta);
                    }
                    foreach (var row in prepared.Entries) table.Set(row.Prefab, row.Chance, row.Counter);
                    completed[deadId] = Time.realtimeSinceStartup + 300f;
                    StorageManager.UpdateMemoryCache(player, world);
                    yield break;
                }
                yield return null;
            }
        }
        finally { pending.Remove(deadId); }
    }

    private static bool Finite(Vector3 value) => !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
        !float.IsNaN(value.y) && !float.IsInfinity(value.y) && !float.IsNaN(value.z) && !float.IsInfinity(value.z);

    private static readonly List<ZDOID> Expired = new();
    private static void Prune()
    {
        var expired = Expired;
        expired.Clear();
        foreach (var row in completed) if (row.Value <= Time.realtimeSinceStartup) expired.Add(row.Key);
        foreach (ZDOID id in expired) completed.Remove(id);
        // At the bound refuse new claims until expiry; never evict a replay guard to accept another packet.
    }
}

// Staged alongside the four pinned persistence hooks. Tick handles late network readiness after Awake.
[HarmonyPatch(typeof(Player), nameof(Player.Awake))]
internal static class PlayerLuckRpcPatch
{
    private static void Postfix(Player __instance) => LuckAttributionRuntime.Register(__instance);
}
