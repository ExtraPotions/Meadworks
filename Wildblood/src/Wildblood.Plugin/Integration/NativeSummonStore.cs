// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using BepInEx;
using ExtraPotions.Wildblood.Adopted.Traits;
using ExtraPotions.Wildblood.Bosses;
using ExtraPotions.Wildblood.Transfers;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Wildblood.Runtime;

// The WAL decides; the altar image, refund objects and destruction receipts share a native world snapshot.
internal sealed class NativeSummonStore : IInventoryImageStore, IInventoryCheckpointStore, IDisposable
{
    const string ReceiptKey = "wildblood.summon.receipt";
    readonly ZDO altar;
    readonly long world;
    readonly string identity;
    readonly InventoryDecisionJournal journal;
    readonly DurableInventoryParticipant participant;
    InventoryDecisionRecord? binding;
    SummonLedgerTransition? preparing;
    string savedReceipt;
    bool faulted;
    bool dropsReleased;
    string lastWarning = "";
    internal SummonLedgerSnapshot? State { get; private set; }
    internal bool Ready => !faulted && binding != null && participant.Status(binding.Id) == InventoryDecision.Committed || !faulted && binding == null;
    static readonly object saveGate = new();
    static readonly Dictionary<string, NativeSummonStore> saveStores = new(StringComparer.Ordinal);
    static readonly HashSet<string> readyRefunds = new(StringComparer.Ordinal);
    internal static bool RefundClaimReady(string token) => readyRefunds.Any(id => token.StartsWith(id + ":", StringComparison.Ordinal));
    static Dictionary<string, string> captured = new(StringComparer.Ordinal), completed = new(StringComparer.Ordinal);
    static float saveRequested, saveRetry;
    [ThreadStatic] static bool worldSaveThread;
    [ThreadStatic] static Dictionary<string, string>? writingReceipts;
    [ThreadStatic] static int writingEpoch;
    static long saveWorld;
    static int saveEpoch;
    static long completedWorld;
    static int completedEpoch;

    internal static string DirectoryFor(long world) => Path.Combine(Paths.BepInExRootPath, "Wildblood.boss-summons", world.ToString(CultureInfo.InvariantCulture));
    internal NativeSummonStore(ZDO altar, long world)
    {
        this.altar = altar; this.world = world;
        string id = identity = NativeBossAbandon.SetIdentity(altar);
        journal = new InventoryDecisionJournal(Path.Combine(DirectoryFor(world), id.Replace(':', '_') + ".wal"));
        try
        {
            savedReceipt = altar.GetString(ReceiptKey);
            participant = new DurableInventoryParticipant(journal, this, () => Time.realtimeSinceStartupAsDouble);
            var image = altar.GetByteArray(NativeBossAbandon.ImageKey);
            if (image?.Length > 0) State = SummonLedgerSnapshot.Decode(image);
            saveStores.Add(id, this);
            Recover();
        }
        catch { journal.Dispose(); throw; }
    }
    bool Current => NativeBossAbandon.Authority && NativeBossAbandon.World == world && NativeBossAbandon.Find(identity) == altar;
    public bool IsBound(string source) => Current && (preparing?.Source == source || binding?.Source == source);
    public byte[] Capture(string source)
    {
        if (!IsBound(source)) throw new InvalidOperationException("Summon altar unavailable.");
        return altar.GetByteArray(NativeBossAbandon.ImageKey) ?? Array.Empty<byte>();
    }
    public bool ReceiptConfirmed(string source)
    {
        if (!IsBound(source)) return false;
        if (binding == null) return true;
        if (!Capture(source).SequenceEqual(binding.After)) return false;
        var snapshot = SummonLedgerSnapshot.Decode(binding!.After);
        return snapshot.Stage != SummonStage.RefundDelivered || savedReceipt == binding.Fingerprint;
    }
    internal void Change(SummonLedgerSnapshot next, bool allowUncheckpointed = false)
    {
        if (!Current || faulted || !Ready && !allowUncheckpointed) throw new InvalidOperationException("Summon recovery is pending.");
        var transition = new SummonLedgerTransition(State, next);
        InventoryDecision prepared;
        preparing = transition;
        try { prepared = participant.Prepare(transition.Id, transition.Source, transition.Fingerprint, transition.Before, transition.After); }
        finally { preparing = null; }
        if (prepared != InventoryDecision.Rejected) binding = journal.Find(transition.Id);
        if (binding == null || prepared == InventoryDecision.Rejected) throw new IOException("Summon ledger refused the transition.");
        var decision = participant.Commit(binding.Id);
        if (decision != InventoryDecision.Committed && decision != InventoryDecision.RecoveryRequired) throw new IOException("Summon decision not committed.");
        var image = Capture(binding.Source);
        if (!image.SequenceEqual(transition.After)) throw new IOException("Summon world image requires recovery.");
        State = next;
    }
    internal void Recover()
    {
        if (!Current || faulted) return;
        var records = journal.Snapshot().Where(r => r.State == InventoryDecision.Decided || r.State == InventoryDecision.Committed).ToArray();
        var current = altar.GetByteArray(NativeBossAbandon.ImageKey) ?? Array.Empty<byte>();
        int start = Array.FindLastIndex(records, r => r.After.SequenceEqual(current));
        // A later image is authoritative for older transitions; never replay an old refund because its drop was picked up.
        for (int i = start + 1; i < records.Length; i++)
        {
            var record = records[i];
            if (!record.Before.SequenceEqual(current)) { faulted = true; throw new InvalidDataException("Summon ledger/world image conflict."); }
            binding = record;
            var result = participant.Commit(record.Id);
            current = altar.GetByteArray(NativeBossAbandon.ImageKey) ?? Array.Empty<byte>();
            if (!current.SequenceEqual(record.After)) throw new IOException("Summon decision still requires world recovery: " + result);
        }
        binding = records.LastOrDefault();
        if (current.Length != 0) State = SummonLedgerSnapshot.Decode(current);
        // A subsequent summon can be admitted only after the preceding refund's world checkpoint.
        foreach (var prior in records.Select(r => SummonLedgerSnapshot.Decode(r.After)))
            if (prior.Stage == SummonStage.RefundDelivered && State != null && prior.SummonId != State.SummonId)
                readyRefunds.Add(prior.RefundId);
        if (binding != null && (binding.State == InventoryDecision.Decided || participant.Status(binding.Id) == InventoryDecision.RecoveryRequired)) participant.Commit(binding.Id);
        ReleaseCheckpointedDrops();
    }
    public void Apply(string source, byte[] image)
    {
        if (!IsBound(source) || !image.SequenceEqual(binding!.After) || !Capture(source).SequenceEqual(binding.Before))
            throw new InvalidDataException("Summon image is not the decided transition.");
        var snapshot = SummonLedgerSnapshot.Decode(image);
        if (snapshot.WorldUid != world || snapshot.AltarId != identity) throw new InvalidDataException("Summon binding changed.");
        altar.SetOwner(ZDOMan.GetSessionID());
        if (snapshot.Stage == SummonStage.Pending) dropsReleased = false;
        if (snapshot.Stage == SummonStage.RefundDecided) Deliver(snapshot);
        altar.Set(NativeBossAbandon.ImageKey, image);
    }
    void Deliver(SummonLedgerSnapshot snapshot)
    {
        var existing = ZDOMan.instance.m_objectsByID.Values.Where(z => z.GetString(NativeBossAbandon.RefundKey).StartsWith(snapshot.RefundId + ":", StringComparison.Ordinal)).ToArray();
        var costs = snapshot.Offerings.GroupBy(o => o.Prefab, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => new SummonOffering(g.Key, checked(g.Sum(o => o.Amount)))).ToArray();
        // Validate every prefab before the first mutation; a missing modded offering keeps the decided refund held.
        foreach (var cost in costs)
            if (!ZNetScene.instance.GetPrefab(cost.Prefab)?.GetComponent<ItemDrop>()) throw new InvalidDataException("Refund offering prefab unavailable.");
        foreach (var id in snapshot.BossIds)
        {
            var boss = NativeBossAbandon.Find(id);
            if (boss == null) continue;
            var nativeId = boss.m_uid;
            bool rawLinksAllowed = nativeId != ZDOID.None && NativeSummonIdentity.Fresh(id);
            DestroyBoss(boss);
            foreach (var copy in ZDOMan.instance.m_objectsByID.Values.ToArray())
                if (copy.GetString("wildblood.summon.phantom-parent") == id
                    || rawLinksAllowed && (AspectStore.GetPhantomOf(copy) == nativeId || AspectStore.GetEchoOf(copy) == nativeId)) DestroyBoss(copy);
        }
        int ordinal = 0;
        foreach (var cost in costs)
        {
            var prefab = ZNetScene.instance.GetPrefab(cost.Prefab);
            int remaining = cost.Amount, max = Math.Max(1, prefab.GetComponent<ItemDrop>().m_itemData.m_shared.m_maxStackSize);
            while (remaining > 0)
            {
                int amount = Math.Min(remaining, max); remaining -= amount;
                string token = snapshot.RefundId + ":" + (ordinal++).ToString(CultureInfo.InvariantCulture);
                if (existing.Any(z => z.GetString(NativeBossAbandon.RefundKey) == token)) continue;
                var drop = UnityEngine.Object.Instantiate(prefab, altar.GetPosition() + Vector3.up, Quaternion.identity).GetComponent<ItemDrop>();
                var zdo = drop.m_nview.GetZDO();
                zdo.Set(NativeBossAbandon.RefundKey, token); zdo.Set(NativeBossAbandon.HoldKey, true);
                drop.m_itemData.m_stack = amount; drop.m_itemData.m_worldLevel = Game.m_worldLevel;
                drop.m_autoPickup = false; drop.m_autoDestroy = false;
                drop.Save();
            }
        }
    }
    static void DestroyBoss(ZDO zdo)
    {
        zdo.SetOwner(ZDOMan.GetSessionID());
        if (zdo.GetBool("bosscount"))
        {
            zdo.Set("bosscount", false);
            ZoneSystem.instance.GetGlobalKey(GlobalKeys.activeBosses, out float count);
            ZoneSystem.instance.SetGlobalKey(GlobalKeys.activeBosses, Mathf.Max(0, count - 1));
        }
        var live = ZNetScene.instance.FindInstance(zdo);
        if (live) ZNetScene.instance.Destroy(live.gameObject);
        ZDOMan.instance.DestroyZDO(zdo);
        // Flush the local destroy queue before a save snapshot can retain both boss and refund.
        ZDOMan.instance.SendDestroyed();
    }
    public void Save(string source)
    {
        if (!IsBound(source) || !Capture(source).SequenceEqual(binding!.After)) throw new IOException("Summon image not applied.");
        altar.Set(ReceiptKey, binding.Fingerprint);
        if (SummonLedgerSnapshot.Decode(binding.After).Stage == SummonStage.RefundDelivered)
            if (saveRequested == 0) saveRequested = Time.unscaledTime + 0.1f;
    }
    void ReleaseCheckpointedDrops()
    {
        if (dropsReleased || State?.Stage != SummonStage.RefundDelivered || binding == null || savedReceipt != binding.Fingerprint) return;
        foreach (var zdo in ZDOMan.instance.m_objectsByID.Values)
            if (zdo.GetString(NativeBossAbandon.RefundKey).StartsWith(State.RefundId + ":", StringComparison.Ordinal) && zdo.GetBool(NativeBossAbandon.HoldKey))
            {
                zdo.SetOwner(ZDOMan.GetSessionID()); zdo.Set(NativeBossAbandon.HoldKey, false);
                var drop = ZNetScene.instance.FindInstance(zdo)?.GetComponent<ItemDrop>();
                if (drop) { drop.m_autoPickup = true; drop.m_autoDestroy = true; }
            }
        dropsReleased = true;
        readyRefunds.Add(State.RefundId);
    }
    internal void Warn(Exception error)
    {
        var code = error.GetType().Name + ":" + error.Message;
        if (lastWarning == code) return; lastWarning = code;
        Plugin.NativeLogger?.LogWarning("Wildblood altar " + NativeBossAbandon.Id(altar.m_uid) + " held: " + code);
    }
    internal static void SaveDue()
    {
        Dictionary<string, string> done;
        lock (saveGate)
        {
            done = completedWorld == NativeBossAbandon.World && completedEpoch == saveEpoch ? completed : new(StringComparer.Ordinal);
            completed = new(StringComparer.Ordinal);
        }
        foreach (var pair in done) if (saveStores.TryGetValue(pair.Key, out var store)) store.savedReceipt = pair.Value;
        if (saveRequested > 0 && Time.unscaledTime >= saveRequested && Time.unscaledTime >= saveRetry && !ZNet.instance.IsSaving())
        {
            saveRequested = 0; saveRetry = Time.unscaledTime + 10;
            ZNet.instance.Save(false);
        }
    }
    internal static void ResetSave()
    {
        saveStores.Clear(); readyRefunds.Clear(); saveRequested = saveRetry = 0;
        lock (saveGate) { captured.Clear(); completed.Clear(); saveWorld = 0; completedWorld = 0; saveEpoch++; }
    }
    public void Dispose() { journal.Dispose(); saveStores.Remove(identity); }

    [HarmonyPatch(typeof(ZDOMan), "PrepareSave")]
    internal static class CaptureSave
    {
        static void Postfix()
        {
            if (!NativeBossAbandon.Authority) return;
            lock (saveGate)
            {
                saveWorld = NativeBossAbandon.World;
                captured = saveStores.ToDictionary(p => p.Key, p => p.Value.altar.GetString(ReceiptKey), StringComparer.Ordinal);
            }
        }
    }
    [HarmonyPatch(typeof(ZNet), "SaveWorldThread")]
    internal static class WorldSaveScope
    {
        static void Prefix()
        {
            worldSaveThread = true;
            lock (saveGate) { writingReceipts = new(captured, StringComparer.Ordinal); writingEpoch = saveEpoch; }
        }
        static Exception? Finalizer(Exception? __exception) { worldSaveThread = false; writingReceipts = null; return __exception; }
    }
    [HarmonyPatch]
    internal static class SuccessfulSave
    {
        static MethodBase TargetMethod() => AccessTools.Method(AccessTools.TypeByName("SaveSystem"), "EndSave");
        static void Postfix(bool writeOK)
        {
            if (!worldSaveThread) return;
            lock (saveGate)
            {
                if (writeOK && writingEpoch == saveEpoch && writingReceipts != null)
                {
                    completed = new(writingReceipts, StringComparer.Ordinal);
                    completedWorld = saveWorld; completedEpoch = writingEpoch;
                }
            }
        }
    }
    internal static bool Held(ItemDrop drop) => drop && drop.m_nview && drop.m_nview.IsValid()
        && (drop.m_nview.GetZDO().GetBool(NativeBossAbandon.HoldKey) || drop.m_nview.GetZDO().GetString(NativeBossAbandon.RefundKey).Length != 0);
    [HarmonyPatch(typeof(ItemDrop), nameof(ItemDrop.CanPickup))]
    internal static class PickupGuard
    {
        static bool Prefix(ItemDrop __instance, ref bool __result) { if (!Held(__instance)) return true; __result = false; return false; }
    }
    [HarmonyPatch(typeof(ItemDrop), "AutoStackItems")]
    internal static class StackGuard
    {
        static bool Prefix(ItemDrop __instance) => !Held(__instance);
        static ItemDrop? Available(ItemDrop drop) => Held(drop) ? null : drop;
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            int found = 0;
            foreach (var instruction in instructions)
            {
                yield return instruction;
                if (instruction.operand is MethodInfo method && method.Name == "GetComponent" && method.ReturnType == typeof(ItemDrop))
                {
                    found++;
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(StackGuard), nameof(Available)));
                }
            }
            if (found != 1) throw new InvalidOperationException("Wildblood cannot guard refund stacking.");
        }
    }
    [HarmonyPatch(typeof(ItemDrop), "TimedDestruction")]
    internal static class ExpiryGuard { static bool Prefix(ItemDrop __instance) => !Held(__instance); }
}
