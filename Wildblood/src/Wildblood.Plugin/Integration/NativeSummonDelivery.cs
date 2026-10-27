// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using BepInEx;
using ExtraPotions.Wildblood.Adopted.Runtime;
using ExtraPotions.Wildblood.Adopted.Transfers;
using ExtraPotions.Wildblood.AdoptedLibs.PlayerGrid;
using ExtraPotions.Wildblood.Transfers;
using HarmonyLib;
using UnityEngine;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Wildblood.Runtime;

internal static class NativeSummonDelivery
{
    const string RequestRpc = "Wildblood.Summon.Refund.Request", GrantRpc = "Wildblood.Summon.Refund.Grant",
        AckRpc = "Wildblood.Summon.Refund.Ack", NoticeRpc = "Wildblood.Summon.Refund.Notice";
    const int MaximumClaims = 4096, MaximumPacket = 2048;
    sealed class Claim
    {
        internal long World, Actor;
        internal string Token = "", Prefab = "";
        internal ZDOID Drop;
        internal int Amount, WorldLevel;
        internal Vector3 Position;
        internal InventoryDecision State;
        internal string Source => "boss-refund:" + World.ToString(CultureInfo.InvariantCulture) + ":" + Actor.ToString(CultureInfo.InvariantCulture) + ":" + Token;
        internal byte[] Encode()
        {
            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                writer.Write(1); writer.Write(World); writer.Write(Actor); writer.Write(Token);
                writer.Write(Drop.UserID); writer.Write(Drop.ID); writer.Write(Prefab); writer.Write(Amount); writer.Write(WorldLevel);
                writer.Write(Position.x); writer.Write(Position.y); writer.Write(Position.z);
            }
            return stream.ToArray();
        }
        internal static Claim Decode(byte[] bytes)
        {
            if (bytes == null || bytes.Length > MaximumPacket) throw new InvalidDataException("Refund claim packet bound.");
            using var stream = new MemoryStream(bytes, false);
            using var reader = new BinaryReader(stream, Encoding.UTF8);
            if (reader.ReadInt32() != 1) throw new InvalidDataException("Refund claim version.");
            var value = new Claim { World = reader.ReadInt64(), Actor = reader.ReadInt64(), Token = reader.ReadString(),
                Drop = new ZDOID(reader.ReadInt64(), reader.ReadUInt32()), Prefab = reader.ReadString(), Amount = reader.ReadInt32(),
                WorldLevel = reader.ReadInt32(), Position = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()) };
            if (stream.Position != stream.Length || value.World == 0 || value.Actor == 0 || !Canonical(value.Token)
                || string.IsNullOrWhiteSpace(value.Prefab) || value.Prefab.Length > 256
                || value.Prefab.IndexOfAny(new[] { '\0', '\r', '\n' }) >= 0 || value.Amount < 1 || value.Amount > 1000000
                || value.WorldLevel < 0 || value.WorldLevel > 10000 || !Finite(value.Position))
                throw new InvalidDataException("Refund claim facts.");
            return value;
        }
    }
    static readonly Dictionary<string, Claim> claims = new(StringComparer.Ordinal);
    static readonly Dictionary<string, float> sent = new(StringComparer.Ordinal), requested = new(StringComparer.Ordinal);
    static ZNet? serverNetwork;
    static long serverWorld;
    static bool serverFault, granting;
    static float nextServer, nextNotice;
    static object? scannedContext;
    static float nextRecovery;
    static Inventory? heldInventory;
    static Player? heldPlayer;
    static long heldWorld;
    static string heldSource = "";

    internal static bool Held(Inventory inventory) => inventory != null && ReferenceEquals(inventory, heldInventory)
        && heldPlayer && heldPlayer == Player.m_localPlayer && ZNet.instance && ZNet.m_world != null
        && ZNet.instance.GetWorldUID() == heldWorld;
    static bool Canonical(string token)
    {
        if (string.IsNullOrEmpty(token) || token.Length > 64) return false;
        var parts = token.Split(':');
        return parts.Length == 3 && Guid.TryParseExact(parts[0], "N", out _) && parts[1] == "refund"
            && int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out int ordinal)
            && ordinal >= 0 && ordinal < MaximumClaims && parts[2] == ordinal.ToString(CultureInfo.InvariantCulture);
    }
    static bool Finite(Vector3 value) => !float.IsNaN(value.sqrMagnitude) && !float.IsInfinity(value.sqrMagnitude);
    static string Token(ItemDrop drop) => drop && drop.m_nview && drop.m_nview.IsValid()
        ? drop.m_nview.GetZDO().GetString(NativeBossAbandon.RefundKey) : "";
    static ItemDrop.ItemData ReadOffering(ZDO drop, out string name)
    {
        var prefab = ZNetScene.instance.GetPrefab(drop.GetPrefab());
        var definition = prefab ? prefab.GetComponent<ItemDrop>() : null;
        if (!definition) throw new InvalidDataException("Refund offering prefab unavailable.");
        var item = definition.m_itemData.Clone(); item.m_dropPrefab = prefab;
        ItemDrop.LoadFromZDO(item, drop);
        name = Utils.GetPrefabName(prefab);
        var expected = definition.m_itemData.Clone(); expected.m_dropPrefab = prefab; expected.m_worldLevel = item.m_worldLevel;
        if (item.m_stack < 1 || item.m_stack > 1000000 || !NativeTheftInventory.SameExceptCount(item, expected))
            throw new InvalidDataException("Refund offering metadata differs from its recorded prefab.");
        return item;
    }
    static bool AddComplete(Player player, Inventory live, Inventory inventory, ItemDrop.ItemData item)
    {
        string prefab = Utils.GetPrefabName(item.m_dropPrefab); int amount = item.m_stack;
        if (!WildbloodInventory.Current(player, live) || !live.CanAddItem(item, amount)
            || !WildbloodInventory.TryProtected(player, live, out var protectedCells)
            || !PackPanelGrid.TryMainRows(player, out int mainRows) || mainRows <= 0
            || live.GetWidth() != inventory.GetWidth() || live.GetHeight() != inventory.GetHeight()) return false;
        var original = inventory.GetAllItems().ToArray();
        var originalCells = original.Select(entry => entry.m_gridPos).ToArray();
        long Count() => inventory.GetAllItems().Where(i => i.m_dropPrefab && Utils.GetPrefabName(i.m_dropPrefab) == prefab).Sum(i => (long)i.m_stack);
        long before = Count();
        if (!inventory.AddItem(item) || Count() - before != amount) return false;
        bool MainCell(Vector2i cell) => cell.x >= 0 && cell.x < inventory.GetWidth() && cell.y >= 0
            && cell.y < inventory.GetHeight() && cell.y < mainRows && !protectedCells.Contains(cell);
        // Native shadow inventories do not receive the live player's reserved-cell routing.
        foreach (var added in inventory.GetAllItems().Where(entry => !original.Contains(entry)))
        {
            if (MainCell(added.m_gridPos)) continue;
            Vector2i target = new(-1, -1);
            for (int y = 0; y < Math.Min(mainRows, inventory.GetHeight()) && target.x < 0; y++)
                for (int x = 0; x < inventory.GetWidth(); x++)
                {
                    var cell = new Vector2i(x, y);
                    if (MainCell(cell) && !inventory.GetAllItems().Any(entry => entry.m_gridPos == cell)) { target = cell; break; }
                }
            if (target.x < 0) return false;
            added.m_gridPos = target;
        }
        return original.Select((entry, index) => entry.m_gridPos == originalCells[index]).All(unchanged => unchanged)
            && WildbloodInventory.Current(player, live) && live.CanAddItem(item, amount)
            && WildbloodInventory.TryProtected(player, live, out var latestProtected) && protectedCells.SetEquals(latestProtected)
            && PackPanelGrid.TryMainRows(player, out int latestRows) && latestRows == mainRows;
    }
    static string Hash(byte[] bytes)
    { using var sha = SHA256.Create(); return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
    static string Name(string token) => Hash(Encoding.UTF8.GetBytes(token));
    static string ServerDirectory(long world) => Path.Combine(Paths.BepInExRootPath, "Wildblood.boss-refund-claims", world.ToString(CultureInfo.InvariantCulture));
    static string ClientDirectory(long world, long actor) => Path.Combine(Paths.BepInExRootPath, "Wildblood.boss-refund-grants", world.ToString(CultureInfo.InvariantCulture), actor.ToString(CultureInfo.InvariantCulture));
    static void Warning(Exception error) => Plugin.NativeLogger?.LogWarning("Wildblood boss refund held: " + error.GetType().Name);
    static void LocalNotice()
    { if (Time.unscaledTime >= nextNotice) { nextNotice = Time.unscaledTime + 5; WildbloodText.TopLeft(WildbloodText.Text("boss_summon_recovery_pending")); } }
    static void NoRoom() => WildbloodText.TopLeft(global::Localization.instance.Localize("$msg_noroom"));
    static void Notice(long sender) => ZRoutedRpc.instance.InvokeRoutedRPC(sender, NoticeRpc, "boss_summon_recovery_pending");
    static void Receive(long sender, ZPackage packet, Action<long, byte[]> handler)
    {
        if (packet != null && packet.Size() <= MaximumPacket) handler(sender, packet.GetArray());
    }

    static void Register()
    {
        var bus = ZRoutedRpc.instance;
        if (bus == null || NativeRpcOwnership.OwnsRouted(bus, NativeRpcOwner.BossRefundDelivery)) return;
        NativeRpcOwnership.Routed(bus, NativeRpcOwner.BossRefundDelivery, () =>
        {
            bus.Register<ZPackage>(RequestRpc, (sender, packet) => Receive(sender, packet, Request));
            bus.Register<ZPackage>(GrantRpc, (sender, packet) => Receive(sender, packet, Grant));
            bus.Register<ZPackage>(AckRpc, (sender, packet) => Receive(sender, packet, Acknowledge));
            bus.Register<string>(NoticeRpc, (sender, key) =>
            { if (sender == ZRoutedRpc.instance.GetServerPeerID() && key == "boss_summon_recovery_pending") LocalNotice(); });
        }, () => { }, RequestRpc, GrantRpc, AckRpc, NoticeRpc);
    }

    internal static void Tick()
    {
        RecoverClient();
        if (!Plugin.Ready || !CruciblePlugin.IsReady || !CruciblePlugin.Session.AuthorityReady || ZRoutedRpc.instance == null) return;
        Register();
        if (!NativeBossAbandon.Authority || !EnsureServer() || Time.unscaledTime < nextServer) return;
        nextServer = Time.unscaledTime + 1;
        foreach (var zdo in ZDOMan.instance.m_objectsByID.Values.ToArray())
        {
            string token = zdo.GetString(NativeBossAbandon.RefundKey);
            if (token.Length > 0 && claims.TryGetValue(token, out var claim) && claim.State == InventoryDecision.Committed) Destroy(zdo);
        }
        foreach (var claim in claims.Values.Where(c => c.State == InventoryDecision.Decided).ToArray())
            foreach (var peer in Recipients(claim.Actor))
                if (Character(peer) is ZDO player && Near(player, claim.Position)) SendGrant(peer, claim);
    }

    static bool EnsureServer()
    {
        if (!NativeBossAbandon.Authority) return false;
        long world = ZNet.instance.GetWorldUID();
        if (ReferenceEquals(serverNetwork, ZNet.instance) && serverWorld == world) return !serverFault;
        claims.Clear(); sent.Clear(); serverFault = false; serverNetwork = ZNet.instance; serverWorld = world;
        try
        {
            string directory = ServerDirectory(world);
            if (!Directory.Exists(directory)) return true;
            var files = Directory.EnumerateFiles(directory, "*.wal").Take(MaximumClaims + 1).ToArray();
            if (files.Length > MaximumClaims) throw new InvalidDataException("Refund claim history bound.");
            foreach (string file in files)
            {
                using var journal = new InventoryDecisionJournal(file, 1, true);
                var record = journal.Snapshot().SingleOrDefault();
                if (record == null) continue;
                var claim = VerifyClaim(record, world);
                if (Path.GetFileNameWithoutExtension(file) != Name(claim.Token) || claims.ContainsKey(claim.Token))
                    throw new InvalidDataException("Refund claim file identity.");
                if (record.State == InventoryDecision.Prepared) journal.Transition(record, InventoryDecision.Decided);
                claim.State = record.State; claims.Add(claim.Token, claim);
            }
            return true;
        }
        catch (Exception error) { serverFault = true; Warning(error); return false; }
    }
    static Claim VerifyClaim(InventoryDecisionRecord record, long world)
    {
        var claim = Claim.Decode(record.Metadata);
        if (claim.World != world || record.Id != claim.Token || record.Source != claim.Source
            || record.Fingerprint != Hash(record.Metadata) || record.Before.Length != 0 || !record.After.SequenceEqual(record.Metadata)
            || record.State != InventoryDecision.Prepared && record.State != InventoryDecision.Decided && record.State != InventoryDecision.Committed)
            throw new InvalidDataException("Refund claim journal conflict.");
        return claim;
    }
    static ZDO? Character(long sender)
    {
        if (sender == ZDOMan.GetSessionID())
        {
            var local = Player.m_localPlayer;
            return local && local.m_nview && local.m_nview.IsValid() && local.m_nview.IsOwner() ? local.m_nview.GetZDO() : null;
        }
        var peer = ZNet.instance.GetPeer(sender);
        if (peer == null || !peer.IsReady() || peer.m_rpc == null || !peer.m_rpc.IsConnected() || peer.m_characterID.IsNone()) return null;
        var player = ZDOMan.instance.GetZDO(peer.m_characterID);
        if (player == null || player.GetOwner() != sender || player.GetLong(ZDOVars.s_playerID, 0) == 0
            || peer.m_playerID != 0 && peer.m_playerID != player.GetLong(ZDOVars.s_playerID, 0)) return null;
        var prefab = ZNetScene.instance.GetPrefab(player.GetPrefab());
        return prefab && prefab.GetComponent<Player>() ? player : null;
    }
    static bool Near(ZDO player, Vector3 position)
    { float square = (player.GetPosition() - position).sqrMagnitude; return !float.IsNaN(square) && !float.IsInfinity(square) && square <= 900 && !player.GetBool(ZDOVars.s_dead); }
    static IEnumerable<long> Recipients(long actor)
    {
        if (Character(ZDOMan.GetSessionID())?.GetLong(ZDOVars.s_playerID, 0) == actor) yield return ZDOMan.GetSessionID();
        foreach (var peer in ZNet.instance.GetConnectedPeers())
            if (Character(peer.m_uid)?.GetLong(ZDOVars.s_playerID, 0) == actor) yield return peer.m_uid;
    }
    static void Request(long sender, byte[] bytes)
    {
        if (!NativeBossAbandon.Authority || !EnsureServer()) return;
        bool persisting = false;
        try
        {
            if (bytes == null || bytes.Length > MaximumPacket) return;
            using var stream = new MemoryStream(bytes, false); using var reader = new BinaryReader(stream, Encoding.UTF8);
            long world = reader.ReadInt64(), actor = reader.ReadInt64(); string token = reader.ReadString();
            var id = new ZDOID(reader.ReadInt64(), reader.ReadUInt32());
            var character = Character(sender);
            if (stream.Position != stream.Length || world != serverWorld || !Canonical(token) || character == null
                || actor == 0 || character.GetLong(ZDOVars.s_playerID, 0) != actor) return;
            if (claims.TryGetValue(token, out var prior))
            {
                if (prior.Actor != actor || !Near(character, prior.Position)) { Notice(sender); return; }
                if (prior.State == InventoryDecision.Committed) DestroyCopies(token);
                SendGrant(sender, prior); return;
            }
            var drop = ZDOMan.instance.GetZDO(id);
            if (claims.Count >= MaximumClaims || drop == null || drop.GetString(NativeBossAbandon.RefundKey) != token
                || !Near(character, drop.GetPosition()) || !NativeSummonStore.RefundClaimReady(token)) { Notice(sender); return; }
            var offering = ReadOffering(drop, out string offeringPrefab);
            var claim = Claim.Decode(new Claim { World = world, Actor = actor, Token = token, Drop = id,
                Prefab = offeringPrefab, Amount = offering.m_stack, WorldLevel = offering.m_worldLevel, Position = drop.GetPosition() }.Encode());
            byte[] metadata = claim.Encode();
            persisting = true;
            using (var journal = new InventoryDecisionJournal(Path.Combine(ServerDirectory(world), Name(token) + ".wal"), 1, false))
            {
                if (journal.Snapshot().Count != 0) throw new InvalidDataException("Refund claim admission raced retained history.");
                var record = journal.Prepare(token, claim.Source, Hash(metadata), 0, Array.Empty<byte>(), metadata, metadata);
                journal.Transition(record, InventoryDecision.Decided); claim.State = record.State;
            }
            claims.Add(token, claim);
            SendGrant(sender, claim);
        }
        catch (Exception error) { if (persisting) serverFault = true; Warning(error); Notice(sender); }
    }
    static void SendGrant(long sender, Claim claim)
    {
        string key = sender.ToString(CultureInfo.InvariantCulture) + ":" + claim.Token;
        if (sent.TryGetValue(key, out float retry) && Time.unscaledTime < retry) return;
        sent[key] = Time.unscaledTime + 2;
        ZRoutedRpc.instance.InvokeRoutedRPC(sender, GrantRpc, new ZPackage(claim.Encode()));
    }
    static void Acknowledge(long sender, byte[] bytes)
    {
        if (!NativeBossAbandon.Authority || !EnsureServer()) return;
        bool persisting = false;
        try
        {
            var ack = Claim.Decode(bytes);
            var character = Character(sender);
            if (ack.World != serverWorld || !claims.TryGetValue(ack.Token, out var claim) || claim.Actor != ack.Actor
                || !claim.Encode().SequenceEqual(bytes) || character == null || character.GetLong(ZDOVars.s_playerID, 0) != claim.Actor
                || !Near(character, claim.Position)) return;
            persisting = true;
            using var journal = new InventoryDecisionJournal(Path.Combine(ServerDirectory(serverWorld), Name(claim.Token) + ".wal"), 1, true);
            var record = journal.Find(claim.Token) ?? throw new InvalidDataException("Refund claim disappeared.");
            VerifyClaim(record, serverWorld);
            if (!record.Metadata.SequenceEqual(bytes)) throw new InvalidDataException("Refund acknowledgment changed its claim.");
            if (record.State == InventoryDecision.Prepared) journal.Transition(record, InventoryDecision.Decided);
            journal.Transition(record, InventoryDecision.Committed); claim.State = record.State;
            DestroyCopies(claim.Token);
        }
        catch (Exception error) { if (persisting) serverFault = true; Warning(error); }
    }
    static void DestroyCopies(string token)
    {
        foreach (var zdo in ZDOMan.instance.m_objectsByID.Values.Where(z => z.GetString(NativeBossAbandon.RefundKey) == token).ToArray()) Destroy(zdo);
    }
    static void Destroy(ZDO zdo)
    {
        zdo.SetOwner(ZDOMan.GetSessionID());
        var live = ZNetScene.instance.FindInstance(zdo);
        if (live) ZNetScene.instance.Destroy(live.gameObject);
        ZDOMan.instance.DestroyZDO(zdo); ZDOMan.instance.SendDestroyed();
    }

    static void RecoverClient()
    {
        if (!NativeSummonReceipts.Refresh() || ReferenceEquals(scannedContext, NativeSummonReceipts.Context) || Time.unscaledTime < nextRecovery) return;
        nextRecovery = Time.unscaledTime + 2;
        var player = Player.m_localPlayer; long world = ZNet.instance.GetWorldUID(), actor = player.GetPlayerID();
        string directory = ClientDirectory(world, actor);
        if (!Directory.Exists(directory)) { scannedContext = NativeSummonReceipts.Context; return; }
        if (NativeSummonPayment.Held(player.GetInventory()) || NativeRaidChest.Held(player.GetInventory())
            || TheftTransfers.RawHeld(player.GetInventory()) || TheftInventoryGuards.ForeignHeld(player.GetInventory(), true)) return;
        Hold(player, world, "recovery");
        try
        {
            var files = Directory.EnumerateFiles(directory, "*.wal").Take(MaximumClaims + 1).ToArray();
            if (files.Length > MaximumClaims) throw new InvalidDataException("Refund grant recovery file bound.");
            foreach (string file in files)
            {
                using var journal = new InventoryDecisionJournal(file, 1, true);
                var record = journal.Snapshot().SingleOrDefault();
                if (record == null) continue;
                var claim = VerifyGrant(record, world, actor);
                if (Path.GetFileNameWithoutExtension(file) != Name(claim.Token)) throw new InvalidDataException("Refund grant recovery file identity.");
                Hold(player, world, claim.Source);
                if (!Finish(journal, record, claim, player)) return;
            }
            Release();
            scannedContext = NativeSummonReceipts.Context;
        }
        catch (Exception error) { Warning(error); }
    }
    static Claim VerifyGrant(InventoryDecisionRecord record, long world, long actor)
    {
        var claim = Claim.Decode(record.Metadata);
        if (claim.World != world || claim.Actor != actor || record.Id != claim.Token || record.Source != claim.Source
            || record.Fingerprint != Fingerprint(record.Metadata, record.Before, record.After)) throw new InvalidDataException("Refund grant journal identity.");
        return claim;
    }
    static bool Finish(InventoryDecisionJournal journal, InventoryDecisionRecord record, Claim claim, Player player)
    {
        if (record.State == InventoryDecision.Prepared) journal.Transition(record, InventoryDecision.Decided);
        if (record.State != InventoryDecision.Decided && record.State != InventoryDecision.Committed) return false;
        var store = new NativeSummonInventory(journal, claim.World, claim.Actor.ToString(CultureInfo.InvariantCulture), player, record);
        var participant = new DurableInventoryParticipant(journal, store, () => Time.realtimeSinceStartupAsDouble);
        return participant.Commit(record.Id) == InventoryDecision.Committed && store.ReceiptConfirmed(record.Source);
    }
    static string Fingerprint(byte[] metadata, byte[] before, byte[] after)
    { var packet = new ZPackage(); packet.Write(metadata); packet.Write(before); packet.Write(after); return Hash(packet.GetArray()); }
    static void Grant(long sender, byte[] bytes)
    {
        if (granting || ZRoutedRpc.instance == null || sender != ZRoutedRpc.instance.GetServerPeerID() || !NativeSummonReceipts.Refresh()) return;
        var player = Player.m_localPlayer; var inventory = player.GetInventory();
        Claim? incoming = null;
        string path = "";
        try
        {
            var claim = Claim.Decode(bytes);
            incoming = claim;
            if (claim.World != ZNet.instance.GetWorldUID() || claim.Actor != player.GetPlayerID() || !WildbloodInventory.Current(player, inventory)
                || player.IsDead() || player.IsTeleporting() || !Finite(player.transform.position - claim.Position)
                || (player.transform.position - claim.Position).sqrMagnitude > 900 || NativeSummonPayment.Held(inventory)
                || TheftTransfers.RawHeld(inventory) || NativeRaidChest.Held(inventory) || TheftInventoryGuards.ForeignHeld(inventory, true)
                || Held(inventory) && heldSource != claim.Source) return;
            granting = true;
            path = Path.Combine(ClientDirectory(claim.World, claim.Actor), Name(claim.Token) + ".wal");
            bool receipt = player.m_customData.ContainsKey(NativeTheftInventory.ReceiptKey(claim.Source));
            if (receipt && !File.Exists(path)) { Hold(player, claim.World, claim.Source); throw new InvalidDataException("Refund receipt has no grant journal."); }
            using var journal = new InventoryDecisionJournal(path, 1, receipt);
            var record = journal.Find(claim.Token);
            if (record == null)
            {
                if (journal.Snapshot().Count != 0 || !journal.CanAdmit || receipt) throw new InvalidDataException("Refund grant admission conflict.");
                var prefab = ZNetScene.instance.GetPrefab(claim.Prefab);
                var itemDrop = prefab ? prefab.GetComponent<ItemDrop>() : null;
                if (!itemDrop) return;
                NativeTheftInventory.SettleDurability(inventory);
                byte[] before = NativeTheftInventory.Bytes(inventory);
                var shadow = NativeTheftInventory.Read(before);
                var item = itemDrop.m_itemData.Clone(); item.m_dropPrefab = prefab; item.m_stack = claim.Amount; item.m_worldLevel = claim.WorldLevel;
                // Player.OnInventoryChanged sets this serialized flag before the applied image is checked.
                item.m_pickedUp = true;
                if (!AddComplete(player, inventory, shadow, item)) { NoRoom(); return; }
                if (!NativeTheftInventory.Bytes(inventory).SequenceEqual(before)) return;
                byte[] after = NativeTheftInventory.Bytes(shadow);
                Hold(player, claim.World, claim.Source);
                record = journal.Prepare(claim.Token, claim.Source, Fingerprint(bytes, before, after), 0, before, after, bytes);
            }
            Hold(player, claim.World, claim.Source);
            VerifyGrant(record, claim.World, claim.Actor);
            if (!record.Metadata.SequenceEqual(bytes)) throw new InvalidDataException("Refund grant conflicts with retained claim.");
            if (!Finish(journal, record, claim, player)) return;
            Release();
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.instance.GetServerPeerID(), AckRpc, new ZPackage(bytes));
        }
        catch (Exception error)
        {
            if (incoming != null && path.Length > 0)
                try { if (new FileInfo(path).Length > 0) Hold(player, incoming.World, incoming.Source); } catch { }
            Warning(error); LocalNotice();
        }
        finally { granting = false; }
    }
    static void Hold(Player player, long world, string source)
    { heldPlayer = player; heldInventory = player.GetInventory(); heldWorld = world; heldSource = source; scannedContext = null; }
    static void Release() { heldPlayer = null; heldInventory = null; heldWorld = 0; heldSource = ""; }
    static void Pickup(ItemDrop drop, Humanoid? user)
    {
        string token = Token(drop);
        if (token.Length == 0 || user != Player.m_localPlayer || !NativeSummonReceipts.Refresh()) return;
        Register();
        if (!Canonical(token) || !NativeRpcOwnership.OwnsRouted(ZRoutedRpc.instance, NativeRpcOwner.BossRefundDelivery)) { LocalNotice(); return; }
        string source = "boss-refund:" + ZNet.instance.GetWorldUID().ToString(CultureInfo.InvariantCulture) + ":"
            + Player.m_localPlayer.GetPlayerID().ToString(CultureInfo.InvariantCulture) + ":" + token;
        if (!NativeSummonReceipts.Has(source))
            try
            {
                var inventory = Player.m_localPlayer.GetInventory();
                if (NativeSummonPayment.Held(inventory) || NativeRaidChest.Held(inventory) || TheftTransfers.RawHeld(inventory)
                    || TheftInventoryGuards.ForeignHeld(inventory, true)) return;
                var item = ReadOffering(drop.m_nview.GetZDO(), out _);
                NativeTheftInventory.SettleDurability(inventory);
                byte[] before = NativeTheftInventory.Bytes(inventory);
                var shadow = NativeTheftInventory.Read(before);
                if (!AddComplete(Player.m_localPlayer, inventory, shadow, item)
                    || !NativeTheftInventory.Bytes(inventory).SequenceEqual(before)) { NoRoom(); return; }
            }
            catch (Exception error) { Warning(error); LocalNotice(); return; }
        foreach (string old in requested.Where(p => Time.unscaledTime - p.Value > 10).Select(p => p.Key).ToArray()) requested.Remove(old);
        if (requested.TryGetValue(token, out float previous) && Time.unscaledTime - previous < 1 || requested.Count >= 128) return;
        requested[token] = Time.unscaledTime;
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
        {
            writer.Write(ZNet.instance.GetWorldUID()); writer.Write(Player.m_localPlayer.GetPlayerID()); writer.Write(token);
            writer.Write(drop.m_nview.GetZDO().m_uid.UserID); writer.Write(drop.m_nview.GetZDO().m_uid.ID);
        }
        ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.instance.GetServerPeerID(), RequestRpc, new ZPackage(stream.ToArray()));
    }
    internal static void Reset()
    { claims.Clear(); sent.Clear(); requested.Clear(); serverNetwork = null; serverWorld = 0; serverFault = false; scannedContext = null; nextServer = 0; Release(); }

    [HarmonyPatch(typeof(ItemDrop), nameof(ItemDrop.CanPickup))]
    internal static class PickupGuard
    {
        static bool Prefix(ItemDrop __instance, ref bool __result)
        { if (Token(__instance).Length == 0) return true; __result = false; return false; }
    }
    [HarmonyPatch(typeof(ItemDrop), nameof(ItemDrop.Interact))]
    internal static class Interact
    {
        [HarmonyPriority(Priority.First)]
        static bool Prefix(ItemDrop __instance, object[] __args, ref bool __result)
        {
            if (Token(__instance).Length == 0) return true;
            Pickup(__instance, __args.OfType<Humanoid>().FirstOrDefault()); __result = false; return false;
        }
    }
    [HarmonyPatch]
    internal static class NativePickup
    {
        static IEnumerable<MethodBase> TargetMethods() => typeof(Humanoid).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Where(m => m.Name == "Pickup");
        [HarmonyPriority(Priority.First)]
        static bool Prefix(Humanoid __instance, object[] __args)
        {
            var drop = __args.OfType<GameObject>().Select(go => go ? go.GetComponent<ItemDrop>() : null).FirstOrDefault(item => item);
            if (!drop || Token(drop).Length == 0) return true;
            Pickup(drop, __instance); return false;
        }
    }
}
