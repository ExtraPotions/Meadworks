// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using BepInEx;
using ExtraPotions.Wildblood.Adopted.Runtime;
using ExtraPotions.Wildblood.Adopted.Rules;
using ExtraPotions.Wildblood.Adopted.Traits;
using ExtraPotions.Wildblood.Integration;
using ExtraPotions.Wildblood.AdoptedLuck;
using ExtraPotions.Wildblood.AdoptedCm;
using ExtraPotions.Wildblood.Luck;
using ExtraPotions.Wildblood.Transfers;
using HarmonyLib;
using UnityEngine;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Wildblood.Runtime;

internal static class LuckDeathAdmission
{
    internal const string Rpc = "wildblood.luck.decision", ProtocolKey = "wildblood.luck.decision.protocol";
    internal const string EpochKey = "wildblood.luck.decision.epoch", RevisionKey = "wildblood.luck.decision.revision", PendingKey = "wildblood.luck.decision.pending";
    internal const string SourceFenceKey = "wildblood.luck.decision.source";
    private const int Capacity = 128;
    private static readonly string processEpoch = Guid.NewGuid().ToString("N");
    private static readonly Dictionary<ZDOID, Flight> sources = new();
    private static readonly Dictionary<string, Flight> requests = new(StringComparer.Ordinal);
    private static readonly Dictionary<(long, long), Owner> owners = new();
    private static readonly Queue<string> completed = new();
    [ThreadStatic] private static DeathScope? current;
    private static bool stopped, faulted;
    private static long recoveryWorld, recoveryPlayer, recoveryGeneration, recoveryRevision;
    private static string recoveryEpoch = "", recoveryId = "", recoveryGrantEpoch = "", recoveryState = "None", recoveryReason = "NotObserved";
    private static string recoveryGrantDigest = "", recoveryResultDigest = "", recoveryGrantState = "", recoveryResultState = "";
    private static int recoveredCount, terminalCount, chainApplyCount;
    private static string recoveryReceiptDigest = "", recoveryChainDigest = "", recoveryHeadImage = "";
    private static float recoverySampledAt;
    internal static string CurrentOwnerEpoch => Session(out _, out long generation) ? Epoch(generation) : "";
    internal static object RecoveryStatus() => new {
        world = recoveryWorld.ToString(CultureInfo.InvariantCulture), player = recoveryPlayer.ToString(CultureInfo.InvariantCulture),
        currentEpoch = recoveryEpoch, operationId = recoveryId, grantEpoch = recoveryGrantEpoch,
        state = recoveryState, reason = recoveryReason, revision = recoveryRevision, recoveredCount, sampledAt = recoverySampledAt,
        grantDigest = recoveryGrantDigest, resultDigest = recoveryResultDigest, grantState = recoveryGrantState, resultState = recoveryResultState,
        receiptDigest = recoveryReceiptDigest, chainDigest = recoveryChainDigest, headImage = recoveryHeadImage, terminalCount, chainApplyCount,
        held = owners.TryGetValue((recoveryWorld, recoveryPlayer), out var heldOwner) && heldOwner.Held,
        admissionReady = owners.TryGetValue((recoveryWorld, recoveryPlayer), out var readyOwner) && readyOwner.AdmissionReady,
        pending = Player.m_localPlayer?.m_nview?.GetZDO()?.GetString(PendingKey, "") ?? "",
        currentScope = Session(out long world, out long generation) && world == recoveryWorld && generation == recoveryGeneration
            && Player.m_localPlayer != null && Player.m_localPlayer.GetPlayerID() == recoveryPlayer
            && Player.m_localPlayer.m_nview != null && Player.m_localPlayer.m_nview.IsValid() && Player.m_localPlayer.m_nview.IsOwner()
            && Game.instance != null && Game.instance.GetPlayerProfile()?.GetPlayerID() == recoveryPlayer && recoveryEpoch == Epoch(generation)
    };
    internal const string CapabilityId = "meadworks.wildblood.luck.decisions";
    private sealed class CapabilityLease : IDisposable
    {
        internal bool Open = true;
        public void Dispose() => Open = false;
    }
    internal static IDisposable RegisterCapability(ExtraPotions.Meadworks.Crucible.ProductRegistration registration)
    {
        var lease = new CapabilityLease();
        registration.AddCapability<Func<bool>>(CapabilityId, new System.Version(1, 0), () => lease.Open && Plugin.Running);
        return lease;
    }
    private static bool CapabilityReady => CruciblePlugin.Services.Capabilities.TryResolve<Func<bool>>(CapabilityId, new System.Version(1, 0), out var available) && available != null && available();
    internal static int GrantedCount { get; private set; }
    internal static int ExecutedCount { get; private set; }
    internal static int AppliedCount { get; private set; }
    internal static int AcknowledgedCount { get; private set; }
    internal static bool HasUnsettled => requests.Values.Any(f => f.Phase != "Acknowledged") || owners.Values.Any(o => o.Held);

    internal sealed class Flight
    {
        internal readonly Character Source;
        internal readonly LuckAttributionRuntime.Credit Credit;
        internal readonly LuckDecisionRequest Request;
        internal LuckDecisionGrant? Grant;
        internal LuckDecisionResult? Result;
        internal string Phase = "Waiting";
        internal float Next;
        internal bool Entered;
        internal Flight(Character source, LuckAttributionRuntime.Credit credit, LuckDecisionRequest request)
        { Source = source; Credit = credit; Request = request; }
    }

    private sealed class Owner
    {
        internal readonly long World, Player;
        internal readonly string Epoch;
        internal readonly LuckDecisionLedger Ledger;
        internal readonly Dictionary<string, Facts> Facts = new(StringComparer.Ordinal);
        internal readonly Queue<string> Terminals = new();
        internal float NextRecovery;
        internal bool RecoveryFailed;
        internal bool AdmissionReady;
        internal bool Held => Ledger.Held(World, Player) || Ledger.HasRecoveryReceipts && !AdmissionReady;
        internal Owner(long world, long player, string epoch)
        {
            World = world; Player = player; Epoch = epoch;
            string folder = Path.Combine(Paths.BepInExRootPath, "Wildblood.luck-owner", world.ToString(CultureInfo.InvariantCulture));
            Ledger = new LuckDecisionLedger(Path.Combine(folder, player.ToString(CultureInfo.InvariantCulture) + ".wal"), epoch, world, player, Capacity);
        }
    }

    private sealed class Facts
    {
        internal readonly LuckDecisionRequest Request;
        internal readonly Row[] Rows;
        internal readonly IReadOnlyList<PitySource> Catalog;
        internal bool Granted;
        internal Facts(LuckDecisionRequest request, CharacterDrop drop, ZDO source, IReadOnlyList<PitySource> catalog)
        { Request = request; Catalog = catalog; Rows = drop.m_drops.Where(row => row != null && row.m_prefab != null).Select(row => new Row(drop, row, catalog)).ToArray(); }
        internal bool Valid(LuckDelta delta)
        {
            if (!(delta.Chance > 0f && delta.Chance <= .3f) || delta.NewCounter < -1 || delta.Consumed < 0) return false;
            if (delta.Weighted)
            {
                foreach (var row in Rows)
                    if (row.Prefab == delta.Prefab && row.Chance == delta.Chance && row.Safe
                        && WeightedPity.TryWeight(Request.Level, row.Scaled, out int weight))
                        return delta.NewCounter == -1 ? delta.Consumed == weight
                            : delta.Consumed == 0 && delta.NewCounter <= NativeLuckCounters.MaximumReset(delta.Chance);
                return false;
            }
            int count = 0; var chances = new List<float>();
            foreach (var row in Rows) if (row.Prefab == delta.Prefab)
            {
                float scaled = row.Scaled ? row.Chance * Mathf.Max(1, (int)Mathf.Pow(2f, Request.Level - 1)) : row.Chance;
                chances.Add(row.Chance); chances.Add(scaled);
                if (NativeLuckCounters.Bucket(0, row.Chance) == NativeLuckCounters.Bucket(0, delta.Chance)
                    || NativeLuckCounters.Bucket(0, scaled) == NativeLuckCounters.Bucket(0, delta.Chance)) count++;
            }
            if (count == 0 || !NativeLuckCounters.TryMaximumReset(delta.Chance, chances, out double maximum)) return false;
            return delta.NewCounter == -1 ? delta.Consumed > 0 && delta.Consumed <= count
                : delta.Consumed == 0 && delta.NewCounter <= maximum;
        }
    }

    private readonly struct Row
    {
        internal readonly string Prefab;
        internal readonly float Chance;
        internal readonly bool Scaled, Safe;
        internal Row(CharacterDrop source, CharacterDrop.Drop row, IReadOnlyList<PitySource> catalog)
        { Prefab = row.m_prefab.name; Chance = row.m_chance; Scaled = row.m_levelMultiplier; Safe = NativeWeightedPity.SafeSource(source, row, catalog); }
    }

    internal sealed class DeathScope
    {
        private readonly Flight flight;
        private readonly DeathScope? previous;
        internal readonly LuckTable Table;
        internal readonly List<string> Decisions = new();
        internal readonly LuckAttributionRuntime.Credit Credit;
        internal LuckDecisionGrant Grant => flight.Grant!;
        internal Character Source => flight.Source;
        internal bool Invalid;
        private DeathScope(Flight value)
        {
            flight = value; previous = current; current = this;
            Table = LuckCodec.Parse(value.Grant!.BaseImage); Credit = value.Credit;
            Credit.Table = Table; Credit.Decision = this;
        }
        internal void Record(LuckDelta delta)
        {
            if (Decisions.Count >= 128) { Invalid = true; throw new InvalidOperationException("Luck decision transcript capacity reached; operation retained."); }
            Decisions.Add(delta.ToWire());
        }
        internal static DeathScope Create(Flight flight) => new(flight);
        internal void Restore() { Credit.Decision = null; if (ReferenceEquals(current, this)) current = previous; }
        internal void Finish(Exception? error)
        {
            Restore();
            if (Invalid || !Plugin.Ready) { flight.Phase = "Uncertain"; return; }
            try
            {
                flight.Result = new LuckDecisionResult(Grant, Decisions, LuckCodec.Write(Table));
                LuckDecisionCodec.EncodeResult(flight.Result);
                flight.Phase = "Result"; flight.Next = 0;
                Send(flight, LuckDecisionCodec.EncodeResult(flight.Result));
            }
            catch (Exception failure) { flight.Phase = "Uncertain"; Report(failure); }
        }
    }

    internal static string[] Snapshot() => completed.Concat(requests.Values.Select(f => f.Request.Id + ":" + f.Request.Source + ":" + f.Phase)).ToArray();
    private static void Complete(Flight flight)
    {
        ClearTerminalFence(flight);
        flight.Phase = "Acknowledged"; AcknowledgedCount++;
        completed.Enqueue(flight.Request.Id + ":" + flight.Request.Source + ":Acknowledged");
        while (completed.Count > 64) completed.Dequeue();
        requests.Remove(flight.Request.Id); sources.Remove(flight.Credit.Dead);
    }
    private static ZDO? OwnedSource(Flight flight)
    {
        var source = flight.Source;
        var view = source != null ? source.m_nview : null;
        if (view == null || !view.IsValid() || !view.IsOwner() || source!.GetZDOID() != flight.Credit.Dead
            || Id(view.GetZDO().m_uid) != flight.Request.Source || view.GetZDO().GetOwner() != flight.Request.SourceOwner
            || !Session(out long world, out long generation) || world != flight.Request.World
            || Epoch(generation) != flight.Request.RequesterEpoch) return null;
        return view.GetZDO();
    }
    private static bool WriteFence(Flight flight, string expected)
    {
        var zdo = OwnedSource(flight);
        if (zdo == null || zdo.GetString(SourceFenceKey, "") != expected) return false;
        zdo.Set(SourceFenceKey, flight.Request.Id);
        return zdo.GetString(SourceFenceKey, "") == flight.Request.Id;
    }
    private static void ClearTerminalFence(Flight flight)
    {
        var zdo = OwnedSource(flight);
        if (zdo != null && zdo.GetString(SourceFenceKey, "") == flight.Request.Id) zdo.Set(SourceFenceKey, "");
    }
    internal static bool Session(out long world, out long generation)
    {
        world = 0; generation = CruciblePlugin.Session.Generation;
        if (ZNet.instance == null || ZNet.m_world == null || !CruciblePlugin.Session.AuthorityReady) return false;
        world = ZNet.instance.GetWorldUID();
        return world != 0 && world == CruciblePlugin.Session.WorldId;
    }
    private static string Epoch(long generation) => processEpoch + "." + generation.ToString(CultureInfo.InvariantCulture);
    private static bool Active => !stopped && !faulted && Plugin.Ready && CruciblePlugin.IsReady && CapabilityReady && StorageManager.CurrentMode == LuckMode.Attributed
        && Plugin.Settings?.Get(Config.WildbloodSettings.Cheats.LuckyKills) != true && CharacterDropPatch.WeightedDecisionApplied;

    internal static bool ReadyForDeath(Character source)
    {
        if (source == null || source.IsPlayer() || source.IsDead() || source.GetHealth() > 0) return true;
        ZNetView? view = source.m_nview;
        if (view == null || !view.IsValid() || !view.IsOwner()) return true;
        ZDOID id = source.GetZDOID();
        string sourceFence = view.GetZDO().GetString(SourceFenceKey, "");
        if (sources.TryGetValue(id, out var retained))
        {
            if (sourceFence != retained.Request.Id || !Session(out long world, out long generation) || retained.Request.World != world || retained.Request.RequesterEpoch != Epoch(generation)
                || view.GetZDO().GetOwner() != retained.Request.SourceOwner) return false;
            if (!Active || retained.Phase != "Granted") return false;
            var retainedDrop = source.GetComponent<CharacterDrop>();
            var retainedCatalog = NativeWeightedPity.Catalog();
            var attacker = source.m_lastHit?.GetAttacker();
            return retainedDrop != null && retainedCatalog != null && attacker != null && attacker.GetZDOID() == retained.Credit.Attacker && LuckAttributionRuntime.Resolve(attacker) == retained.Credit.Player
                && view.GetZDO().GetInt(ZDOVars.s_level, 1) == retained.Request.Level
                && retained.Credit.View != null && retained.Credit.View.IsValid() && retained.Credit.View.GetZDO().GetOwner() == retained.Request.RecipientOwner
                && retained.Credit.View.GetZDO().GetString(EpochKey, "") == retained.Grant!.OwnerEpoch
                && Fingerprint(retainedDrop, retained.Request.Level, retainedCatalog) == retained.Request.Fingerprint;
        }
        if (sourceFence.Length != 0) return false;
        if (faulted && LuckAttributionRuntime.Resolve(source.m_lastHit?.GetAttacker()) != null) return false;
        if (!Active) return true;
        CharacterDrop? drop = source.GetComponent<CharacterDrop>();
        if (drop == null || LuckAttributionRuntime.Resolve(source.m_lastHit?.GetAttacker()) == null) return true;
        if (!Session(out long currentWorld, out long currentGeneration) || requests.Count >= Capacity) return false;
        try
        {
            if (!LuckAttributionRuntime.TryCredit(drop, out var credit) || credit == null || credit.View.GetZDO().GetInt(ProtocolKey, 0) != 1 || credit.Player.GetPlayerID() == 0) return false;
            var catalog = NativeWeightedPity.Catalog();
            if (catalog == null) return false;
            if (!drop.m_drops.Any(row => row != null && row.m_prefab != null && NativeWeightedPity.SafeSource(drop, row, catalog))) return true;
            int level = Math.Max(1, view.GetZDO().GetInt(ZDOVars.s_level, 1));
            if (level > WeightedPity.MaximumStars + 1) return true;
            string authorityEpoch = credit.View.GetZDO().GetString(EpochKey, "");
            long revision = credit.View.GetZDO().GetLong(RevisionKey, -1);
            if (authorityEpoch.Length == 0 || revision < 0) return false;
            var request = new LuckDecisionRequest(LuckDecisionRequest.CreateId(authorityEpoch, revision), currentWorld, Epoch(currentGeneration), Id(id),
                view.GetZDO().GetOwner(), Id(credit.Attacker), credit.Player.GetPlayerID(), Id(credit.View.GetZDO().m_uid),
                credit.View.GetZDO().GetOwner(), level, Fingerprint(drop, level, catalog), authorityEpoch, revision);
            var flight = new Flight(source, credit, request);
            if (!WriteFence(flight, "")) return false;
            sources.Add(id, flight); requests.Add(request.Id, flight);
            Send(flight, LuckDecisionCodec.EncodeRequest(request));
            return false;
        }
        catch (Exception error) { faulted = true; Report(error); return false; }
    }

    internal static DeathScope? BeginDeath(Character source)
    {
        if (source == null || !sources.TryGetValue(source.GetZDOID(), out var flight) || flight.Grant == null || flight.Entered) return null;
        if (flight.Phase != "Granted") return null;
        bool valid = Plugin.Ready && Session(out long world, out long generation) && world == flight.Request.World
            && Epoch(generation) == flight.Request.RequesterEpoch && source.m_nview != null && source.m_nview.IsValid()
            && source.m_nview.IsOwner() && source.m_nview.GetZDO().GetOwner() == flight.Request.SourceOwner
            && source.m_nview.GetZDO().GetString(SourceFenceKey, "") == flight.Request.Id;
        flight.Entered = true; flight.Phase = valid ? "Running" : "Uncertain"; ExecutedCount++;
        var scope = DeathScope.Create(flight);
        scope.Invalid = !valid;
        var catalog = NativeWeightedPity.Catalog();
        var drop = source.GetComponent<CharacterDrop>();
        if (catalog == null || drop == null || Fingerprint(drop, flight.Request.Level, catalog) != flight.Request.Fingerprint) scope.Invalid = true;
        return scope;
    }
    internal static void EndDeath(DeathScope? scope, Exception? error) => scope?.Finish(error);
    internal static bool TryCurrent(CharacterDrop drop, out LuckAttributionRuntime.Credit? credit)
    {
        credit = null;
        if (current == null || drop == null || !ReferenceEquals(drop.m_character, current.Source)) return false;
        credit = current.Credit; return true;
    }
    internal static bool MayStart => Active;
    internal static bool Faulted => faulted;
    internal static bool Stop() { stopped = true; return !HasUnsettled; }
    internal static bool Holds(Player player, long world) => owners.TryGetValue((world, player.GetPlayerID()), out var owner) && owner.Held;

    internal static void Tick()
    {
        if (!Session(out long world, out long generation)) return;
        foreach (Player player in Player.GetAllPlayers()) LuckAttributionRuntime.Register(player);
        if (Player.m_localPlayer != null && Player.m_localPlayer.m_nview != null && Player.m_localPlayer.m_nview.IsValid() && Player.m_localPlayer.m_nview.IsOwner())
        {
            try
            {
                var local = Player.m_localPlayer;
                if (local.GetPlayerID() == 0) return;
                var owner = GetOwner(world, local.GetPlayerID(), Epoch(generation));
                Recover(local, owner, generation);
            }
            catch (Exception error) { faulted = true; Report(error); }
        }
        foreach (var flight in requests.Values.ToArray())
        {
            if (flight.Phase == "Acknowledged" || flight.Request.World != world || flight.Request.RequesterEpoch != Epoch(generation)
                || Time.realtimeSinceStartup < flight.Next) continue;
            flight.Next = Time.realtimeSinceStartup + .5f;
            if (flight.Phase == "Result" && flight.Result != null) Send(flight, LuckDecisionCodec.EncodeResult(flight.Result));
            else if (flight.Phase == "Ack" && flight.Grant != null) Send(flight, LuckDecisionCodec.EncodeGrant(flight.Grant, LuckDecisionMessageKind.Acknowledge));
            else if (flight.Phase == "Waiting" || flight.Phase == "Uncertain") Send(flight, LuckDecisionCodec.EncodeRequest(flight.Request, LuckDecisionMessageKind.Status));
        }
    }

    private static bool RecoveryScope(Player player, Owner owner, long generation)
    {
        return player != null && player == Player.m_localPlayer && player.GetPlayerID() == owner.Player
            && Game.instance != null && Game.instance.GetPlayerProfile() != null
            && Game.instance.GetPlayerProfile().GetPlayerID() == owner.Player
            && player.m_nview != null && player.m_nview.IsValid() && player.m_nview.IsOwner()
            && player.m_nview.GetZDO() != null && Session(out long world, out long currentGeneration)
            && world == owner.World && currentGeneration == generation && owner.Epoch == Epoch(generation)
            && owner.Ledger.MatchesStore(owner.World, owner.Player);
    }
    private static void Recover(Player player, Owner owner, long generation)
    {
        if (Time.realtimeSinceStartup < owner.NextRecovery) return;
        owner.NextRecovery = Time.realtimeSinceStartup + .5f;
        recoveryWorld = owner.World; recoveryPlayer = owner.Player; recoveryGeneration = generation;
        recoveryEpoch = owner.Epoch;
        recoverySampledAt = Time.realtimeSinceStartup; recoveryRevision = owner.Ledger.Revision(owner.World, owner.Player);
        recoveryReceiptDigest = recoveryChainDigest = recoveryHeadImage = "";
        if (owner.RecoveryFailed) { recoveryReason = "LedgerFaultRetained"; return; }
        recoveryId = recoveryGrantEpoch = recoveryGrantDigest = recoveryResultDigest = recoveryGrantState = recoveryResultState = ""; recoveryState = "None";
        if (!RecoveryScope(player, owner, generation)) { recoveryReason = "OwnerUnavailable"; return; }
        try
        {
            var held = owner.Ledger.RetainedOperations().Where(o => o.State != LuckRecoveryState.Acknowledged && o.State != LuckRecoveryState.RecoveredTerminal).ToArray();
            var zdo = player.m_nview!.GetZDO();
            string publication = zdo.GetString(PendingKey, "");
            if (held.Length == 0)
            {
                if (owner.Ledger.HasRecoveryReceipts) { ReconcileTerminal(player, owner, generation); return; }
                recoveryReason = publication.Length == 0 ? "NoRetainedHold" : "PublicationConflict";
                if (publication.Length == 0) Publish(player, owner, null);
                return;
            }
            if (held.Length != 1) { recoveryReason = "MultipleRetainedHolds"; return; }
            var retained = held[0]; var grant = retained.Grant; var result = retained.Result;
            recoveryId = grant.Request.Id; recoveryGrantEpoch = grant.OwnerEpoch; recoveryState = retained.State.ToString();
            recoveryGrantDigest = retained.GrantDigest; recoveryResultDigest = retained.ResultDigest ?? "";
            recoveryGrantState = retained.GrantState.ToString(); recoveryResultState = retained.ResultState?.ToString() ?? "";
            if (publication.Length != 0 && publication != recoveryId) { recoveryReason = "PublicationConflict"; return; }
            bool oldEpoch = grant.OwnerEpoch != owner.Epoch;
            if (!RecoveryScope(player, owner, generation) || (zdo.GetString(PendingKey, "").Length != 0 && zdo.GetString(PendingKey, "") != recoveryId))
            { recoveryReason = "PublicationConflict"; return; }
            Publish(player, owner, grant);
            if (zdo.GetString(PendingKey, "") != recoveryId || zdo.GetString(EpochKey, "") != owner.Epoch)
            { recoveryReason = "PublicationReadbackFailed"; return; }
            if (!oldEpoch) { recoveryReason = "CurrentEpochProtocol"; return; }
            if (retained.State == LuckRecoveryState.UnresolvedNoResult) { recoveryReason = "UnresolvedNoResult"; return; }
            if (result == null || (retained.State != LuckRecoveryState.DurableResultPendingApply && retained.State != LuckRecoveryState.AppliedAwaitingReceipt))
            { recoveryReason = "UnsupportedRetainedState"; return; }
            long expectedRevision = retained.State == LuckRecoveryState.AppliedAwaitingReceipt ? checked(result.Revision + 1) : result.Revision;
            if (owner.Ledger.Revision(owner.World, owner.Player) != expectedRevision) { recoveryReason = "RevisionConflict"; return; }
            string stored = player.m_customData.TryGetValue(StorageManager.GetStorageKey(owner.World), out string saved) ? saved : "";
            if (stored != grant.BaseImage && stored != result.FinalImage) { recoveryReason = "CounterImageConflict"; return; }
            if (!StorageManager.TryRecoveryImage(player, owner.World, owner.Ledger, retained, out string image))
            { recoveryReason = "ImageUnavailable"; return; }
            if (image != grant.BaseImage && image != result.FinalImage) { recoveryReason = "CounterImageConflict"; return; }
            bool changed = image != result.FinalImage || stored != result.FinalImage;
            if (changed && !StorageManager.CommitDecision(player, owner.World, grant.BaseImage, result.FinalImage))
            { recoveryReason = "CommitReadbackFailed"; return; }
            if (!RecoveryScope(player, owner, generation) || zdo.GetString(PendingKey, "") != recoveryId
                || !StorageManager.TryDecisionImage(player, owner.World, out string confirmed) || confirmed != result.FinalImage
                || (player.m_customData.TryGetValue(StorageManager.GetStorageKey(owner.World), out string final) ? final : "") != result.FinalImage)
            { recoveryReason = "CommitReadbackFailed"; return; }
            bool pendingTransition = retained.State == LuckRecoveryState.DurableResultPendingApply;
            if (pendingTransition && !owner.Ledger.RecoverApplied(owner.World, owner.Player, retained, confirmed))
            { recoveryReason = "LedgerTransitionRefused"; return; }
            if (changed || pendingTransition) recoveredCount++;
            var after = owner.Ledger.RetainedOperations().Single(o => o.Grant.Request.Id == recoveryId);
            recoveryState = after.State.ToString(); recoveryReason = "AppliedAwaitingReceipt";
            recoveryGrantState = after.GrantState.ToString(); recoveryResultState = after.ResultState?.ToString() ?? "";
            recoveryRevision = owner.Ledger.Revision(owner.World, owner.Player);
            if (!owner.Ledger.RecoverTerminal(owner.World, owner.Player, after, confirmed))
            { recoveryReason = "TerminalReceiptRefused"; return; }
            terminalCount++;
            ReconcileTerminal(player, owner, generation);
        }
        catch
        {
            owner.RecoveryFailed = true; recoveryReason = "RecoveryFaultRetained"; throw;
        }
    }

    internal static bool MayLoadCounter(Player player, long world)
    {
        if (StorageManager.CurrentMode != LuckMode.Attributed) return true;
        if (!Session(out long currentWorld, out long generation) || currentWorld != world || player != Player.m_localPlayer
            || player.GetPlayerID() == 0 || player.m_nview == null || !player.m_nview.IsValid() || !player.m_nview.IsOwner()
            || Game.instance == null || Game.instance.GetPlayerProfile()?.GetPlayerID() != player.GetPlayerID()) return false;
        try
        {
            var owner = GetOwner(world, player.GetPlayerID(), Epoch(generation));
            if (!owner.Ledger.HasRecoveryReceipts) return true;
            if (!RecoveryScope(player, owner, generation) || !owner.AdmissionReady || player.m_customData == null
                || player.m_nview.GetZDO().GetString(PendingKey, "").Length != 0) return false;
            string raw = player.m_customData.TryGetValue(StorageManager.GetStorageKey(world), out string value) ? value : "";
            return owner.Ledger.TryCounterPlan(world, owner.Player, raw, out var plan) && plan != null
                && raw == plan.FinalImage && owner.Ledger.ValidateCounterPlan(plan, raw);
        }
        catch (Exception error) { faulted = true; Report(error); return false; }
    }

    private static bool ReconcileTerminal(Player player, Owner owner, long generation)
    {
        try
        {
            owner.AdmissionReady = false;
            if (owner.RecoveryFailed || !RecoveryScope(player, owner, generation) || player.m_customData == null)
            { recoveryReason = "OwnerUnavailable"; return false; }
            string raw = player.m_customData.TryGetValue(StorageManager.GetStorageKey(owner.World), out string stored) ? stored : "";
            if (!owner.Ledger.TryCounterPlan(owner.World, owner.Player, raw, out var plan) || plan == null)
            { recoveryReason = "CounterChainConflict"; return false; }
            var zdo = player.m_nview!.GetZDO();
            string pending = zdo.GetString(PendingKey, "");
            if (pending.Length != 0 && pending != plan.RecoveryOperationId)
            { recoveryReason = "PublicationConflict"; return false; }
            if (!StorageManager.TryRecoveryChainImage(player, owner.World, owner.Ledger, plan, out string image))
            { recoveryReason = "ImageUnavailable"; return false; }
            if (!owner.Ledger.ValidateCounterPlan(plan, image) || !owner.Ledger.ValidateCounterPlan(plan, raw))
            { recoveryReason = "CounterChainConflict"; return false; }
            bool changed = image != plan.FinalImage || raw != plan.FinalImage;
            if (changed && !StorageManager.CommitDecision(player, owner.World, image, plan.FinalImage))
            { recoveryReason = "CommitReadbackFailed"; return false; }
            if (!RecoveryScope(player, owner, generation) || zdo.GetString(PendingKey, "") != pending
                || !StorageManager.TryDecisionImage(player, owner.World, out string confirmed) || confirmed != plan.FinalImage
                || (player.m_customData.TryGetValue(StorageManager.GetStorageKey(owner.World), out string final) ? final : "") != plan.FinalImage
                || !owner.Ledger.ValidateCounterPlan(plan, confirmed))
            { recoveryReason = "CommitReadbackFailed"; return false; }
            if (changed) chainApplyCount++;
            if (pending.Length != 0) zdo.Set(PendingKey, "");
            if (zdo.GetString(PendingKey, "").Length != 0) { recoveryReason = "PublicationReadbackFailed"; return false; }
            Publish(player, owner, null);
            if (!RecoveryScope(player, owner, generation) || !ReferenceEquals(player.m_nview!.GetZDO(), zdo)
                || zdo.GetString(PendingKey, "").Length != 0 || zdo.GetString(EpochKey, "") != owner.Epoch
                || owner.Ledger.Revision(owner.World, owner.Player) != plan.Revision || zdo.GetLong(RevisionKey, -1) != plan.Revision)
            { recoveryReason = "PublicationReadbackFailed"; return false; }
            owner.AdmissionReady = true;
            recoveryWorld = owner.World; recoveryPlayer = owner.Player; recoveryGeneration = generation; recoveryEpoch = owner.Epoch;
            recoveryId = plan.RecoveryOperationId; recoveryReceiptDigest = plan.RecoveryReceiptDigest;
            recoveryChainDigest = plan.ProofDigest; recoveryHeadImage = plan.FinalImage; recoveryRevision = plan.Revision;
            recoveryState = recoveryReason = "RecoveredTerminal"; recoverySampledAt = Time.realtimeSinceStartup;
            return true;
        }
        catch
        {
            owner.AdmissionReady = false; owner.RecoveryFailed = true; recoveryReason = "RecoveryFaultRetained"; throw;
        }
    }

    private static void Send(Flight flight, byte[] bytes)
    {
        if (flight.Credit.View == null || !flight.Credit.View.IsValid() || flight.Credit.View.GetZDO().GetOwner() != flight.Request.RecipientOwner) return;
        LuckAttributionRuntime.SendDecision(flight.Credit.Player, flight.Request.RecipientOwner, bytes);
    }

    internal static void Receive(Player player, ZNetView view, long sender, byte[] bytes)
    {
        if (!Session(out long world, out long generation) || player == null || view == null || !view.IsValid()
            || !ReferenceEquals(player.m_nview, view) || !NativeRpcOwnership.OwnsView(view, NativeRpcOwner.LuckPlayer)) return;
        try
        {
            var kind = LuckDecisionCodec.PeekKind(bytes);
            if (kind == LuckDecisionMessageKind.Stale)
            {
                var stale = LuckDecisionCodec.DecodeRequest(bytes);
                if (!requests.TryGetValue(stale.Id, out var previous)
                    || sender != previous.Request.RecipientOwner || view.GetZDO().GetOwner() != sender || previous.Request.RequesterEpoch != Epoch(generation)
                    || !LuckDecisionCodec.EncodeRequest(previous.Request).SequenceEqual(LuckDecisionCodec.EncodeRequest(stale))) return;
                string nextEpoch = view.GetZDO().GetString(EpochKey, "");
                long nextRevision = view.GetZDO().GetLong(RevisionKey, -1);
                if (previous.Phase == "Ack" && previous.Result != null && previous.Grant != null && previous.Entered
                    && nextEpoch == previous.Grant.OwnerEpoch && nextRevision > previous.Grant.Revision) { Complete(previous); return; }
                if (previous.Grant != null || previous.Entered || previous.Phase != "Waiting") return;
                if (nextEpoch.Length == 0 || nextRevision < 0 || nextEpoch == stale.ExpectedOwnerEpoch && nextRevision == stale.ExpectedRevision) return;
                var next = new LuckDecisionRequest(LuckDecisionRequest.CreateId(nextEpoch, nextRevision), stale.World, stale.RequesterEpoch, stale.Source, stale.SourceOwner,
                    stale.Attacker, stale.Player, stale.Recipient, stale.RecipientOwner, stale.Level, stale.Fingerprint, nextEpoch, nextRevision);
                var replacement = new Flight(previous.Source, previous.Credit, next);
                if (!WriteFence(replacement, previous.Request.Id)) return;
                requests.Remove(stale.Id); requests.Add(next.Id, replacement);
                sources[previous.Source.GetZDOID()] = replacement;
                Send(replacement, LuckDecisionCodec.EncodeRequest(next)); return;
            }
            if (kind == LuckDecisionMessageKind.Grant || kind == LuckDecisionMessageKind.Applied || kind == LuckDecisionMessageKind.Acknowledged)
            {
                var grant = LuckDecisionCodec.DecodeGrant(bytes);
                if (!requests.TryGetValue(grant.Request.Id, out var flight) || sender != flight.Request.RecipientOwner
                    || grant.Request.World != world || grant.Request.RequesterEpoch != Epoch(generation)
                    || !LuckDecisionCodec.EncodeRequest(grant.Request).SequenceEqual(LuckDecisionCodec.EncodeRequest(flight.Request))
                    || view.GetZDO().GetOwner() != sender) return;
                if (flight.Grant != null && !LuckDecisionCodec.EncodeGrant(flight.Grant).SequenceEqual(LuckDecisionCodec.EncodeGrant(grant))) return;
                if (flight.Grant == null) { flight.Grant = grant; GrantedCount++; }
                if (kind == LuckDecisionMessageKind.Acknowledged) { if (flight.Phase == "Ack") Complete(flight); return; }
                if (kind == LuckDecisionMessageKind.Applied)
                {
                    if (flight.Result == null) return;
                    flight.Phase = "Ack"; flight.Next = 0;
                    Send(flight, LuckDecisionCodec.EncodeGrant(grant, LuckDecisionMessageKind.Acknowledge)); return;
                }
                if (flight.Phase == "Waiting") flight.Phase = "Granted";
                return;
            }
            if (player != Player.m_localPlayer || !view.IsOwner()) return;
            LuckDecisionRequest request;
            LuckDecisionResult? result = null; LuckDecisionGrant? ack = null;
            if (kind == LuckDecisionMessageKind.Result) { result = LuckDecisionCodec.DecodeResult(bytes); request = result.Request; }
            else if (kind == LuckDecisionMessageKind.Acknowledge) { ack = LuckDecisionCodec.DecodeGrant(bytes); request = ack.Request; }
            else if (kind == LuckDecisionMessageKind.Request || kind == LuckDecisionMessageKind.Status) request = LuckDecisionCodec.DecodeRequest(bytes);
            else return;
            if (request.World != world || request.Player != player.GetPlayerID() || request.Recipient != Id(view.GetZDO().m_uid)
                || request.RecipientOwner != view.GetZDO().GetOwner() || sender != request.SourceOwner || sender == 0) return;
            if (ZNet.instance.IsServer() && sender != ZNet.GetUID() && ZNet.instance.GetPeer(sender)?.IsReady() != true) return;
            var owner = GetOwner(world, player.GetPlayerID(), Epoch(generation));
            if (result == null && ack == null && !owner.Facts.ContainsKey(request.Id) && owner.Ledger.HasRecoveryReceipts
                && !ReconcileTerminal(player, owner, generation)) return;
            if (view.GetZDO().GetString(PendingKey, "").Length != 0 && !owner.Held) return;
            if (!owner.Facts.ContainsKey(request.Id) && (request.ExpectedOwnerEpoch != owner.Ledger.OwnerEpoch
                || request.ExpectedRevision != owner.Ledger.Revision(world, player.GetPlayerID())))
            {
                var old = owner.Ledger.Ask(request, "");
                if (old.State == LuckDecisionState.Stale)
                    LuckAttributionRuntime.SendDecision(player, sender, LuckDecisionCodec.EncodeRequest(request, LuckDecisionMessageKind.Stale));
                else if (old.State == LuckDecisionState.Acknowledged && old.Grant != null)
                    LuckAttributionRuntime.SendDecision(player, sender, LuckDecisionCodec.EncodeGrant(old.Grant, LuckDecisionMessageKind.Acknowledged));
                return;
            }
            if (!owner.Facts.TryGetValue(request.Id, out var facts))
            {
                if (!Active || owner.Facts.Count >= Capacity || !Authenticate(player, request, out facts)) return;
                owner.Facts.Add(request.Id, facts!);
            }
            if (!LuckDecisionCodec.EncodeRequest(facts!.Request).SequenceEqual(LuckDecisionCodec.EncodeRequest(request))) return;
            bool hasImage = StorageManager.TryDecisionImage(player, world, out string image);
            if (!hasImage && result != null) return;
            LuckDecisionReply reply;
            if (result != null)
            {
                var retained = owner.Ledger.Ask(request, image);
                if (retained.Grant == null || !Replay(facts, retained.Grant, result, out string final)) return;
                reply = owner.Ledger.Submit(result, final);
                if (reply.State == LuckDecisionState.Prepared)
                {
                    if (!StorageManager.CommitDecision(player, world, retained.Grant.BaseImage, final)) return;
                    reply = owner.Ledger.MarkApplied(result);
                    if (reply.State == LuckDecisionState.Applied) AppliedCount++;
                }
            }
            else if (ack != null) reply = owner.Ledger.Acknowledge(ack);
            else
            {
                if (!facts.Granted && !Authenticate(player, request, out _)) return;
                reply = owner.Ledger.Ask(request, image);
            }
            if (reply.Grant != null) facts.Granted = true;
            if (reply.State == LuckDecisionState.Acknowledged && !owner.Terminals.Contains(request.Id))
            {
                owner.Terminals.Enqueue(request.Id);
                while (owner.Terminals.Count > 64) owner.Facts.Remove(owner.Terminals.Dequeue());
            }
            Publish(player, owner, reply.Grant);
            if (reply.State == LuckDecisionState.Stale)
            {
                if (!facts.Granted && owner.Facts.TryGetValue(request.Id, out var staleFacts) && ReferenceEquals(facts, staleFacts))
                    owner.Facts.Remove(request.Id);
                LuckAttributionRuntime.SendDecision(player, sender, LuckDecisionCodec.EncodeRequest(request, LuckDecisionMessageKind.Stale)); return;
            }
            if (reply.Grant == null || (reply.State != LuckDecisionState.Granted && reply.State != LuckDecisionState.Applied
                && reply.State != LuckDecisionState.Acknowledged)) return;
            var replyKind = reply.State == LuckDecisionState.Acknowledged ? LuckDecisionMessageKind.Acknowledged
                : reply.State == LuckDecisionState.Applied ? LuckDecisionMessageKind.Applied : LuckDecisionMessageKind.Grant;
            LuckAttributionRuntime.SendDecision(player, sender, LuckDecisionCodec.EncodeGrant(reply.Grant, replyKind));
        }
        catch (Exception error) { Report(error); }
    }

    private static Owner GetOwner(long world, long player, string epoch)
    {
        var key = (world, player);
        if (owners.TryGetValue(key, out var prior) && prior.Epoch != epoch)
        { prior.Ledger.Dispose(); owners.Remove(key); }
        if (!owners.TryGetValue(key, out var owner))
        {
            try { owner = new Owner(world, player, epoch); owners.Add(key, owner); }
            catch { faulted = true; throw; }
        }
        return owner;
    }
    private static void Publish(Player player, Owner owner, LuckDecisionGrant? grant)
    {
        var zdo = player.m_nview?.GetZDO();
        if (zdo == null || !player.m_nview!.IsOwner()) return;
        zdo.Set(EpochKey, owner.Ledger.OwnerEpoch);
        zdo.Set(RevisionKey, owner.Ledger.Revision(owner.World, owner.Player));
        string pending = zdo.GetString(PendingKey, "");
        if (!owner.Held)
        {
            if (grant != null && pending == grant.Request.Id && owner.Ledger.RetainedOperations().Any(o => o.Grant.Request.Id == pending
                && (o.State == LuckRecoveryState.Acknowledged || o.State == LuckRecoveryState.RecoveredTerminal))) zdo.Set(PendingKey, "");
        }
        else if (grant != null && (pending.Length == 0 || pending == grant.Request.Id)) zdo.Set(PendingKey, grant.Request.Id);
        zdo.Set(ProtocolKey, 1);
    }

    private static bool Authenticate(Player player, LuckDecisionRequest request, out Facts? facts)
    {
        facts = null;
        if (!TryId(request.Source, out var id) || !TryId(request.Attacker, out var attackerId)) return false;
        var zdo = ZDOMan.instance?.GetZDO(id);
        var source = ZNetScene.instance?.FindInstance(id)?.GetComponent<Character>();
        var attacker = ZNetScene.instance?.FindInstance(attackerId)?.GetComponent<Character>();
        var drop = source?.GetComponent<CharacterDrop>();
        var catalog = NativeWeightedPity.Catalog();
        if (zdo == null || source == null || drop == null || catalog == null || source.IsPlayer() || source.IsDead() || zdo.GetOwner() != request.SourceOwner
            || zdo.GetString(SourceFenceKey, "") != request.Id || zdo.GetInt(ZDOVars.s_level, 1) != request.Level || source.GetHealth() > 0f || !Finite(source.transform.position)
            || LuckAttributionRuntime.Resolve(attacker) != player || Fingerprint(drop, request.Level, catalog) != request.Fingerprint) return false;
        facts = new Facts(request, drop, zdo, catalog); return true;
    }
    private static bool Replay(Facts facts, LuckDecisionGrant grant, LuckDecisionResult result, out string final)
    {
        final = "";
        if (result.OwnerEpoch != grant.OwnerEpoch || result.Revision != grant.Revision || result.BaseHash != grant.BaseHash) return false;
        var table = LuckCodec.Parse(grant.BaseImage);
        foreach (string wire in result.Decisions)
        {
            if (!LuckDelta.TryParse(wire, out var delta) || !facts.Valid(delta)) return false;
            if (delta.Weighted)
            {
                if (delta.NewCounter == -1 && (!WeightedPity.TryMerge(table, facts.Catalog, delta.Prefab, delta.Chance, out int? prior)
                    || !prior.HasValue || prior.Value <= delta.Consumed)) return false;
                if (!NativeWeightedPity.Apply(table, facts.Catalog, delta)) return false;
            }
            else
            {
                if (delta.NewCounter == -1 && (!table.TryGet(delta.Prefab, delta.Chance, out var prior) || prior.Counter <= delta.Consumed)) return false;
                table.Apply(delta);
            }
        }
        final = LuckCodec.Write(table); return final == result.FinalImage;
    }
    private static string Fingerprint(CharacterDrop drop, int level, IReadOnlyList<PitySource> catalog)
    {
        var rows = drop.m_drops.Where(row => row != null && row.m_prefab != null).Select(row => string.Join("|", row.m_prefab.name,
            row.m_chance.ToString("R", CultureInfo.InvariantCulture), row.m_amountMin, row.m_amountMax, row.m_levelMultiplier, row.m_onePerPlayer, row.m_dontScale, row.m_levelMultiplierNotAmount));
        var all = catalog.Select(row => string.Join("|", row.Prefab, row.Chance.ToString("R", CultureInfo.InvariantCulture), row.Minimum, row.Maximum, row.LevelMultiplier)).OrderBy(row => row, StringComparer.Ordinal);
        var character = drop.m_character != null ? drop.m_character : drop.GetComponent<Character>();
        var controller = character != null ? character.GetComponent<EliteController>() : null;
        var zdo = character?.m_nview?.GetZDO();
        int stars = zdo != null ? TraitStore.Load(zdo).Stars : Math.Max(0, level - 1);
        string policy = string.Join(":", WildbloodModules.Active(WildbloodModule.Loot), controller != null, controller?.Ready, controller?.KeepsNativeLevel,
            controller?.LootOnly, controller?.HordeOnly, RuleState.Active.KeepsLevel(stars, character != null && character.IsBoss()), RuleState.Active.Loot.Mode,
            Plugin.Settings?.Get(Config.WildbloodSettings.Luck.TameCreditRange).ToString("R", CultureInfo.InvariantCulture));
        string text = level.ToString(CultureInfo.InvariantCulture) + ":" + NativePseudoChancePatch.Applied + ":" + CharacterDropPatch.WeightedDecisionApplied
            + ":" + policy + ":" + ZoneSystem.instance.GetGlobalKey(GlobalKeys.NoPseudoDrops) + ":" + string.Join(";", rows) + ":" + string.Join(";", all);
        using var hash = SHA256.Create(); return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", "");
    }
    private static string Id(ZDOID id) => id.UserID.ToString(CultureInfo.InvariantCulture) + ":" + id.ID.ToString(CultureInfo.InvariantCulture);
    private static bool TryId(string text, out ZDOID id)
    {
        id = ZDOID.None; var parts = text.Split(':');
        if (parts.Length != 2 || !long.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out long user)
            || !uint.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out uint value)) return false;
        id = new ZDOID(user, value); return !id.IsNone() && Id(id) == text;
    }
    private static bool Finite(Vector3 position) => !float.IsNaN(position.x) && !float.IsInfinity(position.x)
        && !float.IsNaN(position.y) && !float.IsInfinity(position.y) && !float.IsNaN(position.z) && !float.IsInfinity(position.z);
    private static void Report(Exception error) => Plugin.NativeLogger?.LogWarning("Luck decision retained/refused: " + error.GetType().Name);
}

[HarmonyPatch(typeof(Character), "CheckDeath")]
internal static class LuckDeathGate
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(Character __instance) => LuckDeathAdmission.ReadyForDeath(__instance);
}

[HarmonyPatch(typeof(Character), "OnDeath")]
internal static class LuckDeathScope
{
    [HarmonyPriority(Priority.First)]
    private static void Prefix(Character __instance, out LuckDeathAdmission.DeathScope? __state) => __state = LuckDeathAdmission.BeginDeath(__instance);
    [HarmonyPriority(Priority.Last)]
    private static Exception? Finalizer(Exception? __exception, LuckDeathAdmission.DeathScope? __state)
    { LuckDeathAdmission.EndDeath(__state, __exception); return __exception; }
}
