// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using BepInEx;
using ExtraPotions.Wildblood.Adopted.Transfers;
using ExtraPotions.Wildblood.Transfers;
using HarmonyLib;
using UnityEngine;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Wildblood.Runtime;

internal static class NativeSummonPayment
{
    static Inventory? heldInventory;
    static Player? heldPlayer;
    static long heldWorld;
    static string heldSource = "";
    static bool paying;
    static object? scannedContext;
    static float nextRecovery;
    static readonly Dictionary<string, float> warned = new(StringComparer.Ordinal);
    static readonly HashSet<string> declined = new(StringComparer.Ordinal);

    internal static void Tick()
    {
        if (!NativeSummonReceipts.Refresh() || ReferenceEquals(scannedContext, NativeSummonReceipts.Context) || Time.unscaledTime < nextRecovery) return;
        nextRecovery = Time.unscaledTime + 2;
        var player = Player.m_localPlayer;
        var inventory = player.GetInventory();
        long world = ZNet.instance.GetWorldUID();
        string actor = player.GetPlayerID().ToString(CultureInfo.InvariantCulture);
        string directory = Path.Combine(Paths.BepInExRootPath, "Wildblood.boss-offerings", world.ToString(CultureInfo.InvariantCulture), actor);
        if (!Directory.Exists(directory)) { scannedContext = NativeSummonReceipts.Context; return; }
        if (NativeSummonDelivery.Held(inventory) || NativeRaidChest.Held(inventory) || TheftTransfers.RawHeld(inventory)
            || TheftInventoryGuards.ForeignHeld(inventory, true)) return;
        Hold(player, inventory, world, "recovery");
        try
        {
            var files = Directory.EnumerateFiles(directory, "*.wal").Take(4097).ToArray();
            if (files.Length > 4096) throw new InvalidDataException("Summon payment recovery file bound.");
            foreach (string file in files)
            {
                using var journal = new InventoryDecisionJournal(file, 1, true);
                var record = journal.Snapshot().SingleOrDefault();
                if (record == null) continue;
                string source = "boss-offering:" + world.ToString(CultureInfo.InvariantCulture) + ":" + actor + ":" + record.Id;
                var packet = new ZPackage(record.Metadata);
                int version = packet.ReadInt();
                if (!Guid.TryParseExact(record.Id, "N", out _) || Path.GetFileNameWithoutExtension(file) != record.Id
                    || version != 1 && version != 2 || packet.ReadString() != source) throw new InvalidDataException("Summon payment recovery identity.");
                string altarId = version == 1 ? NativeBossAbandon.Id(packet.ReadZDOID()) : packet.ReadString();
                string prefab = packet.ReadString(); int amount = packet.ReadInt();
                if (string.IsNullOrWhiteSpace(prefab) || amount < 1 || packet.GetPos() != packet.Size()
                    || string.IsNullOrWhiteSpace(altarId)
                    || record.Source != source || record.Fingerprint != Fingerprint(record.Metadata, record.Before, record.After))
                    throw new InvalidDataException("Summon payment recovery image.");
                Hold(player, inventory, world, source);
                if (!Finish(journal, record, world, actor, player)) return;
            }
            heldInventory = null; heldPlayer = null; heldWorld = 0; heldSource = "";
            scannedContext = NativeSummonReceipts.Context;
        }
        catch (Exception error) { Warn("recovery", error.GetType().Name + error.Message, "Wildblood summon payment recovery held: " + error.GetType().Name + ": " + error.Message); }
    }

    internal static bool Held(Inventory inventory) => inventory != null && ReferenceEquals(inventory, heldInventory)
        && heldPlayer && heldPlayer == Player.m_localPlayer && ZNet.instance && ZNet.m_world != null
        && ZNet.instance.GetWorldUID() == heldWorld;

    // Only the manager's authenticated server Offer handler may call this method.
    internal static bool Pay(ZDOID altar, string summon, string prefab, int amount)
    {
        Tick();
        var player = Player.m_localPlayer;
        if (paying || !Plugin.Ready || !CruciblePlugin.IsReady || !CruciblePlugin.Session.AuthorityReady
            || !ZNet.instance || ZNet.m_world == null || !player || !Game.instance
            || !Guid.TryParseExact(summon, "N", out _) || string.IsNullOrWhiteSpace(prefab) || prefab.Length > 256
            || prefab.IndexOfAny(new[] { '\0', '\r', '\n' }) >= 0 || amount < 1 || amount > 1000000) return false;
        long world = ZNet.instance.GetWorldUID();
        var inventory = player.GetInventory();
        if (world == 0 || world != CruciblePlugin.Session.WorldId || !WildbloodInventory.Current(player, inventory)
            || TheftInventoryGuards.Loading(player) || player.IsDead() || player.IsTeleporting()
            || TheftTransfers.RawHeld(inventory) || NativeRaidChest.Held(inventory) || NativeSummonDelivery.Held(inventory)
            || TheftInventoryGuards.ForeignHeld(inventory, true)) return false;

        string actor = player.GetPlayerID().ToString(CultureInfo.InvariantCulture);
        string worldText = world.ToString(CultureInfo.InvariantCulture);
        string source = "boss-offering:" + worldText + ":" + actor + ":" + summon;
        if (Held(inventory) && heldSource != source) return false;
        var scene = ZNetScene.instance;
        var altarObject = scene ? scene.FindInstance(altar) : null;
        var bowl = altarObject ? altarObject.GetComponentInChildren<OfferingBowl>() : null;
        if (!bowl || bowl.m_useItemStands || !bowl.m_bossPrefab || !bowl.m_bossItem
            || bowl.m_bossItems != amount || Utils.GetPrefabName(bowl.m_bossItem.gameObject) != prefab
            || NativeBossAbandon.Altar(bowl)?.m_uid != altar
            || Vector3.Distance(player.transform.position, bowl.transform.position) > 20f) return false;

        var packet = new ZPackage();
        packet.Write(2); packet.Write(source); packet.Write(NativeBossAbandon.Id(altar)); packet.Write(prefab); packet.Write(amount);
        byte[] metadata = packet.GetArray();
        string path = Path.Combine(Paths.BepInExRootPath, "Wildblood.boss-offerings", worldText, actor, summon + ".wal");
        bool recorded = false, imaging = false;
        paying = true;
        try
        {
            // A receipt without its WAL is a backup mismatch, never permission to debit again.
            bool receiptExists = player.m_customData.ContainsKey(NativeTheftInventory.ReceiptKey(source));
            if (receiptExists && !File.Exists(path))
            {
                Hold(player, inventory, world, source);
                throw new InvalidDataException("Summon payment receipt has no matching journal.");
            }
            using var journal = new InventoryDecisionJournal(path, 1, receiptExists);
            var record = journal.Find(summon);
            if (record == null)
            {
                if (journal.Snapshot().Count != 0 || !journal.CanAdmit || receiptExists) return false;
                imaging = true;
                SettleDurability(inventory);
                byte[] before = NativeTheftInventory.Bytes(inventory);
                var shadow = NativeTheftInventory.Read(before);
                int remaining = amount;
                foreach (var item in shadow.GetAllItems().Where(item => item.m_dropPrefab
                    && Utils.GetPrefabName(item.m_dropPrefab) == prefab
                    && item.m_shared.m_name == bowl.m_bossItem.m_itemData.m_shared.m_name)
                    .OrderBy(item => item.m_gridPos.y).ThenBy(item => item.m_gridPos.x).ToArray())
                {
                    if (item.m_stack < 1) throw new InvalidDataException("Invalid summon offering stack.");
                    int take = Math.Min(remaining, item.m_stack);
                    item.m_stack -= take; remaining -= take;
                    if (item.m_stack == 0) shadow.GetAllItems().Remove(item);
                    if (remaining == 0) break;
                }
                if (remaining != 0 || !NativeTheftInventory.Bytes(inventory).SequenceEqual(before)) return false;
                byte[] after = NativeTheftInventory.Bytes(shadow);
                imaging = false;
                Hold(player, inventory, world, source);
                record = journal.Prepare(summon, source, Fingerprint(metadata, before, after), 0, before, after, metadata);
            }
            recorded = true;
            Hold(player, inventory, world, source);
            if (journal.Snapshot().Count != 1 || record.Source != source || !Matches(record.Metadata, source, altar, prefab, amount)
                || record.Fingerprint != Fingerprint(record.Metadata, record.Before, record.After))
                throw new InvalidDataException("Summon payment journal identity mismatch.");
            // The flushed immutable offering intent is resumed after a crash, just like a theft debit.
            if (record.State == InventoryDecision.Prepared) journal.Transition(record, InventoryDecision.Decided);
            if (record.State != InventoryDecision.Decided && record.State != InventoryDecision.Committed) return false;
            bool paid = Finish(journal, record, world, actor, player);
            if (paid) Release(inventory, source);
            return paid;
        }
        catch (Exception error)
        {
            // Imaging only reads: no record was prepared and nothing was debited. The same inventory fails the same way
            // every time, so the payer declines and the server cancels the summon instead of offering it again.
            if (imaging && !recorded && error is InvalidDataException
                && !player.m_customData.ContainsKey(NativeTheftInventory.ReceiptKey(source)) && DiscardEmpty(path))
            {
                Release(inventory, source);
                declined.Add(summon);
                Warn(summon, "declined", "Wildblood summon payment declined, nothing taken: " + error.Message);
                return false;
            }
            if (recorded || File.Exists(path)) Hold(player, inventory, world, source);
            Warn(summon, error.GetType().Name + error.Message, "Wildblood summon payment held: " + error.GetType().Name + ": " + error.Message);
            return false;
        }
        finally { paying = false; }
    }

    internal static bool TakeDeclined(string summon) => declined.Remove(summon);

    // Only the empty journal this payment created may go; a journal with any bytes stays for recovery.
    static bool DiscardEmpty(string path)
    {
        try
        {
            var file = new FileInfo(path);
            if (!file.Exists) return true;
            if (file.Length != 0) return false;
            file.Delete();
            return true;
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }

    static void Warn(string summon, string reason, string text)
    {
        string key = summon + "\n" + reason;
        if (warned.TryGetValue(key, out float until) && Time.unscaledTime < until) return;
        if (warned.Count >= 256) warned.Clear();
        warned[key] = Time.unscaledTime + 60f;
        Plugin.NativeLogger?.LogWarning(text);
    }

    // A native save stores durability as whole hundredths and a load multiplies them by 0.01f, so a worn item can
    // come back one hundredth lower and its image never round-trips. Apply that same save/load step now; the item
    // keeps the value its next character save and load would give it, and the exact round-trip check stays strict.
    static void SettleDurability(Inventory inventory)
    {
        foreach (var item in inventory.GetAllItems())
        {
            if (float.IsNaN(item.m_durability) || float.IsInfinity(item.m_durability)) continue;
            for (int step = 0; step < 16; step++)
            {
                int previous = BitConverter.SingleToInt32Bits(item.m_durability);
                item.m_durability = (int)(item.m_durability * 100f) * 0.01f;
                if (BitConverter.SingleToInt32Bits(item.m_durability) == previous) break;
            }
        }
    }

    static bool Matches(byte[] metadata, string source, ZDOID altar, string prefab, int amount)
    {
        var packet = new ZPackage(metadata); int version = packet.ReadInt();
        if (version != 1 && version != 2 || packet.ReadString() != source) return false;
        string storedAltar = version == 1 ? NativeBossAbandon.Id(packet.ReadZDOID()) : packet.ReadString();
        return storedAltar == NativeBossAbandon.Id(altar) && packet.ReadString() == prefab && packet.ReadInt() == amount && packet.GetPos() == packet.Size();
    }

    static bool Finish(InventoryDecisionJournal journal, InventoryDecisionRecord record, long world, string actor, Player player)
    {
        if (record.State == InventoryDecision.Prepared) journal.Transition(record, InventoryDecision.Decided);
        if (record.State != InventoryDecision.Decided && record.State != InventoryDecision.Committed) return false;
        var store = new NativeSummonInventory(journal, world, actor, player, record);
        var participant = new DurableInventoryParticipant(journal, store, () => Time.realtimeSinceStartupAsDouble);
        return participant.Commit(record.Id) == InventoryDecision.Committed && store.ReceiptConfirmed(record.Source);
    }

    static void Hold(Player player, Inventory inventory, long world, string source)
    { heldPlayer = player; heldInventory = inventory; heldWorld = world; heldSource = source; scannedContext = null; }
    static void Release(Inventory inventory, string source)
    {
        if (!ReferenceEquals(heldInventory, inventory) || heldSource != source) return;
        heldInventory = null; heldPlayer = null; heldWorld = 0; heldSource = "";
    }
    static string Fingerprint(byte[] metadata, byte[] before, byte[] after)
    {
        var packet = new ZPackage(); packet.Write(metadata); packet.Write(before); packet.Write(after);
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(packet.GetArray())).Replace("-", "").ToLowerInvariant();
    }
}

// Loaded character receipts prove an image even if inventory activity followed the interrupted journal commit.
internal static class NativeSummonReceipts
{
    static Player? player;
    static long world, actor, generation;
    static readonly HashSet<string> confirmed = new(StringComparer.Ordinal), failed = new(StringComparer.Ordinal);
    internal static object Context { get; private set; } = new();
    internal static bool Refresh()
    {
        var local = Player.m_localPlayer;
        if (!Plugin.Ready || !CruciblePlugin.IsReady || !CruciblePlugin.Session.AuthorityReady || !local
            || !ZNet.instance || ZNet.m_world == null || TheftInventoryGuards.Loading(local)
            || !WildbloodInventory.Current(local, local.GetInventory())) return false;
        long current = ZNet.instance.GetWorldUID();
        if (player == local && world == current && actor == local.GetPlayerID() && generation == CruciblePlugin.Session.Generation) return true;
        player = local; world = current; actor = local.GetPlayerID(); generation = CruciblePlugin.Session.Generation;
        confirmed.Clear();
        const string prefix = "wildblood.theft.receipt.";
        foreach (var pair in local.m_customData)
            if (pair.Value == "1" && pair.Key.StartsWith(prefix, StringComparison.Ordinal))
            {
                string source = pair.Key.Substring(prefix.Length);
                if (!failed.Contains(source)) confirmed.Add(source);
            }
        Context = new object(); return true;
    }
    internal static bool Has(string source) => Refresh() && confirmed.Contains(source)
        && player!.m_customData.TryGetValue(NativeTheftInventory.ReceiptKey(source), out var receipt) && receipt == "1";
    internal static void Saving(string source) { failed.Add(source); confirmed.Remove(source); }
    internal static void Saved(string source) { failed.Remove(source); confirmed.Add(source); }
    static void Invalidate() { player = null; world = actor = generation = 0; confirmed.Clear(); Context = new object(); }
    [HarmonyPatch(typeof(PlayerProfile), nameof(PlayerProfile.LoadPlayerData))]
    internal static class ProfileLoad
    {
        static void Prefix() => Invalidate();
        static void Postfix() { failed.Clear(); Invalidate(); }
    }
}

internal sealed class NativeSummonInventory : IInventoryImageStore, IInventoryCheckpointStore
{
    readonly NativeTheftInventory native;
    readonly InventoryDecisionRecord record;
    readonly long world;
    readonly string actor;
    readonly Player player;
    internal NativeSummonInventory(InventoryDecisionJournal journal, long world, string actor, Player player, InventoryDecisionRecord record)
    { native = new NativeTheftInventory(journal, world, actor, player); this.record = record; this.world = world; this.actor = actor; this.player = player; }
    bool Current => player && player == Player.m_localPlayer && ZNet.instance && ZNet.m_world != null
        && ZNet.instance.GetWorldUID() == world && player.GetPlayerID().ToString(CultureInfo.InvariantCulture) == actor
        && WildbloodInventory.Current(player, player.GetInventory()) && !TheftInventoryGuards.Loading(player);
    public bool IsBound(string source) => Current && record.Source == source && (NativeSummonReceipts.Has(source) || native.IsBound(source));
    public bool ReceiptConfirmed(string source) => Current && record.Source == source && (NativeSummonReceipts.Has(source) || native.ReceiptConfirmed(source));
    public byte[] Capture(string source) => ReceiptConfirmed(source) ? record.After : native.Capture(source);
    public void Apply(string source, byte[] image) { if (!ReceiptConfirmed(source)) native.Apply(source, image); }
    public void Save(string source)
    {
        if (ReceiptConfirmed(source)) return;
        NativeSummonReceipts.Saving(source);
        native.Save(source);
        NativeSummonReceipts.Saved(source);
    }
}
