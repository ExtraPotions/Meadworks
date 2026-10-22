// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using ExtraPotions.Barter.Trading;
using ExtraPotions.Meadworks.Crucible;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Barter.Runtime
{
    internal sealed class TradePermit
    {
        private readonly TradeRequest request;
        private readonly long generation;
        private bool finished;
        internal TradePermit(TradeRequest request, long generation) { this.request = request; this.generation = generation; }
        internal bool IsCurrent => !finished && Plugin.Active && CruciblePlugin.IsReady &&
            CruciblePlugin.Session.AuthorityReady && CruciblePlugin.Session.Generation == generation;
        internal void Finish(bool success)
        {
            if (finished) return;
            finished = true;
            TradeAuthority.Finish(request, success);
        }
    }

    internal static class TradeAuthority
    {
        private const string ReserveOperation = BarterOperations.ReserveId, FinishOperation = BarterOperations.FinishId;
        private static readonly List<Action> observations = new List<Action>();
        private static readonly List<IDisposable> registrations = new List<IDisposable>();
        private sealed class Recovery { internal TradeRequest Request; internal TradeRecovery State; internal JournalEntry Journal; internal bool Reopened, CharacterSaved; }
        private static readonly Dictionary<string, bool> localEvidence = new Dictionary<string, bool>(StringComparer.Ordinal);
        private static Player loadedPlayer;
        private static readonly HashSet<string> loadedMarkers = new HashSet<string>(StringComparer.Ordinal);
        // Retired markers outlive the journal only inside the current authority session or the sale's buyback day.
        // Open journals stay, including past this cap: the loaded marker is what closes that receipt without a second payout.
        internal const int MaxRetiredReceiptMarkers = 64;
        private const string MarkerPrefix = "meadworks.barter.receipt.";
        private static long nextMarkerSequence = 1;
        private static string prunedSession = "";
        private static bool pruneSaveFailed;
        private struct ReceiptMarker
        {
            internal string Key, Epoch;
            internal long World, Sequence;
            internal int Day;
            internal bool JournalOpen;
        }
        private static string Marker(string token) => MarkerPrefix + CruciblePlugin.Session.WorldId.ToString(CultureInfo.InvariantCulture) + ":" + token;
        private static int CurrentDay() => EnvMan.instance == null ? -1 : EnvMan.instance.GetCurrentDay();
        private static bool CurrentSession(out string session)
        {
            session = "";
            if (!CruciblePlugin.IsReady || !CruciblePlugin.Session.AuthorityReady) return false;
            session = CruciblePlugin.Session.SessionId ?? "";
            return session.Length > 0;
        }
        private static string FormatMarker(Player player, TradeRequest request)
        {
            long sequence = nextMarkerSequence;
            if (player != null)
                foreach (var pair in player.m_customData)
                    if (TryParseMarkerKey(pair.Key, out _, out _) && TryParseMarkerValue(pair.Value, out _, out long existing, out _, out _) && existing >= sequence)
                        sequence = existing + 1;
            nextMarkerSequence = sequence + 1;
            string epoch = CurrentSession(out string session) ? session : "";
            return "2:" + TradeCodec.Fingerprint(request) + "|" + sequence.ToString(CultureInfo.InvariantCulture) + "|" +
                CurrentDay().ToString(CultureInfo.InvariantCulture) + "|" + epoch;
        }
        private static bool TryParseMarkerKey(string key, out long world, out string token)
        {
            world = 0; token = "";
            if (key == null || !key.StartsWith(MarkerPrefix, StringComparison.Ordinal)) return false;
            string rest = key.Substring(MarkerPrefix.Length);
            int colon = rest.IndexOf(':');
            if (colon <= 0 || !long.TryParse(rest.Substring(0, colon), NumberStyles.Integer, CultureInfo.InvariantCulture, out world)) return false;
            token = rest.Substring(colon + 1);
            return token.Length > 0 && token.Length <= 128;
        }
        // Legacy "2" and "2:{fingerprint}" stay valid. Newer values append sequence, world day and authority epoch.
        private static bool TryParseMarkerValue(string value, out string fingerprint, out long sequence, out int day, out string epoch)
        {
            fingerprint = ""; sequence = 0; day = -1; epoch = "";
            if (value == "2") return true;
            if (value == null || !value.StartsWith("2:", StringComparison.Ordinal) || value.Length < 46) return false;
            fingerprint = value.Substring(2, 44);
            if (value.Length == 46) return true;
            if (value[46] != '|') return false;
            string rest = value.Substring(47);
            int first = rest.IndexOf('|');
            int second = first < 0 ? -1 : rest.IndexOf('|', first + 1);
            if (first <= 0 || second <= first) return false;
            if (!long.TryParse(rest.Substring(0, first), NumberStyles.None, CultureInfo.InvariantCulture, out sequence) || sequence <= 0) return false;
            if (!int.TryParse(rest.Substring(first + 1, second - first - 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out day)) return false;
            epoch = rest.Substring(second + 1);
            return epoch.Length <= 128;
        }
        private static bool MarkerMatches(string value, TradeRequest request) =>
            TryParseMarkerValue(value, out string fingerprint, out _, out _, out _) &&
            (fingerprint.Length == 0 || fingerprint == TradeCodec.Fingerprint(request));
        internal static bool HasCharacterReceipt(string token) => Player.m_localPlayer != null &&
            Player.m_localPlayer.m_customData.TryGetValue(Marker(token), out string value) &&
            TryParseMarkerValue(value, out _, out _, out _, out _);

        private static void RememberMarkers(Player player)
        {
            loadedMarkers.Clear();
            if (player == null) return;
            foreach (var pair in player.m_customData)
                if (TryParseMarkerKey(pair.Key, out _, out _) && TryParseMarkerValue(pair.Value, out _, out _, out _, out _))
                    loadedMarkers.Add(pair.Key);
        }

        private static void PruneReceiptMarkers(Player player, bool persist)
        {
            if (player == null || journalFailed) return;
            LoadJournal();
            if (journalFailed) return;
            bool sessionKnown = CurrentSession(out string session);
            int today = CurrentDay();
            bool calendarKnown = today >= 0;
            var rows = new List<ReceiptMarker>();
            long currentWorld = CruciblePlugin.IsReady ? CruciblePlugin.Session.WorldId : 0;
            foreach (var pair in player.m_customData)
            {
                if (!TryParseMarkerKey(pair.Key, out long world, out string token) || !TryParseMarkerValue(pair.Value, out _, out long sequence, out int day, out string epoch)) continue;
                if (sequence >= nextMarkerSequence) nextMarkerSequence = sequence + 1;
                rows.Add(new ReceiptMarker { Key = pair.Key, Epoch = epoch ?? "", World = world, Sequence = sequence, Day = day, JournalOpen = outcomes.ContainsKey(token) });
            }
            var keep = new HashSet<string>(StringComparer.Ordinal);
            var retired = new List<ReceiptMarker>();
            foreach (ReceiptMarker row in rows)
            {
                if (row.JournalOpen) { keep.Add(row.Key); continue; }
                if (row.Epoch.Length == 0) continue;
                // Another world's day and session are not this world's clocks. Hold that marker for the cap until the character is back there.
                bool sameWorld = currentWorld != 0 && row.World == currentWorld;
                bool inWindow = sameWorld && sessionKnown && string.Equals(row.Epoch, session, StringComparison.Ordinal);
                bool sameDay = sameWorld && calendarKnown && row.Day == today;
                bool expired = sameWorld && sessionKnown && !inWindow && (!calendarKnown || !sameDay);
                if (expired) continue;
                if (sameWorld && !sessionKnown && !sameDay) { keep.Add(row.Key); continue; }
                retired.Add(row);
            }
            retired.Sort((a, b) =>
            {
                int order = b.Sequence.CompareTo(a.Sequence);
                return order != 0 ? order : string.CompareOrdinal(b.Key, a.Key);
            });
            int kept = 0;
            foreach (ReceiptMarker row in retired)
            {
                if (kept >= MaxRetiredReceiptMarkers) break;
                keep.Add(row.Key);
                kept++;
            }
            var removed = new List<KeyValuePair<string, string>>();
            var removedLoaded = new List<string>();
            foreach (ReceiptMarker row in rows)
            {
                if (keep.Contains(row.Key) || !player.m_customData.TryGetValue(row.Key, out string value)) continue;
                removed.Add(new KeyValuePair<string, string>(row.Key, value));
                if (loadedMarkers.Contains(row.Key)) removedLoaded.Add(row.Key);
                player.m_customData.Remove(row.Key);
                loadedMarkers.Remove(row.Key);
            }
            if (removed.Count == 0)
            {
                pruneSaveFailed = false;
                if (sessionKnown) prunedSession = session;
                return;
            }
            if (!persist || TryPersistCharacter(player))
            {
                pruneSaveFailed = false;
                if (sessionKnown) prunedSession = session;
                return;
            }
            foreach (var pair in removed) player.m_customData[pair.Key] = pair.Value;
            foreach (string key in removedLoaded) loadedMarkers.Add(key);
            pruneSaveFailed = true;
        }

        private static bool TryPersistCharacter(Player player)
        {
            try
            {
                var profile = Game.instance?.GetPlayerProfile();
                if (player == null || profile == null || profile.GetPlayerID() != player.GetPlayerID() ||
                    SaveSystem.HasSessionFlag(SaveSystemSessionFlags.DontSaveCharacter)) return false;
                profile.SavePlayerData(player);
                return profile.Save();
            }
            catch (Exception error)
            {
                Plugin.Log?.Warning("Character receipt prune could not be saved: " + error.GetType().Name);
                return false;
            }
        }

        private static void PruneWhenSessionChanges(Player player)
        {
            if (pruneSaveFailed || player == null || !CurrentSession(out string session) || session == prunedSession) return;
            PruneReceiptMarkers(player, true);
        }

        internal static void BeforeCharacterLoad(Player player)
        {
            loadedPlayer = null; loadedMarkers.Clear();
            var keys = new List<string>(player.m_customData.Keys);
            foreach (string key in keys)
                if (key.StartsWith(MarkerPrefix, StringComparison.Ordinal)) player.m_customData.Remove(key);
        }

        internal static void CharacterLoaded(Player player)
        {
            loadedPlayer = player;
            LoadJournal();
            PruneReceiptMarkers(player, true);
            RememberMarkers(player);
            foreach (Recovery recovery in outcomes.Values)
            {
                recovery.Reopened = true;
                recovery.CharacterSaved = false;
                recovery.State = new TradeRecovery(recovery.Journal.World, recovery.Journal.Actor);
                if (recovery.Journal.Phase == 0 || recovery.Journal.Phase == 3) recovery.State.InventoryFinished(false);
                else recovery.State.InventoryStarting();
            }
        }
        private static readonly Dictionary<string, Recovery> outcomes = new Dictionary<string, Recovery>(StringComparer.Ordinal);
        private static readonly TradeJournal journal = new TradeJournal(System.IO.Path.Combine(BepInEx.Paths.ConfigPath, "Barter.Recovery"));
        private static bool journalLoaded, journalFailed;
        internal static bool RecoveryBlocked => RecoveryStatus.Length > 0;
        internal static string UnavailableReason
        {
            get
            {
                string recovery = RecoveryStatus;
                return recovery.Length > 0 ? recovery : BarterText.Text("trade_unavailable");
            }
        }
        internal static string RecoveryStatus
        {
            get
            {
                if (Player.m_localPlayer == null || !CruciblePlugin.IsReady) return "";
                string path = System.IO.Path.Combine(BepInEx.Paths.ConfigPath, "Barter.Recovery");
                if (journalFailed) return BarterText.Format("trade_recovery_unreadable", path);
                string actor = Player.m_localPlayer.GetPlayerID().ToString(CultureInfo.InvariantCulture);
                foreach (Recovery pending in outcomes.Values)
                    if (pending.Journal.World == CruciblePlugin.Session.WorldId && pending.Journal.Actor == actor && !pending.State.Outcome.HasValue)
                        return BarterText.Format("trade_recovery_pending", pending.Request.Token, path);
                return "";
            }
        }
        private static readonly System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
        private static bool recovering;
        internal static bool RecoveryInFlight => recovering;

        internal static void Initialize(ProductRegistration owner)
        {
            registrations.Add(CruciblePlugin.Services.Operations.Register(owner,
                BarterOperations.Reserve, new TradeCodec(), Reserve));
            registrations.Add(CruciblePlugin.Services.Operations.Register(owner,
                BarterOperations.Finish, new FinishCodec(), Complete));
            registrations.Add(CruciblePlugin.Services.Operations.Register(owner,
                BarterOperations.Recover, new TradeCodec(), Recover));
        }

        internal static void RequestRecovery()
        {
            if (recovering || !Plugin.Active || !CruciblePlugin.IsReady ||
                !CruciblePlugin.Session.AuthorityReady || Player.m_localPlayer == null) return;
            LoadJournal();
            if (journalFailed) { RecoveryMessage(false); return; }
            ReconcileLoadedCharacter();
            var player = Player.m_localPlayer;
            string actor = player.GetPlayerID().ToString(CultureInfo.InvariantCulture);
            foreach (Recovery pending in new List<Recovery>(outcomes.Values))
            {
                if (pending.Journal.World != CruciblePlugin.Session.WorldId || pending.Journal.Actor != actor) continue;
                recovering = true;
                try
                {
                    if (!pending.State.Outcome.HasValue && !SafeTradeInventory.IsBlocked(player.GetInventory()))
                    {
                        var image = new ZPackage(); player.GetInventory().Save(image);
                        string current = Convert.ToBase64String(image.GetArray());
                        if (TradeRecovery.CanConfirm(pending.Journal, current))
                            Finish(pending.Request, true);
                    }
                    player.Message(MessageHud.MessageType.Center, BarterText.Text("trade_recovery_requested"));
                    if (pending.State.Outcome.HasValue)
                    {
                        pending.Reopened = false;
                        var finish = new FinishRequest { Trade = pending.Request, Success = pending.State.Outcome.Value };
                        Observe(CruciblePlugin.RequestClient.InvokeAsync<FinishRequest, bool>(FinishOperation, finish, CancellationToken.None), result =>
                        {
                            bool acknowledged = Acknowledged(result, pending);
                            pending.State.Reply(acknowledged);
                            if (acknowledged && (pending.Journal.Phase != 2 || pending.CharacterSaved))
                                CloseAcknowledgedJournal(pending);
                            recovering = false; RecoveryMessage(acknowledged);
                        });
                    }
                    else Observe(CruciblePlugin.RequestClient.InvokeAsync<TradeRequest, bool>(BarterOperations.RecoverId,
                        pending.Request, CancellationToken.None), result =>
                    {
                        recovering = false;
                        // A terminal server receipt cannot prove which items an uncertain local exchange changed.
                        RecoveryMessage(false);
                    });
                }
                catch (Exception error)
                { recovering = false; Plugin.Log?.Warning("Trade recovery request retained: " + error.GetType().Name); RecoveryMessage(false); }
                return;
            }
            RecoveryMessage(!RecoveryBlocked);
        }

        private static void RecoveryMessage(bool resolved) => Player.m_localPlayer?.Message(MessageHud.MessageType.Center,
            BarterText.Text(resolved ? "trade_recovery_resolved" : "trade_recovery_uncertain"));

        private static OperationExecution<bool> Recover(AuthorityContext context, TradeRequest request)
        {
            if (!Plugin.Active || ZNet.instance == null || !ZNet.instance.IsServer()) return OperationExecution<bool>.Rejected("AuthorityUnavailable");
            ZDO zdo = TraderZdo(request);
            if (zdo == null) return OperationExecution<bool>.Rejected("TraderUnavailable");
            var receipt = AuthorityReceipt.Read(zdo, request.Token);
            if (receipt == null || receipt.Actor != context.ActorId || receipt.Fingerprint != TradeCodec.Fingerprint(request))
                return OperationExecution<bool>.Rejected("ReceiptMismatch");
            if (!receipt.Applied && (receipt.State == 1 || receipt.State == 2 || receipt.State == 5 || receipt.State == 6))
                return Complete(context, new FinishRequest { Trade = request, Success = receipt.State == 1 || receipt.State == 5 });
            var ledger = Purses.ReadLedger(zdo, 0);
            AuthorityReceipt.FinalizePrevious(zdo, ledger);
            receipt = AuthorityReceipt.Read(zdo, request.Token);
            return receipt.Applied ? OperationExecution<bool>.Success(true) : OperationExecution<bool>.Rejected("RecoveryNeedsInventoryEvidence");
        }

        internal static bool TryBegin(StoreGui store, TradeRequest request, Action<TradePermit> complete)
        {
            if (!Plugin.Active || !CruciblePlugin.IsReady || store == null || store.m_trader == null ||
                !CruciblePlugin.Session.AuthorityReady || complete == null || Player.m_localPlayer == null) return false;
            LoadJournal();
            if (journalFailed) return false;
            int currentWorldCount = 0;
            foreach (Recovery pending in outcomes.Values)
                if (pending.Journal.World == CruciblePlugin.Session.WorldId) currentWorldCount++;
            if (currentWorldCount >= 256) return false;
            ReconcileLoadedCharacter();
            string actor = Player.m_localPlayer.GetPlayerID().ToString(CultureInfo.InvariantCulture);
            foreach (Recovery pending in outcomes.Values)
                if (pending.Journal.World == CruciblePlugin.Session.WorldId && pending.Journal.Actor == actor && !pending.State.Outcome.HasValue) return false;
            ZNetView view = store.m_trader.GetComponent<ZNetView>();
            if (view == null || !view.IsValid()) return false;
            ZDOID id = view.GetZDO().m_uid;
            request.TraderUser = id.UserID; request.TraderId = id.ID;
            request.Token = System.Guid.NewGuid().ToString("N"); request.Epoch = CruciblePlugin.Session.SessionId;
            long generation = CruciblePlugin.Session.Generation;
            var recovery = new Recovery { Request = request, State = new TradeRecovery(CruciblePlugin.Session.WorldId,
                actor), Journal = new JournalEntry { Token = request.Token, World = CruciblePlugin.Session.WorldId, Actor = actor,
                    Request = new TradeCodec().EncodeRequest(request) } };
            try { journal.Save(recovery.Journal); }
            catch (Exception error) { journalFailed = true; Plugin.Log?.Warning("Trade journal unavailable: " + error.GetType().Name); return false; }
            outcomes.Add(request.Token, recovery);
            Task<OperationResult<bool>> task;
            try { task = CruciblePlugin.RequestClient.InvokeAsync<TradeRequest, bool>(ReserveOperation, request, CancellationToken.None); }
            catch (Exception error)
            {
                Plugin.Log?.Warning("Trade reservation transport unavailable: " + error.GetType().Name);
                recovery.State.ReservationEnded();
                return false;
            }
            Observe(task, result =>
            {
                if (result != null && result.State == OperationState.Succeeded && result.Reply)
                {
                    var permit = new TradePermit(request, generation);
                    if (!permit.IsCurrent) { permit.Finish(false); complete(null); }
                    else
                    {
                        try
                        {
                            var before = new ZPackage(); Player.m_localPlayer.GetInventory().Save(before);
                            recovery.Journal.BeforeInventory = Convert.ToBase64String(before.GetArray());
                            recovery.Journal.Phase = 1; journal.Save(recovery.Journal);
                        }
                        catch (Exception error) { Plugin.Log?.Warning("Trade journal refused inventory start: " + error.GetType().Name); permit.Finish(false); complete(null); return; }
                        recovery.State.InventoryStarting(); complete(permit);
                    }
                }
                else
                {
                    Plugin.Log?.Warning("Trade reservation rejected: " + (result?.Code ?? "TransportFailure") + "; receipt " + request.Token);
                    recovery.State.ReservationEnded();
                    if (result != null && (result.State == OperationState.Rejected || result.State == OperationState.Cancelled))
                    { journal.Remove(request.Token); outcomes.Remove(request.Token); }
                    complete(null);
                }
            });
            return true;
        }

        private static void Observe<T>(Task<OperationResult<T>> task, Action<OperationResult<T>> complete)
        {
            Action poll = null;
            poll = () =>
            {
                if (!task.IsCompleted) return;
                observations.Remove(poll);
                complete(task.Status == TaskStatus.RanToCompletion ? task.Result : null);
            };
            observations.Add(poll);
        }

        internal static void Tick()
        {
            LoadJournal();
            ReconcileLoadedCharacter();
            foreach (Action observe in observations.ToArray())
                try { observe(); }
                catch (Exception error)
                {
                    observations.Remove(observe);
                    Plugin.Log?.Warning("Trade observation failed; unresolved reservations remain locked: " + error.GetType().Name);
                    Player.m_localPlayer?.Message(MessageHud.MessageType.Center, UnavailableReason);
                }
            RetryOutcomes();
        }

        internal static void Finish(TradeRequest request, bool success)
        {
            if (!outcomes.TryGetValue(request.Token, out Recovery recovery)) return;
            if (success && (Player.m_localPlayer == null || !CruciblePlugin.IsReady ||
                recovery.Journal.World != CruciblePlugin.Session.WorldId ||
                recovery.Journal.Actor != Player.m_localPlayer.GetPlayerID().ToString(CultureInfo.InvariantCulture)))
            { recovery.State.InventoryStarting(); return; }
            try
            {
                if (success)
                {
                    if (recovery.Journal.Consumed && recovery.CharacterSaved)
                    { recovery.State.InventoryFinished(true); RetryOutcomes(); return; }
                    var after = new ZPackage(); Player.m_localPlayer.GetInventory().Save(after);
                    string image = Convert.ToBase64String(after.GetArray());
                    if (recovery.Journal.Consumed && recovery.Journal.AfterInventory != image)
                    { recovery.State.InventoryStarting(); return; }
                    recovery.Journal.AfterInventory = image;
                    recovery.Journal.Consumed = true;
                }
                recovery.Journal.Phase = success ? (byte)2 : (byte)3; journal.Save(recovery.Journal);
                if (success)
                {
                    SaveCharacterReceipt(recovery);
                    recovery.CharacterSaved = true;
                }
                if (ZNet.instance != null && ZNet.instance.IsServer()) localEvidence[request.Token] = success;
            }
            catch (Exception error) { recovery.State.InventoryStarting(); Plugin.Log?.Warning("Trade outcome journal unavailable; receipt retained: " + error.GetType().Name); return; }
            recovery.State.InventoryFinished(success);
            RetryOutcomes();
        }

        private static void SaveCharacterReceipt(Recovery recovery)
        {
            var player = Player.m_localPlayer;
            var profile = Game.instance?.GetPlayerProfile();
            if (player == null || profile == null || profile.GetPlayerID() != player.GetPlayerID() ||
                SaveSystem.HasSessionFlag(SaveSystemSessionFlags.DontSaveCharacter))
                throw new System.IO.IOException("Character profile cannot save the trade receipt");
            string key = Marker(recovery.Request.Token);
            bool hadMarker = player.m_customData.TryGetValue(key, out string previous);
            player.m_customData[key] = FormatMarker(player, recovery.Request);
            try
            {
                profile.SavePlayerData(player);
                if (!profile.Save()) throw new System.IO.IOException("Character receipt save failed");
                loadedMarkers.Add(key);
            }
            catch
            {
                if (hadMarker) player.m_customData[key] = previous;
                else player.m_customData.Remove(key);
                throw;
            }
        }

        private static void RetryOutcomes()
        {
            if (recovering || !Plugin.Active || !CruciblePlugin.IsReady || !CruciblePlugin.Session.AuthorityReady || Player.m_localPlayer == null) return;
            string actor = Player.m_localPlayer.GetPlayerID().ToString(CultureInfo.InvariantCulture);
            foreach (Recovery recovery in new List<Recovery>(outcomes.Values))
            {
                if (!recovery.State.TryDispatch(CruciblePlugin.Session.WorldId, actor, clock.Elapsed.TotalSeconds)) continue;
                var finish = new FinishRequest { Trade = recovery.Request, Success = recovery.State.Outcome.Value };
                try
                {
                    Observe(CruciblePlugin.RequestClient.InvokeAsync<FinishRequest, bool>(FinishOperation, finish, CancellationToken.None), result =>
                    {
                        bool acknowledged = Acknowledged(result, recovery);
                        recovery.State.Reply(acknowledged);
                        if (acknowledged && (recovery.Journal.Phase != 2 || recovery.CharacterSaved))
                            CloseAcknowledgedJournal(recovery);
                    });
                }
                catch (Exception error)
                {
                    recovery.State.Reply(false);
                    Plugin.Log?.Warning("Trade outcome transport unavailable; retry retained for receipt " + recovery.Request.Token + "; " + error.GetType().Name);
                }
            }
        }

        private static ZDO TraderZdo(TradeRequest request) => ZDOMan.instance?.GetZDO(new ZDOID(request.TraderUser, request.TraderId));

        private static bool Acknowledged(OperationResult<bool> result, Recovery recovery)
            => result != null && (result.State == OperationState.Succeeded && result.Reply ||
                result.Code == "ReceiptExpired" && recovery.Journal.Phase != 1 &&
                (recovery.Journal.Phase != 2 || recovery.CharacterSaved));

        private static void CloseAcknowledgedJournal(Recovery recovery)
        {
            journal.Remove(recovery.Request.Token, recovery.CharacterSaved);
            outcomes.Remove(recovery.Request.Token);
            if (Player.m_localPlayer != null) PruneReceiptMarkers(Player.m_localPlayer, true);
        }

        private static void ReconcileLoadedCharacter()
        {
            if (journalFailed) return;
            try { ReconcileCharacter(); }
            catch (Exception error)
            {
                journalFailed = true;
                Plugin.Log?.Warning("Character recovery requires reconciliation; evidence retained: " + error.GetType().Name);
            }
        }

        private static void ReconcileCharacter()
        {
            var player = Player.m_localPlayer;
            if (player == null || player != loadedPlayer || !CruciblePlugin.IsReady) return;
            PruneWhenSessionChanges(player);
            string actor = player.GetPlayerID().ToString(CultureInfo.InvariantCulture);
            foreach (Recovery recovery in new List<Recovery>(outcomes.Values))
            {
                if (!recovery.Reopened || recovery.Journal.Actor != actor || recovery.Journal.World != CruciblePlugin.Session.WorldId) continue;
                var entry = recovery.Journal;
                if (entry.Phase == 2)
                {
                    bool saved = loadedMarkers.Contains(Marker(entry.Token)) &&
                        player.m_customData.TryGetValue(Marker(entry.Token), out string value) &&
                        MarkerMatches(value, recovery.Request);
                    // An old profile cannot prove that its output was not moved into the world already.
                    recovery.Reopened = false;
                    if (!saved) { recovery.State.InventoryStarting(); continue; }
                    recovery.CharacterSaved = saved;
                    recovery.State.InventoryFinished(true);
                }
                recovery.Reopened = false;
                if (recovery.State.Outcome.HasValue && ZNet.instance != null && ZNet.instance.IsServer())
                    localEvidence[entry.Token] = recovery.State.Outcome.Value;
            }
        }

        private static void LoadJournal()
        {
            if (journalLoaded) return;
            journalLoaded = true;
            try
            {
                foreach (var entry in journal.Read())
                {
                    var request = new TradeCodec().DecodeRequest(entry.Request);
                    if (request.Token != entry.Token) throw new System.IO.InvalidDataException("Journal receipt mismatch");
                    var state = new TradeRecovery(entry.World, entry.Actor);
                    if (entry.Phase == 0 || entry.Phase == 3) state.InventoryFinished(false);
                    else state.InventoryStarting();
                    outcomes.Add(request.Token, new Recovery { Request = request, State = state, Journal = entry, Reopened = true });
                }
            }
            catch (Exception error) { journalFailed = true; Plugin.Log?.Warning("Trade recovery journal requires reconciliation: " + error.GetType().Name); }
        }

        private static OperationExecution<bool> Reserve(AuthorityContext context, TradeRequest request)
        {
            if (!Plugin.Active || ZNet.instance == null || !ZNet.instance.IsServer() ||
                request.Epoch != context.Session.SessionId || !System.Guid.TryParseExact(request.Token, "N", out _))
                return Reject(request, "InvalidSession");
            ZDO zdo = TraderZdo(request);
            Trader trader = zdo == null ? null : ZNetScene.instance?.GetPrefab(zdo.GetPrefab())?.GetComponent<Trader>();
            if (trader == null) return Reject(request, "TraderUnavailable");
            if (!TraderAuthority.TryValidate(zdo, out _)) return Reject(request, "TraderUnavailable");
            if (AuthorityReceipt.Read(zdo, request.Token) != null) return Reject(request, "AlreadyReserved");
            if (!TradeQuoteValidation.TryValidate(context, request, trader, zdo, out long delta, out string reason))
                return Reject(request, reason);
            if (!TradeFeatures.CanCapture(request, trader, zdo)) return Reject(request, "EscrowUnavailable");
            if (context.IsLocal && request.Service == 0 && !request.Buy && request.Snapshot.Length > 0 && !TradeEvidence.MatchesInventory(request, true))
                return Reject(request, "InventoryEvidenceMismatch");
            PurseLedger ledger = Purses.ReadLedger(zdo, Purses.Balance(trader, zdo));
            AuthorityReceipt.FinalizePrevious(zdo, ledger);
            AuthorityReceipt.Rotate(zdo, ledger, context.Session.SessionId);
            if (ledger.Actor == context.ActorId && ledger.Token == request.Token && ledger.Epoch == request.Epoch)
                return Reject(request, "AlreadyReserved");
            string fingerprint = TradeCodec.Fingerprint(request);
            if (!ledger.Reserve(context.ActorId, request.Token, request.Epoch, fingerprint, delta, out reason))
            {
                if (ledger.Pending) Plugin.Log?.Warning("Trader awaits inventory recovery: receipt " + ledger.Token + "; actor " + ledger.Actor);
                return Reject(request, reason);
            }
            context.MarkDecisionMade();
            new AuthorityReceipt { Actor = context.ActorId, Fingerprint = fingerprint, Epoch = request.Epoch }.Save(zdo, request.Token);
            Purses.WriteLedger(zdo, ledger);
            // Retain the canonical item as hidden reservation evidence until the inventory outcome is proved.
            if (request.Service == 1) TradeFeatures.ReserveBuyback(context.ActorId, request, zdo);
            return OperationExecution<bool>.Success(true);
        }

        private static OperationExecution<bool> Reject(TradeRequest request, string reason)
        {
            string prefab = request.Prefab.Replace('\r', ' ').Replace('\n', ' ');
            string token = request.Token.Replace('\r', ' ').Replace('\n', ' ');
            Plugin.Log?.Warning("Trade quote rejected: " + reason + "; prefab " + prefab + "; receipt " + token);
            return OperationExecution<bool>.Rejected(reason);
        }

        private static OperationExecution<bool> Complete(AuthorityContext context, FinishRequest request)
        {
            if (!Plugin.Active || ZNet.instance == null || !ZNet.instance.IsServer()) return OperationExecution<bool>.Rejected("AuthorityUnavailable");
            ZDO zdo = TraderZdo(request.Trade);
            if (zdo == null) return OperationExecution<bool>.Rejected("TraderUnavailable");
            AuthorityReceipt receipt = AuthorityReceipt.Read(zdo, request.Trade.Token);
            if (receipt == null && request.Trade.Epoch != context.Session.SessionId)
                return OperationExecution<bool>.Rejected("ReceiptExpired");
            if (receipt == null || receipt.Actor != context.ActorId || receipt.Fingerprint != TradeCodec.Fingerprint(request.Trade))
                return OperationExecution<bool>.Rejected("ReceiptMismatch");
            if (receipt.Applied)
                return ((receipt.State == 1 || receipt.State == 5) && !receipt.CharacterConsumed) ||
                    (receipt.State == 1 || receipt.State == 2 || receipt.State == 5 || receipt.State == 6) && request.Success != (receipt.State == 1 || receipt.State == 5)
                    ? OperationExecution<bool>.Rejected("ReceiptMismatch") : OperationExecution<bool>.Success(true);
            PurseLedger ledger = Purses.ReadLedger(zdo, 0);
            if (!TradeCodec.MatchesReceipt(ledger, request.Trade)) return OperationExecution<bool>.Rejected("ReceiptMismatch");
            bool verified = context.IsLocal && localEvidence.TryGetValue(request.Trade.Token, out bool observed) && observed == request.Success;
            context.MarkDecisionMade();
            if (ledger.Quarantined)
            { receipt.State = 4; receipt.Applied = true; receipt.Save(zdo, request.Trade.Token); return OperationExecution<bool>.Success(true); }
            if (receipt.State == 5 || receipt.State == 6 || !verified && receipt.State != 1 && receipt.State != 2)
            {
                if (context.IsLocal && receipt.State != 5 && receipt.State != 6) return OperationExecution<bool>.Rejected("InventoryEvidenceMismatch");
                if ((receipt.State == 5 || receipt.State == 6) && request.Success != (receipt.State == 5))
                    return OperationExecution<bool>.Rejected("ReceiptMismatch");
                receipt.State = request.Success ? (byte)5 : (byte)6; receipt.CharacterConsumed |= request.Success;
                receipt.Save(zdo, request.Trade.Token);
                if (!ledger.FinishReported(context.ActorId, request.Trade.Token, request.Trade.Epoch, request.Success))
                    return OperationExecution<bool>.Rejected("ReceiptMismatch");
                Purses.WriteLedger(zdo, ledger);
                receipt.Applied = true; receipt.Save(zdo, request.Trade.Token);
                return OperationExecution<bool>.Success(true);
            }
            if (receipt.State != 1 && receipt.State != 2)
            { receipt.State = request.Success ? (byte)1 : (byte)2; receipt.CharacterConsumed |= request.Success; receipt.Save(zdo, request.Trade.Token); }
            bool success = receipt.State == 1;
            if (!ledger.Finish(context.ActorId, request.Trade.Token, request.Trade.Epoch, success, true))
                return OperationExecution<bool>.Rejected("ReceiptMismatch");
            TradeFeatures.Finish(context.ActorId, request.Trade, success, zdo, true);
            Purses.WriteLedger(zdo, ledger);
            receipt.Applied = true; receipt.Save(zdo, request.Trade.Token);
            localEvidence.Remove(request.Trade.Token);
            return OperationExecution<bool>.Success(true);
        }

        internal static void Shutdown()
        {
            recovering = false;
            prunedSession = "";
            pruneSaveFailed = false;
            observations.Clear();
            foreach (Recovery recovery in outcomes.Values) recovery.State.Suspend();
            foreach (IDisposable registration in registrations) registration.Dispose();
            registrations.Clear();
        }

    }
}
