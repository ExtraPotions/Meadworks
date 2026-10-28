// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ExtraPotions.Larder.Cooking;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Larder.Runtime.Adopted.GrindstoneCooking
{
    internal static class StationAddTransactions
    {
        const string AddRpc = "larder_AddReserved_v1", ResultRpc = "larder_AddReservedResult_v1";
        const string EscrowKey = "larder.stationEscrow.v1", EscrowBackupKey = "larder.stationEscrow.backup.v1";
        const string SequenceKey = "larder.stationSequence.v1";
        const string ParkedKey = "larder.stationEscrow.parked.v1", HoldKey = "larder.stationHold.v1";
        const string ReceiptsKey = "larder_add_receipts_v1";
        const string AttemptKey = "larder_add_attempt_v1";
        const int ManyLimit = 256;
        static Harmony transport;
        static float nextPoll;
        static Player sessionPlayer;
        static readonly HashSet<long> created = new HashSet<long>();
        static readonly HashSet<long> awaitingProof = new HashSet<long>();
        static readonly HashSet<long> proven = new HashSet<long>();
        static readonly HashSet<long> unsaved = new HashSet<long>();
        static readonly HashSet<long> holdsAtLoad = new HashSet<long>();

        sealed class Escrow
        {
            internal long World, Character, Sequence;
            internal ZDOID Station;
            internal string Item;
            internal float Level;
            internal bool Rejected;

            internal string Save()
            {
                var p = new ZPackage();
                p.Write(World); p.Write(Character); p.Write(Sequence); p.Write(Station);
                p.Write(Item); p.Write(Level); p.Write(Rejected);
                return p.GetBase64();
            }

            internal static Escrow Load(string value)
            {
                var p = new ZPackage(value);
                var result = new Escrow { World = p.ReadLong(), Character = p.ReadLong(), Sequence = p.ReadLong(),
                    Station = p.ReadZDOID(), Item = p.ReadString(), Level = p.ReadSingle(), Rejected = p.ReadBool() };
                if (result.Character == 0 || result.Sequence <= 0 || string.IsNullOrEmpty(result.Item))
                    throw new FormatException("Invalid station-add escrow.");
                return result;
            }
        }

        sealed class Attempt
        {
            internal long Character, Sequence;
            internal bool Rejected;

            internal string Save()
            {
                var p = new ZPackage();
                p.Write(Character); p.Write(Sequence); p.Write(Rejected);
                return p.GetBase64();
            }

            internal static Attempt Load(string value)
            {
                var p = new ZPackage(value);
                var result = new Attempt { Character = p.ReadLong(), Sequence = p.ReadLong(), Rejected = p.ReadBool() };
                if (result.Character == 0 || result.Sequence <= 0) throw new FormatException("Invalid station-add attempt.");
                return result;
            }
        }

        internal static void Initialize()
        {
            transport = new Harmony(NativeCooking.HarmonyId + ".transactions");
            transport.CreateClassProcessor(typeof(Register)).Patch();
            foreach (var station in UnityEngine.Object.FindObjectsOfType<CookingStation>()) RegisterOn(station);
        }

        internal static void Shutdown()
        {
            transport?.UnpatchSelf();
            transport = null;
            nextPoll = 0;
            sessionPlayer = null;
            created.Clear(); awaitingProof.Clear(); proven.Clear(); unsaved.Clear(); holdsAtLoad.Clear();
            // Reservations belong to the character save, including while Cooking is disabled or this world is unloaded.
        }

        [HarmonyPatch(typeof(CookingStation), nameof(CookingStation.Awake))]
        sealed class Register
        {
            [HarmonyPostfix]
            static void Postfix(CookingStation __instance) => RegisterOn(__instance);
        }

        static void RegisterOn(CookingStation station)
        {
            if (station == null || station.m_skill != CookLevel.Skill) return;
            var view = station.m_nview;
            if (view == null || !view.IsValid() || view.m_functions.ContainsKey(AddRpc.GetStableHashCode())) return;
            view.Register<ZPackage>(AddRpc, (sender, p) => NativeCooking.Run("reserved station add", () => Receive(station, sender, p)));
            view.Register<ZPackage>(ResultRpc, (sender, p) => NativeCooking.Run("station add result", () => Result(station, sender, p)));
            NativeCooking.RememberRpc(view, AddRpc);
            NativeCooking.RememberRpc(view, ResultRpc);
        }

        static bool Local(Player player) => player != null && player == Player.m_localPlayer &&
            player.m_nview != null && player.m_nview.IsValid() && player.m_nview.IsOwner() &&
            ZNet.instance != null && ZNet.instance.GetWorld() != null;

        internal static bool Begin(CookingStation station, Humanoid user, ItemDrop.ItemData item)
        {
            var player = user as Player;
            if (!Local(player) || player.IsDead() || player.GetPlayerID() == 0 || item == null || item.m_stack < 1 ||
                !player.GetInventory().ContainsItem(item)) return false;
            NoteSession(player);
            // A reservation for another world, or for a station that is not spawned, must not block this one.
            ParkForeign(player);
            ParkIfStationUnloaded(player);
            ActivateParked(player);
            if (player.m_customData.ContainsKey(EscrowKey)) return false;
            long sequence = 0;
            if (player.m_customData.TryGetValue(SequenceKey, out string prior) &&
                !long.TryParse(prior, NumberStyles.None, CultureInfo.InvariantCulture, out sequence)) return false;
            if (sequence < 0 || sequence == long.MaxValue) return false;
            var reserved = item.Clone();
            reserved.m_stack = 1;
            reserved.m_gridPos = new Vector2i(0, 0);
            var inventory = new Inventory("Larder escrow", null, 1, 1);
            inventory.m_inventory.Add(reserved);
            var payload = new ZPackage();
            inventory.Save(payload);
            var escrow = new Escrow { World = ZNet.instance.GetWorldUID(), Character = player.GetPlayerID(),
                Sequence = sequence + 1, Station = station.m_nview.GetZDO().m_uid, Item = payload.GetBase64(), Level = CookLevel.Local() };
            created.Add(escrow.Sequence);
            unsaved.Add(escrow.Sequence);
            player.m_customData[SequenceKey] = escrow.Sequence.ToString(CultureInfo.InvariantCulture);
            SetActive(player, escrow);
            // Mutate before Changed so a reentrant character save sees the debit and reservation together.
            var carried = player.GetInventory();
            item.m_stack--;
            if (item.m_stack == 0) carried.m_inventory.Remove(item);
            try { carried.Changed(); }
            catch (Exception error) { NativeCooking.Report("Escrow inventory notification: " + error.Message); }
            // The profile save is the write-ahead record. The station is asked only after it succeeds.
            if (Persist(player)) unsaved.Remove(escrow.Sequence);
            if (!unsaved.Contains(escrow.Sequence)) Send(station, escrow);
            return true;
        }

        internal static void Tick()
        {
            if (Time.realtimeSinceStartup < nextPoll) return;
            nextPoll = Time.realtimeSinceStartup + 2f;
            var player = Player.m_localPlayer;
            if (!Local(player)) return;
            NativeCooking.Run("station escrow recovery", () => Recover(player));
        }

        static void NoteSession(Player player)
        {
            if (ReferenceEquals(sessionPlayer, player)) return;
            sessionPlayer = player;
            created.Clear(); awaitingProof.Clear(); proven.Clear(); unsaved.Clear(); holdsAtLoad.Clear();
            var holds = LoadMany(player, HoldKey);
            if (holds == null) return;
            foreach (var hold in holds) holdsAtLoad.Add(hold.Sequence);
        }

        static void Recover(Player player)
        {
            NoteSession(player);
            ParkForeign(player);
            ParkIfStationUnloaded(player);
            ActivateParked(player);
            ReconcileHolds(player);
            if (!TryLoadActive(player, out var escrow)) return;
            if (escrow.Character != player.GetPlayerID() || escrow.World != ZNet.instance.GetWorldUID()) return;
            if (unsaved.Contains(escrow.Sequence))
            {
                if (!Persist(player)) return;
                unsaved.Remove(escrow.Sequence);
            }
            if (escrow.Rejected) { FinishReject(player, escrow); return; }
            var station = Find(escrow.Station);
            if (station == null) return;
            if (!TryDecision(station, escrow, out var decision)) return;
            if (decision == StationAddDecision.Accepted)
            {
                // A receipt already stored at load survived a world save. One written this session has not.
                if (created.Contains(escrow.Sequence) || awaitingProof.Contains(escrow.Sequence)) RememberHold(player, escrow);
                else CommitProven(player, escrow);
                return;
            }
            // A newer sequence cannot prove whether this character backup's ingredient was ever placed.
            if (decision == StationAddDecision.Stale) return;
            if (decision == StationAddDecision.Rejected) { escrow.Rejected = true; FinishReject(player, escrow); return; }
            if (!created.Contains(escrow.Sequence)) awaitingProof.Add(escrow.Sequence);
            RegisterOn(station);
            Send(station, escrow);
        }

        static void CommitProven(Player player, Escrow escrow)
        {
            proven.Add(escrow.Sequence);
            ClearActive(player);
            DropHold(player, escrow);
            Persist(player);
        }

        static void RememberHold(Player player, Escrow escrow)
        {
            var holds = LoadMany(player, HoldKey);
            if (holds == null) return;
            bool added = !holds.Exists(h => h.World == escrow.World && h.Character == escrow.Character && h.Sequence == escrow.Sequence);
            if (added) holds.Add(escrow);
            player.m_customData[HoldKey] = SaveMany(holds);
            bool removed = ClearActive(player);
            if (added || removed) Persist(player);
        }

        static void DropHold(Player player, Escrow escrow)
        {
            var holds = LoadMany(player, HoldKey);
            if (holds == null || holds.RemoveAll(h => h.World == escrow.World && h.Character == escrow.Character && h.Sequence == escrow.Sequence) == 0) return;
            if (holds.Count == 0) player.m_customData.Remove(HoldKey);
            else player.m_customData[HoldKey] = SaveMany(holds);
        }

        static void FinishReject(Player player, Escrow escrow)
        {
            string next = escrow.Save();
            bool changed = !player.m_customData.TryGetValue(EscrowKey, out string prior) || prior != next;
            SetActive(player, escrow);
            if (TryGive(player, escrow, () => ClearActive(player))) changed = true;
            if (changed) Persist(player);
        }

        static void ParkForeign(Player player)
        {
            if (!TryLoadActive(player, out var escrow)) return;
            if (escrow.Character != player.GetPlayerID() || escrow.World == ZNet.instance.GetWorldUID()) return;
            Park(player, escrow);
        }

        static void ParkIfStationUnloaded(Player player)
        {
            if (ZNetScene.instance == null || !TryLoadActive(player, out var escrow) || escrow.Rejected) return;
            if (escrow.World != ZNet.instance.GetWorldUID() || escrow.Character != player.GetPlayerID()) return;
            if (Find(escrow.Station) != null) return;
            Park(player, escrow);
        }

        static void Park(Player player, Escrow escrow)
        {
            var parked = LoadMany(player, ParkedKey);
            if (parked == null) return;
            parked.RemoveAll(e => e.World == escrow.World && e.Character == escrow.Character && e.Sequence == escrow.Sequence);
            parked.Add(escrow);
            player.m_customData[ParkedKey] = SaveMany(parked);
            ClearActive(player);
            Persist(player);
        }

        static void ActivateParked(Player player)
        {
            if (player.m_customData.ContainsKey(EscrowKey)) return;
            var parked = LoadMany(player, ParkedKey);
            if (parked == null || parked.Count == 0) return;
            long world = ZNet.instance.GetWorldUID(), character = player.GetPlayerID();
            int index = parked.FindIndex(e => e.World == world && e.Character == character && (e.Rejected || Find(e.Station) != null));
            if (index < 0) return;
            var mine = parked[index];
            parked.RemoveAt(index);
            SetActive(player, mine);
            if (parked.Count == 0) player.m_customData.Remove(ParkedKey);
            else player.m_customData[ParkedKey] = SaveMany(parked);
            Persist(player);
        }

        static void ReconcileHolds(Player player)
        {
            if (!player.m_customData.ContainsKey(HoldKey)) return;
            var holds = LoadMany(player, HoldKey);
            if (holds == null) return;
            long world = ZNet.instance.GetWorldUID(), character = player.GetPlayerID();
            bool changed = false;
            for (int i = holds.Count - 1; i >= 0; i--)
            {
                var hold = holds[i];
                if (hold.World != world || hold.Character != character || !holdsAtLoad.Contains(hold.Sequence)) continue;
                var station = Find(hold.Station);
                if (station == null || !TryDecision(station, hold, out var decision)) continue;
                if (decision == StationAddDecision.Accepted)
                {
                    holds.RemoveAt(i);
                    changed = true;
                }
                else if (decision != StationAddDecision.Stale)
                {
                    var remaining = new List<Escrow>(holds);
                    remaining.RemoveAt(i);
                    string saved = remaining.Count == 0 ? null : SaveMany(remaining);
                    if (TryGive(player, hold, () =>
                    {
                        if (saved == null) player.m_customData.Remove(HoldKey);
                        else player.m_customData[HoldKey] = saved;
                    }))
                    {
                        holds.RemoveAt(i);
                        changed = true;
                    }
                }
            }
            if (!changed) return;
            if (holds.Count == 0) player.m_customData.Remove(HoldKey);
            else player.m_customData[HoldKey] = SaveMany(holds);
            Persist(player);
        }

        static bool TryLoadActive(Player player, out Escrow escrow)
        {
            escrow = null;
            if (!player.m_customData.TryGetValue(EscrowKey, out string saved)) return false;
            bool backupValid = TryLoadBackup(player, out string backup, out Escrow backupEscrow);
            if (backupValid && backup != saved)
            {
                NativeCooking.Report("Escrow active record differed from its validated backup; backup restored.");
                player.m_customData[EscrowKey] = backup;
                Persist(player);
                escrow = backupEscrow;
                return true;
            }
            try
            {
                escrow = Escrow.Load(saved);
                if (!backupValid)
                {
                    player.m_customData[EscrowBackupKey] = Protect(saved);
                    Persist(player);
                }
                return true;
            }
            catch (Exception error)
            {
                NativeCooking.Report("Escrow load: " + error.Message);
                return false;
            }
        }

        static void SetActive(Player player, Escrow escrow)
        {
            string saved = escrow.Save();
            Escrow.Load(saved);
            player.m_customData[EscrowKey] = saved;
            player.m_customData[EscrowBackupKey] = Protect(saved);
        }

        static bool ClearActive(Player player)
        {
            bool changed = player.m_customData.Remove(EscrowKey);
            return player.m_customData.Remove(EscrowBackupKey) || changed;
        }

        static string Protect(string saved)
        {
            using var hash = SHA256.Create();
            string digest = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(saved))).Replace("-", "");
            return digest + ":" + saved;
        }

        static bool TryLoadBackup(Player player, out string saved, out Escrow escrow)
        {
            saved = null; escrow = null;
            if (!player.m_customData.TryGetValue(EscrowBackupKey, out string protectedValue)) return false;
            int separator = protectedValue.IndexOf(':');
            if (separator != 64) return false;
            string candidate = protectedValue.Substring(separator + 1);
            if (!string.Equals(protectedValue.Substring(0, separator), Protect(candidate).Substring(0, separator),
                StringComparison.OrdinalIgnoreCase)) return false;
            try
            {
                escrow = Escrow.Load(candidate);
                saved = candidate;
                return true;
            }
            catch (Exception error)
            {
                NativeCooking.Report("Escrow backup: " + error.Message);
                return false;
            }
        }

        static List<Escrow> LoadMany(Player player, string key)
        {
            if (!player.m_customData.TryGetValue(key, out string saved) || string.IsNullOrEmpty(saved)) return new List<Escrow>();
            try
            {
                var package = new ZPackage(saved);
                int count = package.ReadInt();
                if (count < 0 || count > ManyLimit) return null;
                var list = new List<Escrow>(count);
                for (int i = 0; i < count; i++) list.Add(Escrow.Load(package.ReadString()));
                return list;
            }
            catch (Exception error)
            {
                NativeCooking.Report("Escrow list: " + error.Message);
                return null;
            }
        }

        static string SaveMany(List<Escrow> items)
        {
            var package = new ZPackage();
            package.Write(items.Count);
            foreach (var item in items) package.Write(item.Save());
            return package.GetBase64();
        }

        static bool Persist(Player player)
        {
            try
            {
                var game = Game.instance;
                var profile = game ? game.GetPlayerProfile() : null;
                if (profile == null || profile.GetPlayerID() != player.GetPlayerID()) return false;
                profile.SavePlayerData(player);
                return profile.Save();
            }
            catch (Exception error)
            {
                NativeCooking.Report("Escrow character save: " + error.Message);
                return false;
            }
        }

        static CookingStation Find(ZDOID id)
        {
            var instance = ZNetScene.instance == null ? null : ZNetScene.instance.FindInstance(id);
            var station = instance == null ? null : instance.GetComponent<CookingStation>();
            if (station == null || station.m_nview == null || !station.m_nview.IsValid()) return null;
            return station;
        }

        static bool TryDecision(CookingStation station, Escrow escrow, out StationAddDecision decision)
        {
            decision = StationAddDecision.New;
            try
            {
                var zdo = station.m_nview.GetZDO();
                if (zdo == null) return false;
                string raw = zdo.GetString(ReceiptsKey);
                decision = StationAddReceipts.Parse(raw).Check(escrow.Character, escrow.Sequence);
                string attemptRaw = zdo.GetString(AttemptKey);
                if (string.IsNullOrEmpty(attemptRaw)) return true;
                var attempt = Attempt.Load(attemptRaw);
                if (attempt.Character != escrow.Character || attempt.Sequence != escrow.Sequence) return true;
                if (decision == StationAddDecision.Accepted || decision == StationAddDecision.Rejected) return true;
                if (!attempt.Rejected) return false;
                decision = StationAddDecision.Rejected;
                return true;
            }
            catch (Exception error)
            {
                NativeCooking.Report("Escrow receipt: " + error.Message);
                return false;
            }
        }

        static bool TryGive(Player player, Escrow escrow, Action commit)
        {
            if (player.IsDead()) return false;
            var inventory = player.GetInventory();
            var pos = inventory.FindEmptySlot(false);
            if (pos.x < 0) return false;
            var held = ReadItem(escrow);
            if (held == null) return false;
            var item = held.m_inventory[0];
            item.m_gridPos = pos;
            // An empty cell preserves quality, custom data and provenance instead of merging unlike native stacks.
            inventory.m_inventory.Add(item);
            try
            {
                commit();
            }
            catch
            {
                inventory.m_inventory.Remove(item);
                throw;
            }
            try { inventory.Changed(); }
            catch (Exception error) { NativeCooking.Report("Escrow inventory notification: " + error.Message); }
            return true;
        }

        static void Send(CookingStation station, Escrow escrow)
        {
            var inventory = ReadItem(escrow);
            if (inventory == null) return;
            var item = inventory.m_inventory[0];
            var p = new ZPackage();
            p.Write(escrow.World); p.Write(escrow.Character); p.Write(escrow.Sequence);
            p.Write(item.m_dropPrefab.name); p.Write(item.m_cheated); p.Write(escrow.Level);
            station.m_nview.InvokeRPC(AddRpc, p);
        }

        static Inventory ReadItem(Escrow escrow)
        {
            if (ObjectDB.instance == null) return null;
            var inventory = new Inventory("Larder escrow", null, 1, 1);
            inventory.Load(new ZPackage(escrow.Item));
            return inventory.m_inventory.Count == 1 && inventory.m_inventory[0].m_stack == 1 ? inventory : null;
        }

        static void Receive(CookingStation station, long sender, ZPackage p)
        {
            var view = station.m_nview;
            if (!view.IsValid() || !view.IsOwner() || ZNet.instance == null) return;
            long world = p.ReadLong(), character = p.ReadLong(), sequence = p.ReadLong();
            string prefab = p.ReadString(); bool cheated = p.ReadBool(); float level = p.ReadSingle();
            if (world != ZNet.instance.GetWorldUID()) return;
            var zdo = view.GetZDO();
            var receipts = StationAddReceipts.Parse(zdo.GetString(ReceiptsKey));
            var decision = receipts.Check(character, sequence);
            if (decision == StationAddDecision.Stale) return;
            if (decision != StationAddDecision.New)
            {
                Reply(view, sender, world, character, sequence, decision == StationAddDecision.Accepted);
                return;
            }
            string attemptRaw = zdo.GetString(AttemptKey);
            if (!string.IsNullOrEmpty(attemptRaw))
            {
                var previous = Attempt.Load(attemptRaw);
                var priorDecision = receipts.Check(previous.Character, previous.Sequence);
                if (priorDecision != StationAddDecision.Accepted && priorDecision != StationAddDecision.Rejected)
                {
                    if (!previous.Rejected) return;
                    if (priorDecision == StationAddDecision.New)
                    {
                        zdo.Set(ReceiptsKey, receipts.Record(previous.Character, previous.Sequence, false));
                        receipts = StationAddReceipts.Parse(zdo.GetString(ReceiptsKey));
                    }
                    zdo.Set(AttemptKey, "");
                    if (previous.Character == character && previous.Sequence == sequence)
                    {
                        Reply(view, sender, world, character, sequence, false);
                        return;
                    }
                }
            }

            string accepted = receipts.Record(character, sequence, true);
            string rejected = StationAddReceipts.Parse(zdo.GetString(ReceiptsKey)).Record(character, sequence, false);
            if (!NativeCooking.Active)
            {
                zdo.Set(ReceiptsKey, rejected);
                Reply(view, sender, world, character, sequence, false);
                return;
            }
            // Write ahead of the native slot: an interrupted attempt must never be replayed or refunded as New.
            var attempt = new Attempt { Character = character, Sequence = sequence };
            zdo.Set(AttemptKey, attempt.Save());
            StationRpc.ApplyAdd(station, sender, prefab, cheated, level, (slot, placed) =>
            {
                if (!view.IsValid() || !view.IsOwner()) return;
                string outcome = placed ? accepted : rejected;
                try { zdo.Set(ReceiptsKey, outcome); }
                catch
                {
                    if (zdo.GetString(ReceiptsKey) == outcome) return;
                    if (placed)
                    {
                        station.GetSlot(slot, out string current, out _, out _, out _);
                        if (current != prefab) throw;
                        station.SetSlot(slot, "", 0f, 0, false);
                    }
                    // Only a completed rollback proves rejection. A failed rollback leaves the attempt unresolved.
                    attempt.Rejected = true;
                    zdo.Set(AttemptKey, attempt.Save());
                    throw;
                }
                zdo.Set(AttemptKey, "");
                Reply(view, sender, world, character, sequence, placed);
            });
        }

        static void Reply(ZNetView view, long sender, long world, long character, long sequence, bool accepted)
        {
            var p = new ZPackage();
            p.Write(world); p.Write(character); p.Write(sequence); p.Write(accepted);
            view.InvokeRPC(sender, ResultRpc, p);
        }

        static void Result(CookingStation station, long sender, ZPackage p)
        {
            var player = Player.m_localPlayer;
            if (!Local(player) || sender != station.m_nview.GetZDO().GetOwner() ||
                !TryLoadActive(player, out var escrow)) return;
            long world = p.ReadLong(), character = p.ReadLong(), sequence = p.ReadLong();
            bool accepted = p.ReadBool();
            if (world != ZNet.instance.GetWorldUID() || escrow.World != world || escrow.Character != character ||
                player.GetPlayerID() != character || escrow.Sequence != sequence ||
                escrow.Station != station.m_nview.GetZDO().m_uid || escrow.Rejected) return;
            NoteSession(player);
            if (!accepted) { escrow.Rejected = true; FinishReject(player, escrow); return; }
            if (proven.Contains(escrow.Sequence)) CommitProven(player, escrow);
            else RememberHold(player, escrow);
        }
    }
}
