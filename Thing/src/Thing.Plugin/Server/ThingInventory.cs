// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the character owner reserves the source; the server seals delivery before requesting removal.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Thing.Admin;
using ExtraPotions.Thing.Moderation;
using UnityEngine;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Thing.Runtime
{
    internal static class ThingInventory
    {
        internal const string OperationId = "meadworks.thing.inventory";
        internal const string OwnerId = "meadworks.thing.inventory_owner";
        internal const string Seal = "expDARE.Thing.Escrow";
        internal const string Distinct = "expDARE.Thing.Distinct";
        const string Receipt = "expDARE.Thing.EscrowReceipt";
        const int MaxBytes = 8192;
        static readonly TransferLedger ledger = new TransferLedger();
        static readonly HashSet<string> retiredSource = new HashSet<string>(StringComparer.Ordinal);
        static readonly Dictionary<string, Reservation> reserved = new Dictionary<string, Reservation>(StringComparer.Ordinal);
        static readonly Dictionary<string, Work> armedDeletes = new Dictionary<string, Work>(StringComparer.Ordinal);
        static readonly Dictionary<string, Work> recovering = new Dictionary<string, Work>(StringComparer.Ordinal);
        static IDisposable operation, owner;
        static InventoryEscrowJournal journal;
        static MeadworksLog log;
        static long generation = -1, recoveryWorld, retiredGeneration = -1;
        static float nextRecovery;
        static bool recoveryBlocked;

        internal static void Initialize(ProductRegistration registration, MeadworksLog logger)
        {
            log = logger;
            journal = new InventoryEscrowJournal(Path.Combine(ThingPaths.DataFolder, "inventory-escrow"));
            ThingInventoryPickup.Initialize();
            operation = CruciblePlugin.Services.Operations.Register(registration, new OperationDescriptor(OperationId, new System.Version(1, 0)), new Codec(), Apply);
            owner = CruciblePlugin.Services.PeerCommands.Register(registration, new OperationDescriptor(OwnerId, new System.Version(3, 0)), new Codec(), ApplyOwner);
            generation = CruciblePlugin.Session.Generation;
        }

        static readonly HashSet<string> retainedWarned = new HashSet<string>(StringComparer.Ordinal);
        const float SealScanSeconds = 60;
        static readonly Dictionary<string, float> sealAbsentAt = new Dictionary<string, float>(StringComparer.Ordinal);

        internal static void Tick()
        {
            ThingInventoryDelivery.Tick();
            var current = CruciblePlugin.IsReady ? CruciblePlugin.Session.Generation : -1;
            if (generation != current)
            {
                // Pending source removals survive here in the flushed journal; never guess a refund.
                ledger.Clear(); reserved.Clear(); armedDeletes.Clear(); recovering.Clear(); recoveryWorld = 0; retainedWarned.Clear(); sealAbsentAt.Clear();
                generation = current;
            }
            if (ZNet.instance == null || !ZNet.instance.IsServer() || !ThingServer.Active || !CruciblePlugin.Session.AuthorityReady) return;
            if (Time.unscaledTime < nextRecovery) return;
            nextRecovery = Time.unscaledTime + 2;
            var world = ZNet.instance.GetWorldUID();
            if (world != recoveryWorld)
            {
                try
                {
                    sealAbsentAt.Clear();
                    foreach (var record in journal.LoadAll(world))
                        recovering[record.Token] = Recovery(record);
                    recoveryWorld = world; recoveryBlocked = false;
                }
                catch (Exception error)
                {
                    recoveryBlocked = true;
                    log?.Warning("Thing inventory recovery refused: " + error.GetType().Name);
                    return;
                }
            }
            foreach (var work in recovering.Values.ToArray())
            {
                if (Time.unscaledTime < work.ResumeAt || work.Task != null && !work.Task.IsCompleted) continue;
                try { Recover(work); }
                catch (Exception error)
                {
                    if (retainedWarned.Add(work.Token + ":" + error.GetType().Name))
                        log?.Warning("Thing inventory escrow " + work.Token + " retained: " + error.GetType().Name);
                }
            }
        }

        internal static void Shutdown()
        {
            operation?.Dispose(); owner?.Dispose(); operation = owner = null;
            ledger.Clear(); reserved.Clear(); armedDeletes.Clear(); recovering.Clear(); recoveryWorld = 0; generation = -1; retainedWarned.Clear(); sealAbsentAt.Clear();
            // A sealed native pickup remains sealed across unload; its journal is required to release it.
            ThingInventoryDelivery.Shutdown(); ThingInventoryPickup.Shutdown(); log = null;
        }

        static OperationExecution<IReadOnlyList<string>> Apply(AuthorityContext context, IReadOnlyList<string> request)
        {
            var net = ZNet.instance;
            if (!Plugin.Ready || net == null || !net.IsServer() || !ThingServer.Active || !ThingModules.ProductActive(out _)
                || !CruciblePlugin.Session.AuthorityReady || context.Session.Generation != CruciblePlugin.Session.Generation)
                return Done("denied");
            if (!Caller(context, out var adminId, out var adminName, out var full)) return Done("denied");
            if (request.Count == 2 && request[0] == "claim") return Claim(context, adminId, request[1]);
            if (request.Count == 2 && request[0] == "release")
            {
                var armed = ledger.Find(request[1]);
                if (armed != null && armed.AdminId == adminId && armed.Phase == TransferPhase.DeleteArmed)
                { ledger.Fail(request[1]); armedDeletes.Remove(request[1]); }
                return Done("released");
            }
            if (request.Count < 2 || request[0] != "view" && request[0] != "take" && request[0] != "delete") return Done("denied");
            var action = request[0] == "view" ? ThingAction.InventoryView
                : request[0] == "take" ? ThingAction.InventoryTake : ThingAction.InventoryDelete;
            if (!full && !AdminPermissions.Covers(ThingAccess.Of(adminId), action)) return Done("denied");
            ledger.ReleaseIdle(adminId, request.Count == 4 && request[0] == "delete" ? request[3] : null);
            var platform = request[1];
            if (!GameListIdentity.Valid(platform) || platform == adminId) return Done("denied");
            var target = ThingServer.Online().FirstOrDefault(p => p.Actor.Id == platform);
            if (target.Peer == null) return Done("offline");
            var character = CharacterOf(target.Peer);
            if (character.Length == 0) return Done("missing");
            var admin = new Actor(adminId, adminName);
            if (request[0] == "view" && request.Count == 2)
            {
                context.MarkDecisionMade();
                return Poll(new Work { Kind = "view", Admin = admin, Target = target.Actor,
                    Task = Send(target.Peer.m_uid, "view-" + character + ":" + Guid.NewGuid().ToString("N"), new[] { "snapshot", character }) });
            }
            if (recoveryWorld != net.GetWorldUID()) return Done("busy");
            if (recoveryBlocked || recovering.Values.Any(w => (w.Record.State == EscrowState.CommitRequested || w.Record.State == EscrowState.Committed || w.Record.State == EscrowState.ClaimRequested || w.Record.State == EscrowState.DeleteRequested) && (w.Target.Id == platform || w.Admin.Id == adminId))) return Done("busy");
            if (!ItemSlot.TryParse(request.Count > 2 ? request[2] : "", out var slot) || !slot.HasIdentity) return Done("missing");
            if (request[0] == "delete" && request.Count == 4)
                return Confirm(context, admin, target.Actor, target.Peer.m_uid, character, slot, request[3]);
            if (request.Count != 3) return Done("denied");
            var token = Guid.NewGuid().ToString("N");
            if (request[0] == "delete")
            {
                if (ledger.BeginDelete(adminId, platform, slot, token) == null) return Done("busy");
                context.MarkDecisionMade();
                return Poll(new Work { Kind = "arm-delete", Token = token, Slot = slot, Admin = admin, Target = target.Actor,
                    TargetUid = target.Peer.m_uid, TargetCharacter = character,
                    Task = Send(target.Peer.m_uid, token + ":reserve-delete", new[] { "reserve", token, slot.Text, character }) });
            }
            if (!Receiver(adminId, out var adminUid, out var adminCharacter)) return Done("failed");
            if (ledger.BeginTake(adminId, platform, slot, token) == null) return Done("busy");
            context.MarkDecisionMade();
            return Poll(new Work
            {
                Kind = "take", Token = token, Slot = slot, Admin = admin, Target = target.Actor,
                AdminUid = adminUid, AdminCharacter = adminCharacter, TargetUid = target.Peer.m_uid, TargetCharacter = character,
                Task = Send(target.Peer.m_uid, token + ":reserve", new[] { "reserve", token, slot.Text, character }),
            });
        }

        static OperationExecution<IReadOnlyList<string>> Confirm(AuthorityContext context, Actor admin, Actor target, long targetUid, string character, ItemSlot slot, string token)
        {
            var move = ledger.Find(token);
            if (move == null || move.AdminId != admin.Id || move.TargetId != target.Id || !move.Delete || !move.Slot.Same(slot)) return Done("failed");
            if (recovering.TryGetValue(token, out var retry) && retry.Kind == "destroy")
            { context.MarkDecisionMade(); return PollDelete(retry); }
            if (!armedDeletes.TryGetValue(token, out var work) || work.TargetCharacter != character || !ledger.ConfirmDelete(token)) return Done("failed");
            context.MarkDecisionMade();
            work.Kind = "destroy"; work.TargetUid = targetUid;
            work.Record = new EscrowRecord(ZNet.instance.GetWorldUID(), token, admin.Id, target.Id, slot,
                work.Payload, "", EscrowState.DeleteRequested, work.SourceCharacter);
            try { journal.Save(work.Record); }
            catch { ledger.Fail(token); armedDeletes.Remove(token); return Done("failed"); }
            armedDeletes.Remove(token); recovering[token] = work;
            work.Task = Delete(work); work.ResumeAt = Time.unscaledTime + 15;
            return PollDelete(work);
        }

        static OperationExecution<IReadOnlyList<string>> Poll(Work work)
        {
            if (work.Task != null && !work.Task.IsCompleted) return OperationExecution<IReadOnlyList<string>>.Pending(() => Poll(work));
            if (generation != CruciblePlugin.Session.Generation) return Finish(work, "failed", AuditOutcome.Failed, "session changed; escrow retained");
            if (work.Kind == "take" && work.Record != null && work.Record.State >= EscrowState.Released && work.Record.State != EscrowState.Cancelled) return Done("dropped");
            if (work.Kind == "claim") return PollClaim(work);
            if (work.Kind == "destroy") return PollDelete(work);
            var reply = ReplyOf(work.Task);
            if (work.Kind == "view")
            {
                if (reply == null || reply.Count == 0 || reply[0] != "items" && reply[0] != "empty") return Finish(work, "failed", AuditOutcome.Failed, "view");
                ThingServer.Audit(work.Admin, ThingAction.InventoryView, work.Target, AuditOutcome.Done, "view");
                return OperationExecution<IReadOnlyList<string>>.Success(reply);
            }
            if (work.Kind == "take" && work.Step == 0)
            {
                if (reply == null || reply.Count != 4 || reply[0] != "reserved" || reply[1] != work.Token
                    || !long.TryParse(reply[3], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var source) || source == 0
                    || !ValidatePayload(reply[2], work.Slot, out var item))
                {
                    ledger.Fail(work.Token);
                    return Finish(work, "missing", AuditOutcome.Failed, "reservation refused; source untouched");
                }
                work.Payload = reply[2]; work.SourceCharacter = reply[3];
                ItemDrop staged = null;
                try
                {
                    var sourceZdo = ZDOMan.instance.GetZDO(ZNet.instance.GetPeers().First(p => p.m_uid == work.TargetUid).m_characterID);
                    if (sourceZdo == null || sourceZdo.GetLong(ZDOVars.s_playerID) != source) throw new InvalidOperationException("Source character changed");
                    if (!Receiver(work.Admin.Id, out _, out var receiver) || receiver != work.AdminCharacter) throw new InvalidOperationException("Receiver changed");
                    work.Record = new EscrowRecord(ZNet.instance.GetWorldUID(), work.Token, work.Admin.Id, work.Target.Id, work.Slot,
                        work.Payload, "", EscrowState.Prepared, work.SourceCharacter);
                    journal.Save(work.Record);
                    staged = Stage(item, work.AdminUid, work.Token);
                    Persist(work, EscrowState.CommitRequested, staged.GetComponent<ZNetView>().GetZDO().m_uid.ToString());
                }
                catch (Exception error)
                {
                    if (staged != null) DestroyPickup(staged.GetComponent<ZNetView>()?.GetZDO());
                    if (work.Record != null) { work.Record = Cancel(work.Record); recovering[work.Token] = work; }
                    ledger.Fail(work.Token);
                    return Finish(work, "failed", AuditOutcome.Failed, "delivery escrow refused: " + error.GetType().Name + "; source untouched");
                }
                work.Step = 1;
                work.ResumeAt = Time.unscaledTime + 15;
                recovering[work.Token] = work;
                work.Task = Commit(work);
                return Poll(work);
            }
            if (work.Kind == "take" && work.Step == 1)
            {
                if (Accepted(work, reply))
                {
                    Persist(work, EscrowState.Committed);
                    Release(work);
                    return Finish(work, "dropped", AuditOutcome.Done, Detail(work.Slot, "server pickup"));
                }
                if (Unchanged(work, reply))
                {
                    work.Record = Cancel(work.Record); work.Task = null; work.ResumeAt = 0; ledger.Fail(work.Token);
                    return Finish(work, "missing", AuditOutcome.Failed, "source unchanged; escrow cancelled");
                }
                // No restore request is sent to either client. A timeout cannot manufacture a second copy.
                work.Task = null; work.ResumeAt = 0;
                return Finish(work, "busy", AuditOutcome.Failed, "source decision pending; sealed delivery retained " + work.Token);
            }
            if (work.Kind == "arm-delete")
            {
                if (reply == null || reply.Count != 4 || reply[0] != "reserved" || reply[1] != work.Token
                    || !long.TryParse(reply[3], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var source) || source == 0
                    || !ValidatePayload(reply[2], work.Slot, out _))
                { ledger.Fail(work.Token); return Done("missing"); }
                work.Payload = reply[2]; work.SourceCharacter = reply[3]; work.Task = null;
                foreach (var token in armedDeletes.Keys.Where(t => ledger.Find(t)?.Phase != TransferPhase.DeleteArmed).ToArray()) armedDeletes.Remove(token);
                armedDeletes[work.Token] = work;
                return Done("armed", work.Token);
            }
            return Finish(work, "failed", AuditOutcome.Failed, "unhandled");
        }

        static OperationExecution<IReadOnlyList<string>> Finish(Work work, string outcome, AuditOutcome audit, string detail)
        {
            if (work.Kind != "view")
            {
                ThingServer.Audit(work.Admin, work.Kind == "destroy" ? ThingAction.InventoryDelete : ThingAction.InventoryTake, work.Target, audit, detail);
                log?.Info("Thing inventory " + work.Kind + " by " + work.Admin.Id + " on " + work.Target.Id + " " + detail + " -> " + outcome + ".");
            }
            return Done(outcome);
        }

        static OperationExecution<IReadOnlyList<string>> ApplyOwner(PeerCommandContext context, IReadOnlyList<string> request)
        {
            var session = CruciblePlugin.Session.Generation;
            if (retiredGeneration != session) { retiredSource.Clear(); retiredGeneration = session; }
            if (!Plugin.Ready || context.Session.Generation != generation || (!context.IsLocal && !context.ServerConnectionVerified) || request.Count < 2) return Done("missing");
            if (request[0] == "snapshot" && request.Count == 2 && LocalPlayer(request[1], out var viewer))
                return OperationExecution<IReadOnlyList<string>>.Success(Snapshot(viewer.GetInventory()));
            if ((request[0] == "forget" || request[0] == "forget-delete") && request.Count == 5 && LocalPlayer(request[3], out var finished, true))
            {
                if (finished.GetPlayerID().ToString(CultureInfo.InvariantCulture) != request[4] || !ValidToken(request[1])) return Done("pending");
                var expected = (request[0] == "forget-delete" ? "delete:" : "") + ReceiptText(request[1], request[2], request[4]);
                if (finished.m_customData.TryGetValue(Receipt, out var receipt) && (receipt == expected || receipt == "unchanged:" + expected))
                {
                    finished.m_customData.Remove(Receipt);
                    if (!SaveCharacter(finished)) { finished.m_customData[Receipt] = receipt; return Done("pending"); }
                }
                retiredSource.Add(request[1]); reserved.Remove(request[1]); return Done("forgotten", request[1]);
            }
            if (request[0] == "deliver" && request.Count == 7 && LocalPlayer(request[3], out var receiver, true)
                && ItemSlot.TryParse(request[6], out var receiveSlot) && ValidToken(request[1]))
            {
                var outcome = ThingInventoryDelivery.Receive(receiver, request[1], request[2], request[4], request[5], receiveSlot);
                return Done(outcome, request[1], request[4], request[5]);
            }
            if (request[0] == "forget-delivery" && request.Count == 5 && LocalPlayer(request[3], out var delivered, true))
                return Done(ThingInventoryDelivery.Forget(delivered, request[1], request[2], request[4]), request[1]);
            if (request[0] == "reserve" && request.Count == 4 && LocalPlayer(request[3], out var owner))
            {
                if (!ItemSlot.TryParse(request[2], out var slot) || !ValidToken(request[1])) return Done("missing");
                var bytes = Reserve(owner, request[1], slot);
                return bytes.Length == 0 ? Done("missing") : Done("reserved", request[1], bytes, owner.GetPlayerID().ToString(CultureInfo.InvariantCulture));
            }
            if (request[0] == "destroy" && request.Count == 6 && LocalPlayer(request[3], out var deleting, true)
                && ItemSlot.TryParse(request[2], out var deleteSlot) && deleteSlot.HasIdentity && ValidToken(request[1]))
                return Destroy(deleting, request[1], deleteSlot, request[4], request[5]);
            if (request[0] == "commit" && request.Count == 6 && LocalPlayer(request[3], out var sourcePlayer, true)
                && ItemSlot.TryParse(request[2], out var sourceSlot) && ValidToken(request[1]))
                return CommitOwner(sourcePlayer, request[1], sourceSlot, request[4], request[5]);
            return Done("missing");
        }

        static IReadOnlyList<string> Snapshot(Inventory inventory)
        {
            var lines = new List<string> { "items" };
            if (inventory == null) return new[] { "empty" };
            foreach (var item in inventory.GetAllItems())
            {
                if (item?.m_shared == null || item.m_dropPrefab == null) continue;
                if (lines.Count > 48) break;
                var slot = SlotOf(item);
                if (ItemSlot.TryParse(slot.Text, out _)) lines.Add(slot.Text);
            }
            return lines.Count == 1 ? new[] { "empty" } : lines;
        }

        static string Reserve(Player player, string token, ItemSlot slot)
        {
            if (!ValidToken(token) || retiredSource.Contains(token) || retiredSource.Count >= InventoryEscrowJournal.MaxRecords || player.m_customData.ContainsKey(Receipt)) return "";
            var item = Find(player.GetInventory(), slot);
            if (item == null) return "";
            var bytes = WriteItem(item);
            if (reserved.TryGetValue(token, out var prior)) return ReferenceEquals(prior.Item, item) && prior.Payload == bytes ? bytes : "";
            // The real stack stays in the character. A later mutation invalidates this exact-image reservation.
            if (reserved.Count >= TransferLedger.MaxActive) reserved.Clear();
            reserved[token] = new Reservation { Item = item, Payload = bytes };
            return bytes;
        }

        static OperationExecution<IReadOnlyList<string>> CommitOwner(Player player, string token, ItemSlot slot, string digest, string character)
        {
            if (retiredSource.Contains(token) || player.GetPlayerID().ToString(CultureInfo.InvariantCulture) != character) return Done("missing");
            var receipt = ReceiptText(token, digest, character);
            if (player.m_customData.TryGetValue(Receipt, out var prior))
            {
                if ((prior == receipt || prior == "unchanged:" + receipt) && SaveCharacter(player))
                    return Done(prior == receipt ? "committed" : "unchanged", token, digest, character);
                return Done("pending");
            }
            var inventory = player.GetInventory();
            var item = Find(inventory, slot);
            if (item == null || Digest(WriteItem(item)) != digest) { return RejectSource(player, token, digest, character); }
            if (reserved.TryGetValue(token, out var held) && (!ReferenceEquals(item, held.Item) || held.Payload != WriteItem(item)))
            { return RejectSource(player, token, digest, character); }
            // The receipt and inventory are saved as one character image. Retry never removes another stack.
            if (item.m_equipped) player.UnequipItem(item);
            player.m_customData[Receipt] = receipt;
            try { inventory.RemoveItem(item); }
            catch
            {
                if (inventory.ContainsItem(item)) { player.m_customData.Remove(Receipt); return RejectSource(player, token, digest, character); }
                return Done("pending");
            }
            if (inventory.ContainsItem(item)) { player.m_customData.Remove(Receipt); return RejectSource(player, token, digest, character); }
            reserved.Remove(token);
            return SaveCharacter(player) ? Done("committed", token, digest, character) : Done("pending");
        }

        static OperationExecution<IReadOnlyList<string>> RejectSource(Player player, string token, string digest, string character)
        {
            // A rejected decision must survive a lost reply just as a committed decision does.
            player.m_customData[Receipt] = "unchanged:" + ReceiptText(token, digest, character);
            return SaveCharacter(player) ? Done("unchanged", token, digest, character) : Done("pending");
        }

        static bool SaveCharacter(Player player)
        {
            try
            {
                var profile = Game.instance?.GetPlayerProfile();
                if (profile == null || Player.m_localPlayer != player || SaveSystem.HasSessionFlag(SaveSystemSessionFlags.DontSaveCharacter)) return false;
                profile.SavePlayerData(player);
                return profile.Save();
            }
            catch { return false; }
        }

        static string ReceiptText(string token, string digest, string character) => ZNet.instance.GetWorldUID().ToString(CultureInfo.InvariantCulture) + ":" + character + ":" + token + ":" + digest;
        static OperationExecution<IReadOnlyList<string>> Destroy(Player player, string token, ItemSlot slot, string digest, string character)
        {
            if (retiredSource.Contains(token) || player.GetPlayerID().ToString(CultureInfo.InvariantCulture) != character || slot.ImageDigest != digest) return Done("pending");
            var owner = new DeleteOwner(player, token, slot, digest);
            var result = DurableInventoryDelete.Apply(owner, "delete:" + ReceiptText(token, digest, character));
            if (result == InventoryDeleteResult.Pending) return Done("pending");
            reserved.Remove(token);
            return Done(result == InventoryDeleteResult.Deleted ? "destroyed" : "unchanged", token, digest, character);
        }

        sealed class DeleteOwner : IInventoryDeleteOwner
        {
            readonly Player player;
            readonly string token, digest;
            readonly ItemSlot slot;
            ItemDrop.ItemData item;
            string preparedPayload;
            internal DeleteOwner(Player player, string token, ItemSlot slot, string digest)
            { this.player = player; this.token = token; this.slot = slot; this.digest = digest; }
            public string Receipt
            {
                get => player.m_customData.TryGetValue(ThingInventory.Receipt, out var receipt) ? receipt : "";
                set => player.m_customData[ThingInventory.Receipt] = value;
            }
            public bool MatchesReservation()
            {
                if (preparedPayload != null)
                    return player.GetInventory().ContainsItem(item) && reserved.TryGetValue(token, out var prepared)
                        && ReferenceEquals(item, prepared.Item) && Digest(prepared.Payload) == digest && item.m_durability == slot.Durability && WriteItem(item) == preparedPayload;
                item = Find(player.GetInventory(), slot);
                return item != null && reserved.TryGetValue(token, out var held) && ReferenceEquals(item, held.Item)
                    && held.Payload == WriteItem(item) && Digest(held.Payload) == digest;
            }
            public void Prepare()
            {
                var original = item.Clone();
                if (item.m_equipped) player.UnequipItem(item);
                var after = item.Clone(); after.m_equipped = original.m_equipped;
                if (item.m_durability != original.m_durability || WriteItem(after) != WriteItem(original)) throw new InvalidOperationException("Reserved item changed during unequip.");
                preparedPayload = WriteItem(item);
            }
            public void Remove()
            {
                var inventory = player.GetInventory();
                inventory.GetAllItems().Remove(item);
                inventory.Changed();
            }
            public bool ContainsReservedItem() => player.GetInventory().ContainsItem(item);
            public bool Save() => SaveCharacter(player);
        }

        static ItemSlot SlotOf(ItemDrop.ItemData item, bool identity = true) => new ItemSlot(item.m_dropPrefab?.name ?? "", item.m_stack, item.m_quality,
            item.m_gridPos.x, item.m_gridPos.y, item.m_crafterID, item.m_variant, identity ? item.m_durability : -1, identity ? Digest(WriteItem(item)) : "");
        static ItemDrop.ItemData Find(Inventory inventory, ItemSlot slot) => inventory?.GetAllItems().FirstOrDefault(item => item?.m_dropPrefab != null && slot.Matches(SlotOf(item, slot.HasIdentity)));
        static string WriteItem(ItemDrop.ItemData item)
        {
            var package = new ZPackage(); package.Write(item.m_dropPrefab.name); item.Save(package);
            var bytes = package.GetArray();
            return bytes.Length > 0 && bytes.Length <= MaxBytes ? Convert.ToBase64String(bytes) : "";
        }
        internal static bool ValidatePayload(string encoded, ItemSlot slot, out ItemDrop.ItemData item)
        {
            item = null;
            try
            {
                var bytes = Convert.FromBase64String(encoded);
                if (bytes.Length == 0 || bytes.Length > MaxBytes) return false;
                var package = new ZPackage(bytes);
                var prefab = package.ReadString();
                if (prefab != slot.Prefab) return false;
                var prefabObject = ObjectDB.instance?.GetItemPrefab(prefab);
                var drop = prefabObject != null ? prefabObject.GetComponent<ItemDrop>() : null;
                if (drop?.m_itemData?.m_shared == null) return false;
                var data = drop.m_itemData.Clone();
                var hash = ItemDrop.ItemData.Load(package, data, global::Version.Item.ChunksNCheats);
                if (hash != prefab.GetStableHashCode() || package.GetPos() != bytes.Length) return false;
                data.m_dropPrefab = prefabObject;
                if (!slot.MatchesPayload(SlotOf(data, false), Digest(encoded)) || data.m_stack > data.m_shared.m_maxStackSize || data.m_quality > Math.Max(1, data.m_shared.m_maxQuality)
                    || float.IsNaN(data.m_durability) || float.IsInfinity(data.m_durability) || data.m_durability < 0) return false;
                if (data.m_customData == null) data.m_customData = new Dictionary<string, string>();
                // Old releases left this transport-only field on items. It is never propagated to the delivered stack.
                data.m_customData.Remove("expDARE.Thing.Transfer");
                data.m_equipped = false;
                item = data; return true;
            }
            catch { return false; }
        }
        internal static string Digest(string payload)
        {
            using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(Convert.FromBase64String(payload))).Replace("-", "").ToLowerInvariant();
        }
        static bool ValidToken(string token) => token != null && token.Length == 32 && token.All(c => c >= '0' && c <= '9' || c >= 'a' && c <= 'f');
        static string Detail(ItemSlot slot, string outcome) => slot.Prefab + " x" + slot.Stack.ToString(CultureInfo.InvariantCulture) + " q" + slot.Quality.ToString(CultureInfo.InvariantCulture) + " " + outcome;
        static bool LocalPlayer(string character, out Player player, bool allowDead = false)
        {
            player = Player.m_localPlayer;
            if (!player || !allowDead && player.IsDead()) return false;
            var view = player.GetComponent<ZNetView>();
            var zdo = view != null && view.IsValid() && view.IsOwner() ? view.GetZDO() : null;
            return zdo != null && zdo.m_uid.ToString() == character;
        }
        static string CharacterOf(ZNetPeer peer)
        {
            var zdo = ZDOMan.instance?.GetZDO(peer.m_characterID);
            return zdo == null || zdo.m_uid.IsNone() ? "" : zdo.m_uid.ToString();
        }
        static bool Receiver(string adminId, out long uid, out string character)
        {
            uid = 0; character = "";
            if (adminId == "host" || adminId == "server")
            {
                var player = Player.m_localPlayer;
                var view = player != null ? player.GetComponent<ZNetView>() : null;
                var zdo = view != null && view.IsValid() ? view.GetZDO() : null;
                if (zdo == null || zdo.m_uid.IsNone() || player.IsDead()) return false;
                uid = ZNet.GetUID(); character = zdo.m_uid.ToString(); return uid != 0;
            }
            var peer = ThingServer.Online().FirstOrDefault(p => p.Actor.Id == adminId).Peer;
            if (peer == null) return false;
            character = CharacterOf(peer); uid = peer.m_uid; return character.Length > 0 && uid != 0;
        }

        static ItemDrop Stage(ItemDrop.ItemData item, long receiver, string token)
        {
            sealAbsentAt.Remove(token ?? "");
            var character = ReceiverZdo(receiver);
            if (character == null) throw new InvalidOperationException("Receiver unavailable");
            ItemDrop drop = null;
            GameObject created = null;
            try
            {
                // Capture the object before invoking drop callbacks, which can throw after spawning it.
                created = UnityEngine.Object.Instantiate(item.m_dropPrefab, character.GetPosition() + Vector3.up, Quaternion.identity);
                drop = created.GetComponent<ItemDrop>();
                if (drop == null) throw new InvalidOperationException("Pickup not created");
                var view = drop.GetComponent<ZNetView>();
                if (view == null || !view.IsValid()) throw new InvalidOperationException("Pickup has no network identity");
                view.GetZDO().Set(Seal, token); view.GetZDO().Set(Distinct, true);
                view.GetZDO().SetOwner(ZNet.GetUID());
                if (!view.IsOwner()) throw new InvalidOperationException("Pickup is not server owned");
                drop.m_itemData = item.Clone();
                if (item.m_quality > 1) drop.SetQuality(item.m_quality);
                drop.m_onDrop?.Invoke(drop);
                if (WriteItem(drop.m_itemData) != WriteItem(item)) throw new InvalidOperationException("Drop callback changed the reserved item");
                ItemDrop.SaveToZDO(drop.m_itemData, view.GetZDO());
                ThingInventoryPickup.Protect(drop);
                return drop;
            }
            catch
            {
                if (drop != null)
                {
                    var view = drop.GetComponent<ZNetView>();
                    if (view != null && view.IsValid()) DestroyPickup(view.GetZDO());
                    else UnityEngine.Object.Destroy(drop.gameObject);
                }
                else if (created != null) UnityEngine.Object.Destroy(created);
                throw;
            }
        }
        static ZDO ReceiverZdo(long uid) => uid == ZNet.GetUID() ? Player.m_localPlayer?.GetComponent<ZNetView>()?.GetZDO()
            : ZDOMan.instance.GetZDO(ZNet.instance.GetPeers().FirstOrDefault(p => p.m_uid == uid)?.m_characterID ?? ZDOID.None);
        static ZDO Pickup(EscrowRecord record)
        {
            var parts = (record.Pickup ?? "").Split(':');
            if (parts.Length == 2 && long.TryParse(parts[0], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var user)
                && uint.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var id))
            {
                var found = ZDOMan.instance?.GetZDO(new ZDOID(user, id));
                if (found != null) return found;
            }
            // A crash between native creation and journal binding can leave only the sealed token.
            var token = record.Token ?? "";
            if (sealAbsentAt.TryGetValue(token, out var scanned) && Time.unscaledTime >= scanned && Time.unscaledTime - scanned < SealScanSeconds) return null;
            var zdos = ZDOMan.instance;
            if (zdos == null) return null;
            string available = "available:" + token;
            foreach (var z in zdos.m_objectsByID.Values)
            {
                var seal = z.GetString(Seal);
                if (seal.Length != 0 && (seal == token || seal == available)) { sealAbsentAt.Remove(token); return z; }
            }
            if (sealAbsentAt.Count >= 1024) sealAbsentAt.Clear();
            sealAbsentAt[token] = Time.unscaledTime;
            return null;
        }
        static void DestroyPickup(ZDO zdo)
        {
            if (zdo == null) return;
            var instance = ZNetScene.instance?.FindInstance(zdo.m_uid);
            if (instance != null) ZNetScene.instance.Destroy(instance);
            else ZDOMan.instance.DestroyZDO(zdo);
        }
        static EscrowRecord Change(EscrowRecord record, EscrowState state, string pickup = null, string receiverCharacter = null) => new EscrowRecord(record.World, record.Token,
            record.AdminId, record.TargetId, record.Slot, record.Payload, pickup ?? record.Pickup, state, record.SourceCharacter, receiverCharacter ?? record.ReceiverCharacter);
        static void Persist(Work work, EscrowState state, string pickup = null, string receiverCharacter = null)
        {
            var next = Change(work.Record, state, pickup, receiverCharacter);
            journal.Save(next); work.Record = next;
        }
        static EscrowRecord Cancel(EscrowRecord record)
        {
            DestroyPickup(Pickup(record));
            var cancelled = Change(record, EscrowState.Cancelled);
            journal.Save(cancelled); return cancelled;
        }
        static Task<OperationResult<IReadOnlyList<string>>> Commit(Work work) => Send(work.TargetUid, work.Token + ":commit:" + Guid.NewGuid().ToString("N"),
            new[] { "commit", work.Token, work.Slot.Text, work.TargetCharacter, Digest(work.Payload), work.SourceCharacter });
        static bool Accepted(Work work, IReadOnlyList<string> reply) => Matches(work, reply, "committed", work.SourceCharacter);
        static bool Unchanged(Work work, IReadOnlyList<string> reply) => Matches(work, reply, "unchanged", work.SourceCharacter);
        static bool Matches(Work work, IReadOnlyList<string> reply, string outcome, string character) => reply != null && reply.Count == 4
            && reply[0] == outcome && reply[1] == work.Token && reply[2] == Digest(work.Payload) && reply[3] == character;
        static void EnsurePickup(Work work)
        {
            var zdo = Pickup(work.Record);
            if (zdo != null) return;
            if (!Receiver(work.Admin.Id, out var uid, out _) || !ValidatePayload(work.Payload, work.Slot, out var item))
                throw new InvalidOperationException("Receiver or escrow image unavailable; recovery retained");
            var drop = Stage(item, uid, work.Token);
            Persist(work, work.Record.State, drop.GetComponent<ZNetView>().GetZDO().m_uid.ToString());
        }
        static void Release(Work work)
        {
            EnsurePickup(work);
            if (work.Record.State != EscrowState.Released) Persist(work, EscrowState.Released);
            // Native pickup remains blocked. Claim is a durable character transaction, including after world rollback.
            Pickup(work.Record).Set(Seal, "available:" + work.Token);
            if (ledger.Find(work.Token)?.Phase == TransferPhase.Armed) ledger.Removed(work.Token);
            ledger.DeliveredTo(work.Token, TransferWhere.Feet);
            work.Step = 2; work.Task = null; work.ResumeAt = 0;
        }
        static OperationExecution<IReadOnlyList<string>> Claim(AuthorityContext context, string caller, string token)
        {
            if (!ValidToken(token) || !recovering.TryGetValue(token, out var work) || work.Record.AdminId != caller) return Done("denied");
            if (work.Record.State == EscrowState.Claimed) return Done("stored");
            if (work.Record.State != EscrowState.Released && work.Record.State != EscrowState.ClaimRequested) return Done("busy");
            if (!Receiver(caller, out var uid, out var character)) return Done("offline");
            var receiver = ReceiverZdo(uid); var pickup = Pickup(work.Record);
            if (pickup == null || receiver == null || Vector3.Distance(receiver.GetPosition(), pickup.GetPosition()) > 5f) return Done("denied");
            var persistent = receiver.GetLong(ZDOVars.s_playerID).ToString(CultureInfo.InvariantCulture);
            if (persistent == "0" || work.Record.ReceiverCharacter.Length > 0 && persistent != work.Record.ReceiverCharacter) return Done("denied");
            context.MarkDecisionMade();
            if (work.Record.State == EscrowState.Released) Persist(work, EscrowState.ClaimRequested, receiverCharacter: persistent);
            if (work.Kind == "claim" && work.Task != null && !work.Task.IsCompleted) return Done("busy");
            work.Kind = "claim"; work.AdminUid = uid; work.AdminCharacter = character; work.ResumeAt = Time.unscaledTime + 15;
            work.Task = Deliver(work);
            return PollClaim(work);
        }
        static Task<OperationResult<IReadOnlyList<string>>> Deliver(Work work) => Send(work.AdminUid, work.Token + ":deliver:" + Guid.NewGuid().ToString("N"),
            new[] { "deliver", work.Token, work.Payload, work.AdminCharacter, Digest(work.Payload), work.Record.ReceiverCharacter, work.Slot.Text });
        static OperationExecution<IReadOnlyList<string>> PollClaim(Work work)
        {
            if (work.Record.State == EscrowState.Claimed) return Done("stored");
            if (work.Task != null && !work.Task.IsCompleted) return OperationExecution<IReadOnlyList<string>>.Pending(() => PollClaim(work));
            if (Matches(work, ReplyOf(work.Task), "stored", work.Record.ReceiverCharacter))
            {
                Persist(work, EscrowState.Claimed);
                DestroyPickup(Pickup(work.Record));
                ThingServer.Audit(work.Admin, ThingAction.InventoryTake, work.Target, AuditOutcome.Done, Detail(work.Slot, "saved distinct receiver stack"));
                work.Step = 3; work.Task = null; work.ResumeAt = 0;
                return Done("stored");
            }
            work.Task = null; work.ResumeAt = 0;
            return Done("busy");
        }
        static Work Recovery(EscrowRecord record) => new Work { Kind = record.State >= EscrowState.DeleteRequested ? "destroy" : "take", Token = record.Token, Slot = record.Slot, Payload = record.Payload,
            SourceCharacter = record.SourceCharacter, Record = record, Admin = new Actor(record.AdminId, record.AdminId), Target = new Actor(record.TargetId, record.TargetId), Step = 1 };
        static Task<OperationResult<IReadOnlyList<string>>> Delete(Work work) => Send(work.TargetUid, work.Token + ":destroy:" + Guid.NewGuid().ToString("N"),
            new[] { "destroy", work.Token, work.Slot.Text, work.TargetCharacter, Digest(work.Payload), work.SourceCharacter });

        static OperationExecution<IReadOnlyList<string>> PollDelete(Work work)
        {
            if (work.Record.State == EscrowState.Deleted) return Done("deleted");
            if (work.Record.State == EscrowState.DeleteCancelled) return Done("missing");
            if (generation != CruciblePlugin.Session.Generation) return Done("busy");
            if (work.Task != null && !work.Task.IsCompleted) return OperationExecution<IReadOnlyList<string>>.Pending(() => PollDelete(work));
            if (work.Step == 2) return Done("busy");
            var reply = ReplyOf(work.Task);
            work.Task = null; work.ResumeAt = 0;
            if (Matches(work, reply, "destroyed", work.SourceCharacter))
            {
                Persist(work, EscrowState.Deleted);
                ledger.DeletedFrom(work.Token);
                return Finish(work, "deleted", AuditOutcome.Done, Detail(work.Slot, "deleted and saved by character owner"));
            }
            if (Matches(work, reply, "unchanged", work.SourceCharacter))
            {
                Persist(work, EscrowState.DeleteCancelled);
                ledger.Fail(work.Token);
                return Finish(work, "missing", AuditOutcome.Failed, Detail(work.Slot, "source unchanged; deletion cancelled"));
            }
            // An unknown reply is not evidence of failure: keep the saved request and retry its token.
            return Done("busy");
        }

        static void RecoverDelete(Work work)
        {
            if (work.Record.State != EscrowState.DeleteRequested)
            {
                ForgetSource(work);
                if (work.SourceForgotten) recovering.Remove(work.Token);
                return;
            }
            if (work.Task != null)
            {
                if (work.Step == 2)
                {
                    work.Step = 0; work.Task = Delete(work); work.ResumeAt = Time.unscaledTime + 15;
                    return;
                }
                PollDelete(work);
                if (work.Record.State != EscrowState.DeleteRequested) return;
            }
            var target = ThingServer.Online().FirstOrDefault(p => p.Actor.Id == work.Target.Id).Peer;
            if (target == null || ZDOMan.instance?.GetZDO(target.m_characterID)?.GetLong(ZDOVars.s_playerID).ToString(CultureInfo.InvariantCulture) != work.SourceCharacter) return;
            work.TargetUid = target.m_uid; work.TargetCharacter = CharacterOf(target);
            if (work.TargetCharacter.Length == 0) return;
            // Re-establish the exact reservation after a character/session reload. A saved decision wins over reserve failure.
            work.Step = 2; work.ResumeAt = Time.unscaledTime + 15;
            work.Task = Send(work.TargetUid, work.Token + ":reserve-delete:" + Guid.NewGuid().ToString("N"),
                new[] { "reserve", work.Token, work.Slot.Text, work.TargetCharacter });
        }

        static void Recover(Work work)
        {
            if (work.Kind == "destroy") { RecoverDelete(work); return; }
            if (work.Record.State == EscrowState.Prepared) work.Record = Cancel(work.Record);
            if (work.Record.State == EscrowState.Cancelled)
            {
                DestroyPickup(Pickup(work.Record)); ForgetSource(work);
                if (work.SourceForgotten) recovering.Remove(work.Token);
                return;
            }
            if (work.Record.State == EscrowState.Committed) { Release(work); return; }
            if (work.Record.State == EscrowState.Released)
            {
                EnsurePickup(work); Pickup(work.Record).Set(Seal, "available:" + work.Token);
                ForgetSource(work); return;
            }
            if (work.Record.State == EscrowState.ClaimRequested)
            {
                if (work.Task != null) { PollClaim(work); if (work.Record.State == EscrowState.Claimed) return; }
                if (!Receiver(work.Admin.Id, out var uid, out var character) || ReceiverZdo(uid)?.GetLong(ZDOVars.s_playerID).ToString(CultureInfo.InvariantCulture) != work.Record.ReceiverCharacter) return;
                work.Kind = "claim"; work.AdminUid = uid; work.AdminCharacter = character;
                work.Task = Deliver(work); return;
            }
            if (work.Record.State == EscrowState.Claimed)
            {
                DestroyPickup(Pickup(work.Record));
                ForgetSource(work);
                if (!work.ReceiverForgotten)
                {
                    if (work.CleanupTask != null && !work.CleanupTask.IsCompleted) return;
                    if (Forgotten(work.CleanupTask, work.Token)) { work.ReceiverForgotten = true; work.CleanupTask = null; }
                    else if (Receiver(work.Admin.Id, out var uid, out var character)) work.CleanupTask = Send(uid, work.Token + ":forget-delivery:" + Guid.NewGuid().ToString("N"),
                        new[] { "forget-delivery", work.Token, Digest(work.Payload), character, work.Record.ReceiverCharacter });
                }
                if (work.SourceForgotten && work.ReceiverForgotten) recovering.Remove(work.Token);
                return;
            }
            if (work.Task != null)
            {
                var reply = ReplyOf(work.Task); work.Task = null;
                if (Accepted(work, reply)) { Persist(work, EscrowState.Committed); Release(work); return; }
                if (Unchanged(work, reply)) { work.Record = Cancel(work.Record); work.Task = null; work.ResumeAt = 0; ledger.Fail(work.Token); return; }
            }
            var target = ThingServer.Online().FirstOrDefault(p => p.Actor.Id == work.Target.Id).Peer;
            if (target == null) return;
            work.TargetUid = target.m_uid; work.TargetCharacter = CharacterOf(target);
            if (work.TargetCharacter.Length > 0) work.Task = Commit(work);
        }
        static bool Forgotten(Task<OperationResult<IReadOnlyList<string>>> task, string token) => ReplyOf(task) is IReadOnlyList<string> reply
            && reply.Count == 2 && reply[0] == "forgotten" && reply[1] == token;
        static void ForgetSource(Work work)
        {
            if (work.SourceForgotten || work.SourceTask != null && !work.SourceTask.IsCompleted) return;
            if (Forgotten(work.SourceTask, work.Token)) { work.SourceForgotten = true; work.SourceTask = null; return; }
            var peer = ThingServer.Online().FirstOrDefault(p => p.Actor.Id == work.Target.Id).Peer;
            if (peer != null) work.SourceTask = Send(peer.m_uid, work.Token + ":forget:" + Guid.NewGuid().ToString("N"),
                new[] { work.Kind == "destroy" ? "forget-delete" : "forget", work.Token, Digest(work.Payload), CharacterOf(peer), work.SourceCharacter });
        }
        sealed class Reservation { internal ItemDrop.ItemData Item; internal string Payload; }

        static bool Caller(AuthorityContext context, out string id, out string name, out bool full)
        {
            id = ""; name = ""; full = false;
            if (context.IsLocal)
            {
                id = ZNet.instance != null && ZNet.instance.IsDedicated() ? "server" : "host";
                name = Player.m_localPlayer != null ? Player.m_localPlayer.GetPlayerName() : id;
                full = true; return true;
            }
            if (!long.TryParse(context.SenderId, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var uid)) return false;
            var peer = ZNet.instance?.GetPeers().FirstOrDefault(p => p != null && p.IsReady() && p.m_uid == uid && p.m_socket != null);
            id = peer?.m_socket.GetHostName() ?? "";
            name = peer?.m_playerName ?? "";
            full = id.Length > 0 && context.IsAdmin && ZNet.instance.IsAdmin(id);
            return id.Length > 0;
        }
        static Task<OperationResult<IReadOnlyList<string>>> Send(long peer, string key, string[] request) =>
            CruciblePlugin.PeerCommandClient.SendAsync<IReadOnlyList<string>, IReadOnlyList<string>>(peer.ToString(CultureInfo.InvariantCulture), OwnerId, key, request, CancellationToken.None);
        static IReadOnlyList<string> ReplyOf(Task<OperationResult<IReadOnlyList<string>>> task)
        {
            if (task == null || task.Status != TaskStatus.RanToCompletion) return null;
            var result = task.Result;
            return result.State == OperationState.Succeeded ? result.Reply : null;
        }
        static OperationExecution<IReadOnlyList<string>> Done(params string[] lines) => OperationExecution<IReadOnlyList<string>>.Success(lines);

        sealed class Work
        {
            internal string Kind = "", Token = "", AdminCharacter = "", TargetCharacter = "", Payload = "", SourceCharacter = "";
            internal EscrowRecord Record;
            internal float ResumeAt;
            internal bool SourceForgotten, ReceiverForgotten;
            internal Task<OperationResult<IReadOnlyList<string>>> SourceTask, CleanupTask;
            internal int Step;
            internal long AdminUid, TargetUid;
            internal ItemSlot Slot;
            internal Actor Admin, Target;
            internal Task<OperationResult<IReadOnlyList<string>>> Task;
        }

        sealed class Codec : IOperationCodec<IReadOnlyList<string>, IReadOnlyList<string>>
        {
            public byte[] EncodeRequest(IReadOnlyList<string> value) => Pack(value, 8);
            public IReadOnlyList<string> DecodeRequest(byte[] bytes) => Unpack(bytes, 8);
            public byte[] EncodeReply(IReadOnlyList<string> value) => Pack(value, 64);
            public IReadOnlyList<string> DecodeReply(byte[] bytes) => Unpack(bytes, 64);
            static byte[] Pack(IReadOnlyList<string> lines, int max)
            {
                if (lines == null || lines.Count > max) throw new ArgumentException("Thing inventory reply is too long.");
                using (var stream = new System.IO.MemoryStream())
                using (var writer = new System.IO.BinaryWriter(stream))
                {
                    writer.Write((byte)1);
                    writer.Write((byte)lines.Count);
                    foreach (var line in lines)
                    {
                        var bytes = Encoding.UTF8.GetBytes(line ?? "");
                        if (bytes.Length > 16384) throw new ArgumentException("Thing inventory line is too long.");
                        writer.Write((ushort)bytes.Length);
                        writer.Write(bytes);
                    }
                    writer.Flush();
                    if (stream.Length > 65536) throw new ArgumentException("Thing inventory payload is too long.");
                    return stream.ToArray();
                }
            }
            static IReadOnlyList<string> Unpack(byte[] bytes, int max)
            {
                if (bytes == null || bytes.Length < 2 || bytes.Length > 65536 || bytes[0] != 1) throw new FormatException("Thing inventory format");
                var count = bytes[1];
                if (count > max) throw new FormatException("Thing inventory count");
                var result = new List<string>(count);
                var at = 2;
                var utf = new UTF8Encoding(false, true);
                for (var i = 0; i < count; i++)
                {
                    if (at + 2 > bytes.Length) throw new FormatException("Thing inventory truncated");
                    var length = bytes[at] | (bytes[at + 1] << 8);
                    at += 2;
                    if (length > 16384 || at + length > bytes.Length) throw new FormatException("Thing inventory length");
                    try { result.Add(utf.GetString(bytes, at, length)); }
                    catch (ArgumentException) { throw new FormatException("Thing inventory text"); }
                    at += length;
                }
                if (at != bytes.Length) throw new FormatException("Thing inventory trailing bytes");
                return result;
            }
        }
    }
}
