// SPDX-License-Identifier: GPL-3.0-only
// Portions from BossRules 1.1.8 (sighsorry1029), GPL-3.0.
// ae28779955a8979a96a43de373504d3a35f6868f
// Changed by expDARE, 2026: server admission, exact spawn identity and durable refund decisions.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using BepInEx;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Wildblood.Adopted.Runtime;
using ExtraPotions.Wildblood.Adopted.Traits;
using ExtraPotions.Wildblood.Bosses;
using ExtraPotions.Wildblood.Config;
using ExtraPotions.Wildblood.Integration;
using ExtraPotions.Wildblood.Transfers;
using HarmonyLib;
using UnityEngine;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;
using Aspect = ExtraPotions.Wildblood.Adopted.Traits.Aspect;
using ExtraPotions.Wildblood.Adopted.Aspects;

namespace ExtraPotions.Wildblood.Runtime;

internal static class NativeBossAbandon
{
    internal const string ImageKey = "wildblood.summon.image", RefundKey = "wildblood.summon.refund",
        HoldKey = "wildblood.summon.hold", BossKey = "wildblood.summon.altar",
        MaxHealthKey = "wildblood.summon.max-health", PrimaryKey = "wildblood.summon.primary", PendingIdKey = "wildblood.summon.pending-id",
        DeathSeenKey = "wildblood.summon.death-seen", PrimaryIdentityKey = "wildblood.summon.primary-identity",
        TwinIdentityKey = "wildblood.summon.twin-identity", TetherIdentityKey = "wildblood.summon.tether-identity",
        SummonIdKey = "wildblood.summon.id", PrimaryBornKey = "wildblood.summon.primary-born";
    internal const string PhantomIdentityKey = "wildblood.summon.phantom-parent";
    const string RequestRpc = "Wildblood.Summon.Request", NoticeRpc = "Wildblood.Summon.Notice", DeathRpc = "Wildblood.Summon.Death",
        OfferRpc = "Wildblood.Summon.Offer", PaidRpc = "Wildblood.Summon.Paid", DeclinedRpc = "Wildblood.Summon.Declined",
        PaidKey = "wildblood.summon.paid", PayerKey = "wildblood.summon.payer", InventoryKey = "wildblood.summon.inventory";
    const string SpawnPointKey = "wildblood.summon.spawn-point";
    static readonly Dictionary<string, NativeSummonStore> stores = new(StringComparer.Ordinal);
    sealed class QueuedRequest
    {
        internal readonly long Sender;
        internal readonly ZDOID Altar;
        internal readonly bool Inventory;
        internal readonly float Expires;
        internal QueuedRequest(long sender, ZDOID altar, bool inventory)
        { Sender = sender; Altar = altar; Inventory = inventory; Expires = Time.unscaledTime + 15f; }
    }
    static readonly Dictionary<string, QueuedRequest> requests = new(StringComparer.Ordinal);
    static long world;
    static float next;
    static bool executing;
    static string pendingContext = "";
    internal static bool Authority => Plugin.Ready && ZNet.instance && ZNet.instance.IsServer()
        && ZNet.m_world != null && CruciblePlugin.Session.AuthorityReady && CruciblePlugin.Session.IsAuthority;
    static bool Active => WildbloodModules.Active(WildbloodModule.Bosses);
    internal static long World => ZNet.instance.GetWorldUID();
    internal static double Now => ZNet.instance.GetTimeSeconds();
    static bool IsBoss(OfferingBowl bowl) => bowl && bowl.m_bossPrefab
        && bowl.m_bossPrefab.GetComponent<Character>() is Character character && character.IsBoss();
    internal static ZDO? Altar(OfferingBowl bowl) => bowl.GetComponentInParent<ZNetView>()?.GetZDO();
    internal static string Id(ZDOID id) => NativeSummonIdentity.Id(id);
    internal static string SetIdentity(ZDO zdo) => NativeSummonIdentity.Enlist(zdo);
    internal static ZDOID Parse(string id)
    {
        var parts = id.Split(':');
        if (parts.Length != 2) throw new InvalidDataException("Invalid summon ZDO identity.");
        return new ZDOID(long.Parse(parts[0], CultureInfo.InvariantCulture), uint.Parse(parts[1], CultureInfo.InvariantCulture));
    }
    internal static ZDO? Find(string id) => NativeSummonIdentity.Find(id);
    static void Notice(long peer, string key) => ZRoutedRpc.instance.InvokeRoutedRPC(peer, NoticeRpc, key);
    static void Register()
    {
        var bus = ZRoutedRpc.instance;
        if (bus == null || NativeRpcOwnership.OwnsRouted(bus, NativeRpcOwner.BossAbandon)) return;
        NativeRpcOwnership.Routed(bus, NativeRpcOwner.BossAbandon, () =>
        {
            bus.Register<ZDOID, bool>(RequestRpc, Request);
            bus.Register<string>(NoticeRpc, (sender, key) =>
            {
                if (sender == ZRoutedRpc.instance.GetServerPeerID() && (key == "boss_summon_blocked" || key == "boss_summon_recovery_pending" || key == "boss_summon_offering_cancelled"))
                    WildbloodText.TopLeft(WildbloodText.Text(key));
            });
            bus.Register<ZDOID>(DeathRpc, Death);
            bus.Register<ZDOID, string, string, int>(OfferRpc, (sender, altar, summon, prefab, amount) =>
            {
                if (sender != ZRoutedRpc.instance.GetServerPeerID()) return;
                if (NativeSummonPayment.Pay(altar, summon, prefab, amount)) ZRoutedRpc.instance.InvokeRoutedRPC(sender, PaidRpc, altar, summon);
                else if (NativeSummonPayment.TakeDeclined(summon)) ZRoutedRpc.instance.InvokeRoutedRPC(sender, DeclinedRpc, altar, summon);
            });
            bus.Register<ZDOID, string>(PaidRpc, Paid);
            bus.Register<ZDOID, string>(DeclinedRpc, Declined);
        }, () => { }, RequestRpc, NoticeRpc, DeathRpc, OfferRpc, PaidRpc, DeclinedRpc);
    }
    internal static bool Initiate(OfferingBowl bowl, bool inventory)
    {
        if (!Active || !IsBoss(bowl)) return true;
        Register();
        var zdo = Altar(bowl);
        if (zdo != null && NativeRpcOwnership.OwnsRouted(ZRoutedRpc.instance, NativeRpcOwner.BossAbandon))
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.instance.GetServerPeerID(), RequestRpc, zdo.m_uid, inventory);
        else WildbloodText.TopLeft(WildbloodText.Text("boss_summon_recovery_pending"));
        return false;
    }
    static void Request(long sender, ZDOID id, bool inventory)
    {
        if (!Authority || !Active) return;
        try
        {
            EnsureWorld();
            string key = Id(id);
            if (requests.ContainsKey(key)) { Notice(sender, "boss_summon_blocked"); return; }
            if (requests.Count >= 128) { Notice(sender, "boss_summon_recovery_pending"); return; }
            var request = new QueuedRequest(sender, id, inventory);
            if (!Process(request)) requests.Add(key, request);
        }
        catch (Exception error)
        {
            Plugin.NativeLogger?.LogWarning("Wildblood summon held: " + error.GetType().Name);
            Notice(sender, "boss_summon_recovery_pending");
        }
    }
    static bool Process(QueuedRequest request)
    {
        long sender = request.Sender; ZDOID id = request.Altar; bool inventory = request.Inventory;
        try
        {
            if (!Authority || !Active) return true;
            if (Time.unscaledTime >= request.Expires) { Notice(sender, "boss_summon_recovery_pending"); return true; }
            var altar = ZDOMan.instance.GetZDO(id);
            if (altar == null || !PlayerNear(sender, altar.GetPosition(), 20f)) return true;
            var prefab = ZNetScene.instance.GetPrefab(altar.GetPrefab());
            if (!prefab || !prefab.GetComponentInChildren<OfferingBowl>(true) && !prefab.GetComponent<LocationProxy>()) return true;
            var go = ZNetScene.instance.FindInstance(id) ?? ZNetScene.instance.CreateObject(altar);
            var bowl = go ? go.GetComponentInChildren<OfferingBowl>() : null;
            // LocationProxy may defer its child location; OfferingBowl registers its RPCs in Start.
            if (!bowl || bowl.m_nview == null || !bowl.m_nview.IsValid()) return false;
            if (!IsBoss(bowl) || inventory == bowl.m_useItemStands || Altar(bowl)?.m_uid != id) return true;
            var store = Store(altar);
            if (!store.Ready || store.State?.BlocksSummon == true)
            {
                Notice(sender, store.Ready && store.State?.Stage == SummonStage.Alive ? "boss_summon_blocked" : "boss_summon_recovery_pending");
                return true;
            }
            altar.SetOwner(ZDOMan.GetSessionID());
            // A dedicated server may not have instantiated the egg stands in this area yet.
            if (bowl.m_useItemStands)
                foreach (var zdo in ZDOMan.instance.m_objectsByID.Values.ToArray())
                {
                    var standPrefab = ZNetScene.instance.GetPrefab(zdo.GetPrefab());
                    if (standPrefab && standPrefab.GetComponent<ItemStand>() && standPrefab.name.StartsWith(bowl.m_itemStandPrefix, StringComparison.Ordinal)
                        && Vector3.Distance(zdo.GetPosition(), bowl.transform.position) <= bowl.m_itemstandMaxRange
                        && !ZNetScene.instance.FindInstance(zdo)) ZNetScene.instance.CreateObject(zdo);
                }
            if (bowl.IsBossSpawnQueued()) { Notice(sender, "boss_summon_blocked"); return true; }
            if (bowl.m_useItemStands)
            {
                var stands = bowl.FindItemStands();
                if (stands.Any(stand => stand.m_nview == null || !stand.m_nview.IsValid())) return false;
                if (stands.Count == 0 || stands.Any(stand => !stand.HaveAttachment())) return true;
            }
            if (!bowl.CanSpawnBoss(bowl.GetSpawnPosition(), out var spawnPoint)) return true;
            long actor = Actor(sender); if (actor == 0) return true;
            if (bowl.m_useItemStands)
                foreach (var stand in bowl.FindItemStands()) stand.m_nview.GetZDO().SetOwner(ZDOMan.GetSessionID());
            var context = new ZPackage(); context.Write(1); context.Write(actor); context.Write(inventory); context.Write(spawnPoint);
            pendingContext = Convert.ToBase64String(context.GetArray()); executing = true;
            try { Reserve(bowl); }
            finally { executing = false; pendingContext = ""; }
            if (store.State?.Stage != SummonStage.Pending) throw new InvalidOperationException("Summon reservation missing.");
            altar.Set(PayerKey, actor); altar.Set(InventoryKey, inventory); altar.Set(PaidKey, "");
            altar.Set(SpawnPointKey, spawnPoint);
            if (inventory) Offer(store, sender);
            else
            {
                bowl.RemoveAltarItems();
                if (bowl.FindItemStands().Any(stand => stand.HaveAttachment())) throw new IOException("Summon stands not debited.");
                altar.Set(PaidKey, store.State.SummonId);
                Schedule(store);
            }
            // The boss identity and stand debit share the next world checkpoint. Client debits have saved profile receipts.
            ZNet.instance.Save(false);
            return true;
        }
        catch (Exception error)
        {
            Plugin.NativeLogger?.LogWarning("Wildblood summon held: " + error.GetType().Name);
            Notice(sender, "boss_summon_recovery_pending");
            return true;
        }
    }
    static bool PlayerNear(long sender, Vector3 position, float distance)
    {
        var player = sender == ZDOMan.GetSessionID() ? Player.m_localPlayer?.GetZDOID()
            : ZNet.instance.GetPeer(sender)?.m_characterID;
        var zdo = player.HasValue ? ZDOMan.instance.GetZDO(player.Value) : null;
        // Native initialization removes s_health when a character is at full health.
        return zdo != null && zdo.GetFloat(ZDOVars.s_health, 1) > 0 && Vector3.Distance(zdo.GetPosition(), position) <= distance;
    }
    static long Actor(long sender)
    {
        var id = sender == ZDOMan.GetSessionID() ? Player.m_localPlayer?.GetZDOID() : ZNet.instance.GetPeer(sender)?.m_characterID;
        return id.HasValue ? ZDOMan.instance.GetZDO(id.Value)?.GetLong(ZDOVars.s_playerID, 0) ?? 0 : 0;
    }
    static void Offer(NativeSummonStore store, long peer)
    {
        var state = store.State;
        if (state == null || state.Stage != SummonStage.Pending || state.Offerings.Count != 1) return;
        var cost = state.Offerings[0];
        var altar = Find(state.AltarId);
        if (altar != null) ZRoutedRpc.instance.InvokeRoutedRPC(peer, OfferRpc, altar.m_uid, state.SummonId, cost.Prefab, cost.Amount);
    }
    static void Paid(long sender, ZDOID altarId, string summon)
    {
        if (!Authority || !stores.TryGetValue(Id(altarId), out var store) || store.State?.SummonId != summon) return;
        var altar = ZDOMan.instance.GetZDO(altarId);
        if (altar == null || altar.GetLong(PayerKey, 0) != Actor(sender) || !altar.GetBool(InventoryKey)) return;
        altar.SetOwner(ZDOMan.GetSessionID()); altar.Set(PaidKey, summon);
        Schedule(store);
    }
    // The payer took nothing for this summon, so it ends without a boss and the altar is free again.
    static void Declined(long sender, ZDOID altarId, string summon)
    {
        if (!Authority || !stores.TryGetValue(Id(altarId), out var store) || !store.Ready) return;
        var state = store.State;
        if (state?.Stage != SummonStage.Pending || state.SummonId != summon) return;
        var altar = ZDOMan.instance.GetZDO(altarId);
        if (altar == null || !altar.GetBool(InventoryKey) || altar.GetLong(PayerKey, 0) == 0 || altar.GetLong(PayerKey, 0) != Actor(sender)
            || altar.GetString(PaidKey) == summon || altar.GetString(PrimaryIdentityKey).Length != 0 || altar.GetZDOID(PrimaryKey) != ZDOID.None) return;
        var instance = ZNetScene.instance.FindInstance(altarId);
        var bowl = instance ? instance.GetComponentInChildren<OfferingBowl>() : null;
        if (bowl && bowl.IsBossSpawnQueued()) return;
        try
        {
            altar.SetOwner(ZDOMan.GetSessionID());
            store.Change(state.Cancel());
            if (store.State?.Stage != SummonStage.Cancelled) return;
            altar.Set(PayerKey, 0L); altar.Set(PaidKey, ""); altar.Set(PendingIdKey, "");
            ZNet.instance.Save(false);
            Notice(sender, "boss_summon_offering_cancelled");
        }
        catch (Exception error) { Plugin.NativeLogger?.LogWarning("Wildblood summon cancel held: " + error.GetType().Name + ": " + error.Message); }
    }
    static void Schedule(NativeSummonStore store)
    {
        var state = store.State;
        if (state?.Stage != SummonStage.Pending) return;
        var altar = Find(state.AltarId);
        if (altar == null || altar.GetString(PaidKey) != state.SummonId || altar.GetString(PrimaryIdentityKey).Length != 0
            || altar.GetZDOID(PrimaryKey) != ZDOID.None) return;
        var instance = ZNetScene.instance.FindInstance(altar.m_uid) ?? ZNetScene.instance.CreateObject(altar);
        var bowl = instance ? instance.GetComponentInChildren<OfferingBowl>() : null;
        if (!bowl || bowl.m_nview == null || !bowl.m_nview.IsValid() || bowl.IsBossSpawnQueued()) return;
        altar.SetOwner(ZDOMan.GetSessionID());
        executing = true;
        try { bowl.SpawnBoss(altar.GetVec3(SpawnPointKey, bowl.GetSpawnPosition())); }
        finally { executing = false; }
    }
    internal static void Reserve(OfferingBowl bowl)
    {
        if (!Active || !IsBoss(bowl)) return;
        if (!Authority || !executing) throw new InvalidOperationException("Summon must be admitted by the server.");
        var altar = Altar(bowl) ?? throw new InvalidOperationException("Altar ZDO unavailable.");
        var store = Store(altar);
        if (store.Ready && store.State?.Stage == SummonStage.Pending && altar.GetString(PaidKey) == store.State.SummonId) return;
        if (!store.Ready || store.State?.BlocksSummon == true) throw new InvalidOperationException("Altar has an unsettled summon.");
        var offerings = new List<SummonOffering>();
        if (bowl.m_useItemStands)
            foreach (var stand in bowl.FindItemStands())
            {
                var data = stand.m_nview.GetZDO();
                var item = ZNetScene.instance.GetPrefab(data.GetInt(ZDOVars.s_item));
                if (!stand.HaveAttachment() || item == null || item.GetComponent<ItemDrop>() == null)
                    throw new InvalidDataException("Altar offering unavailable.");
                offerings.Add(new SummonOffering(Utils.GetPrefabName(item), 1));
            }
        else if (bowl.m_bossItem != null) offerings.Add(new SummonOffering(Utils.GetPrefabName(bowl.m_bossItem.gameObject), bowl.m_bossItems));
        var pending = SummonLedgerSnapshot.Start(World, Id(altar.m_uid), Guid.NewGuid().ToString("N"), offerings, pendingContext);
        store.Change(pending);
        altar.Set(PrimaryKey, ZDOID.None);
        altar.Set(PrimaryIdentityKey, "");
        altar.Set(PendingIdKey, pending.SummonId);
    }
    // Capture the exact Instantiate result now; EliteController resolves the aspect and creates its twin in Start.
    internal static void Spawned(GameObject boss, OfferingBowl bowl)
    {
        if (!Authority || !IsBoss(bowl)) return;
        var altar = Altar(bowl); if (altar == null) return;
        var store = Store(altar);
        if (store.State?.Stage != SummonStage.Pending) return;
        var view = boss.GetComponent<ZNetView>();
        if (view == null || !view.IsValid()) throw new InvalidDataException("Summoned boss has no durable identity.");
        string primaryIdentity = SetIdentity(view.GetZDO());
        view.GetZDO().Set(BossKey, store.State.AltarId);
        view.GetZDO().Set(SummonIdKey, store.State.SummonId);
        view.GetZDO().Set(PrimaryBornKey, true);
        altar.Set(PrimaryKey, view.GetZDO().m_uid);
        altar.Set(PrimaryIdentityKey, primaryIdentity);
        altar.Set(PendingIdKey, store.State.SummonId);
    }
    static bool ControllerReady(ZDO boss)
    {
        var instance = ZNetScene.instance.FindInstance(boss);
        var character = instance ? instance.GetComponent<Character>() : null;
        if (character == null) return false;
        var controller = character.GetComponent<EliteController>();
        if (controller == null ? !ProtectedCreatures.IsProtected(character.gameObject) : !controller.Ready) return false;
        boss.Set(MaxHealthKey, character.GetMaxHealth());
        return true;
    }
    static void FinalizePending(NativeSummonStore store)
    {
        var state = store.State;
        if (!store.Ready || state?.Stage != SummonStage.Pending) return;
        var altar = Find(state.AltarId);
        if (altar == null || altar.GetString(PendingIdKey) != state.SummonId) return;
        string primaryId = altar.GetString(PrimaryIdentityKey);
        if (primaryId.Length == 0)
        {
            var candidates = ZDOMan.instance.m_objectsByID.Values.Where(z => z.GetString(BossKey) == state.AltarId
                && z.GetString(SummonIdKey) == state.SummonId && z.GetBool(PrimaryBornKey)).Take(2).ToArray();
            if (candidates.Length > 1) throw new InvalidDataException("Conflicting exact summoned primary identities.");
            if (candidates.Length == 0) return;
            primaryId = SetIdentity(candidates[0]); altar.Set(PrimaryIdentityKey, primaryId); altar.Set(PrimaryKey, candidates[0].m_uid);
        }
        var ids = new List<string> { primaryId };
        var killed = new List<string>();
        for (int index = 0; index < ids.Count; index++)
        {
            var id = ids[index];
            var member = Find(id);
            if (member == null)
            {
                if (!NativeSummonIdentity.Destroyed(id)) return;
                killed.Add(id);
                // A primary can die before the first manager tick. Recover only exact reverse partner links.
                foreach (var candidate in ZDOMan.instance.m_objectsByID.Values)
                    if (candidate.GetString(TwinIdentityKey) == id || candidate.GetString(TetherIdentityKey) == id) Add(SetIdentity(candidate));
                continue;
            }
            bool dead = member.GetBool(DeathSeenKey) || member.GetFloat(ZDOVars.s_health, 1) <= 0;
            if (dead) killed.Add(id);
            else
            {
                // A restored but unloaded exact member is safe to instantiate; never select by proximity.
                if (!ZNetScene.instance.FindInstance(member)) ZNetScene.instance.CreateObject(member);
                if (!ControllerReady(member)) return;
                if (index == 0)
                {
                    var controller = ZNetScene.instance.FindInstance(member)?.GetComponent<EliteController>();
                    if (controller != null && (controller.Traits.HasAspect(Aspect.Twin)
                            && member.GetString(TwinIdentityKey).Length == 0 && AspectStore.GetTwin(member) == ZDOID.None
                        || controller.Traits.HasAspect(Aspect.Tethered)
                            && member.GetString(TetherIdentityKey).Length == 0 && AspectStore.GetTether(member) == ZDOID.None)) return;
                }
            }
            if (!AddLink(member, AspectStore.GetTwin(member), TwinIdentityKey)
                || !AddLink(member, AspectStore.GetTether(member), TetherIdentityKey)) return;
            member.Set(BossKey, state.AltarId);
            member.Set(SummonIdKey, state.SummonId);
        }
        store.Change(state.AttachBosses(ids));
        foreach (var id in killed) store.Change(store.State!.MarkKilled(id), allowUncheckpointed: true);

        void Add(string id)
        {
            if (id.Length == 0 || ids.Contains(id)) return;
            if (ids.Count >= 16) throw new InvalidDataException("Summoned boss partner group exceeds bounds.");
            ids.Add(id);
        }
        bool AddLink(ZDO member, ZDOID link, string key)
        {
            string stable = member.GetString(key);
            if (stable.Length != 0) { Add(stable); return true; }
            if (link == ZDOID.None) return true;
            // Only a link created in this running session can be read as a native ID. Loaded raw IDs may collide.
            if (!NativeSummonIdentity.Fresh(Id(member.m_uid))) return false;
            var partner = ZDOMan.instance.GetZDO(link);
            if (partner == null) return false;
            stable = SetIdentity(partner); member.Set(key, stable); Add(stable); return true;
        }
    }
    static void Death(long sender, ZDOID id)
    {
        if (!Authority) return;
        try
        {
            var boss = ZDOMan.instance.GetZDO(id);
            // The owning peer's OnDeath signal can precede its health replication.
            if (boss == null || boss.GetOwner() != sender) return;
            MarkDead(boss);
        }
        catch (Exception error) { Plugin.NativeLogger?.LogWarning("Wildblood death receipt held: " + error.GetType().Name); }
    }
    internal static void MarkDead(ZDO boss)
    {
        if (!Authority) return;
        var altarId = boss.GetString(BossKey);
        if (altarId.Length == 0) return;
        boss.Set(DeathSeenKey, true);
        EnsureWorld();
        if (!stores.TryGetValue(altarId, out var store))
        {
            var altar = Find(altarId); if (altar == null) return;
            store = Store(altar);
        }
        if (store.State?.Stage == SummonStage.Pending) FinalizePending(store);
        var state = store.State;
        if (state?.Stage != SummonStage.Alive || !state.BossIds.Contains(Id(boss.m_uid))) return;
        var changed = state.MarkKilled(Id(boss.m_uid));
        if (!ReferenceEquals(changed, store.State)) store.Change(changed, allowUncheckpointed: true);
    }
    static bool Near(ZDO boss, float range)
    {
        if (PlayerNear(ZDOMan.GetSessionID(), boss.GetPosition(), range)) return true;
        foreach (var peer in ZNet.instance.GetConnectedPeers()) if (PlayerNear(peer.m_uid, boss.GetPosition(), range)) return true;
        return false;
    }
    static bool FullHealth(ZDO boss)
    {
        var live = ZNetScene.instance.FindInstance(boss)?.GetComponent<Character>();
        if (live != null) return !live.IsDead() && live.GetHealth() >= live.GetMaxHealth();
        float max = boss.GetFloat(MaxHealthKey, 0);
        return max > 0 && boss.GetFloat(ZDOVars.s_health, max) >= max;
    }
    internal static void Tick()
    {
        Register();
        if (!Authority) return;
        EnsureWorld();
        if (Time.unscaledTime < next) return; next = Time.unscaledTime + 1;
        foreach (var request in requests.ToArray())
            if (Process(request.Value)) requests.Remove(request.Key);
        foreach (var store in stores.Values.ToArray())
        {
            try
            {
                store.Recover();
                RestoreContext(store);
                FinalizePending(store);
                Schedule(store);
                var state = store.State;
                if (!store.Ready || state == null) continue;
                if (state.Stage == SummonStage.RefundDecided) { store.Change(state.DeliverRefund()); continue; }
                var altar = Find(state.AltarId);
                bool paid = altar != null && altar.GetString(PaidKey) == state.SummonId;
                if (!paid && state.Stage == SummonStage.Pending && altar != null && altar.GetBool(InventoryKey))
                {
                    long actor = altar.GetLong(PayerKey, 0);
                    if (actor != 0)
                    {
                        if (Actor(ZDOMan.GetSessionID()) == actor) Offer(store, ZDOMan.GetSessionID());
                        foreach (var peer in ZNet.instance.GetConnectedPeers()) if (Actor(peer.m_uid) == actor) Offer(store, peer.m_uid);
                    }
                }
                if (state.Stage != SummonStage.Alive) continue;
                var bosses = state.BossIds.Select(Find).ToArray();
                foreach (var id in state.BossIds.Where(id => Find(id) == null && NativeSummonIdentity.Destroyed(id)))
                {
                    var current = store.State;
                    if (current?.Stage == SummonStage.Alive && !current.KilledBossIds.Contains(id))
                        store.Change(current.MarkKilled(id), allowUncheckpointed: true);
                }
                if (!ReferenceEquals(state, store.State)) continue;
                // Missing ZDOs may be a normal kill, a succession, or a conflicting save. Never guess a refund.
                bool alive = bosses.All(b => b != null && b.GetFloat(ZDOVars.s_health, 1) > 0);
                foreach (var dead in bosses.Where(b => b != null && b.GetFloat(ZDOVars.s_health, 1) <= 0)) MarkDead(dead!);
                if (!ReferenceEquals(state, store.State)) continue;
                bool near = bosses.Any(b => b != null && Near(b, Plugin.Settings!.Get(WildbloodSettings.Bosses.AbandonRange)));
                var changed = state.Observe(paid && Active && Plugin.Settings!.Get(WildbloodSettings.Bosses.AbandonedBosses), near,
                    bosses.All(b => b != null && FullHealth(b)), alive, Now, Plugin.Settings!.Get(WildbloodSettings.Bosses.AbandonMinutes) * 60d);
                if (!ReferenceEquals(changed, state)) store.Change(changed);
            }
            catch (Exception error) { store.Warn(error); }
        }
        NativeSummonStore.SaveDue();
    }
    static NativeSummonStore Store(ZDO altar)
    {
        var id = SetIdentity(altar);
        if (!stores.TryGetValue(id, out var store)) stores.Add(id, store = new NativeSummonStore(altar, World));
        return store;
    }
    static void RestoreContext(NativeSummonStore store)
    {
        var state = store.State;
        if (state == null || state.Context.Length == 0 || state.Stage == SummonStage.Cancelled) return;
        var altar = Find(state.AltarId); if (altar == null) return;
        if (altar.GetString(PendingIdKey) == state.SummonId && altar.GetLong(PayerKey, 0) != 0) return;
        var packet = new ZPackage(Convert.FromBase64String(state.Context));
        if (packet.ReadInt() != 1) throw new InvalidDataException("Unknown summoned-boss context.");
        long actor = packet.ReadLong(); bool inventory = packet.ReadBool(); var point = packet.ReadVector3();
        if (actor == 0 || packet.GetPos() != packet.Size() || float.IsNaN(point.x) || float.IsNaN(point.y) || float.IsNaN(point.z)
            || float.IsInfinity(point.x) || float.IsInfinity(point.y) || float.IsInfinity(point.z))
            throw new InvalidDataException("Invalid summoned-boss context.");
        if (altar.GetString(PendingIdKey) != state.SummonId)
        { altar.Set(PrimaryIdentityKey, ""); altar.Set(PrimaryKey, ZDOID.None); }
        altar.Set(PayerKey, actor); altar.Set(InventoryKey, inventory); altar.Set(SpawnPointKey, point);
        altar.Set(PendingIdKey, state.SummonId);
    }
    static void EnsureWorld()
    {
        if (world == World) return;
        Reset(); world = World;
        NativeSummonIdentity.Scan();
        RepairLinks();
        string directory = NativeSummonStore.DirectoryFor(world);
        if (Directory.Exists(directory))
            foreach (string file in Directory.EnumerateFiles(directory, "*.wal"))
            {
                var parts = Path.GetFileNameWithoutExtension(file).Split('_');
                if (parts.Length != 2) continue;
                var altar = Find(parts[0] + ":" + parts[1]);
                if (altar != null) Store(altar);
            }
        foreach (var altar in ZDOMan.instance.m_objectsByID.Values.ToArray())
            if (altar.GetByteArray(ImageKey)?.Length > 0) Store(altar);
    }
    static void RepairLinks()
    {
        foreach (var member in ZDOMan.instance.m_objectsByID.Values)
        {
            string phantom = member.GetString(PhantomIdentityKey);
            if (phantom.Length != 0)
            {
                var parent = Find(phantom);
                if (parent != null) { member.SetOwner(ZDOMan.GetSessionID()); AspectStore.SetPhantomOf(member, parent.m_uid); }
            }
            if (member.GetString(NativeSummonIdentity.Key).Length == 0) continue;
            string twin = member.GetString(TwinIdentityKey), tether = member.GetString(TetherIdentityKey);
            if (twin.Length == 0 && tether.Length == 0) continue;
            member.SetOwner(ZDOMan.GetSessionID());
            if (twin.Length != 0) AspectStore.SetTwin(member, Find(twin)?.m_uid ?? ZDOID.None);
            if (tether.Length != 0) AspectStore.SetTether(member, Find(tether)?.m_uid ?? ZDOID.None);
        }
    }
    internal static void Reset()
    {
        foreach (var store in stores.Values) store.Dispose();
        stores.Clear(); requests.Clear(); NativeSummonIdentity.Reset(); world = 0; next = 0; executing = false; pendingContext = "";
        NativeSummonStore.ResetSave();
    }

    [HarmonyPatch(typeof(OfferingBowl), "InitiateSpawnBoss")]
    internal static class Initiation
    {
        static bool Prefix(OfferingBowl __instance, bool removeItemsFromInventory) => Initiate(__instance, removeItemsFromInventory);
    }
    [HarmonyPatch(typeof(OfferingBowl), "RPC_SpawnBoss")]
    internal static class OwnerAdmission
    {
        static bool Prefix(OfferingBowl __instance) => !IsBoss(__instance) || !Active || executing && Authority;
    }
    [HarmonyPatch(typeof(OfferingBowl), "DelayedSpawnBoss")]
    internal static class ExactSpawn
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            int found = 0;
            foreach (var instruction in instructions)
            {
                yield return instruction;
                if (instruction.opcode == OpCodes.Call && instruction.operand is MethodInfo method && method.DeclaringType == typeof(UnityEngine.Object)
                    && method.Name == "Instantiate" && method.ReturnType == typeof(GameObject))
                {
                    found++;
                    yield return new CodeInstruction(OpCodes.Dup);
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(NativeBossAbandon), nameof(Spawned)));
                }
            }
            if (found != 1) throw new InvalidOperationException("Wildblood cannot identify the exact altar boss spawn.");
        }
    }
    [HarmonyPatch(typeof(Character), "OnDeath")]
    internal static class Killed
    {
        [HarmonyPriority(Priority.First)]
        static void Prefix(Character __instance)
        {
            try
            {
                var view = __instance.m_nview;
                if (!view || !view.IsValid() || !view.IsOwner() || view.GetZDO().GetString(BossKey).Length == 0) return;
                if (Authority) MarkDead(view.GetZDO());
                else ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.instance.GetServerPeerID(), DeathRpc, view.GetZDO().m_uid);
            }
            catch (Exception error) { Plugin.NativeLogger?.LogWarning("Wildblood death receipt held: " + error.GetType().Name); }
        }
    }
    [HarmonyPatch(typeof(Character), "SetMaxHealth")]
    internal static class MaxHealth
    {
        static void Postfix(Character __instance)
        {
            var view = __instance.m_nview;
            if (view && view.IsValid() && view.IsOwner() && __instance.IsBoss()) view.GetZDO().Set(MaxHealthKey, __instance.GetMaxHealth());
        }
    }

    [HarmonyPatch(typeof(BossCopy), nameof(BossCopy.Make))]
    internal static class CopyIdentity
    {
        static void Postfix(EliteController boss, Character? __result)
        {
            if (__result == null || boss.View == null || !boss.View.IsValid() || boss.View.GetZDO().GetString(BossKey).Length == 0) return;
            var view = __result.m_nview;
            if (!view || !view.IsValid() || !view.IsOwner()) return;
            var copy = view.GetZDO();
            if (AspectStore.GetPhantomOf(copy) == boss.View.GetZDO().m_uid)
                copy.Set(PhantomIdentityKey, NativeSummonIdentity.Id(boss.View.GetZDO().m_uid));
        }
    }
}
