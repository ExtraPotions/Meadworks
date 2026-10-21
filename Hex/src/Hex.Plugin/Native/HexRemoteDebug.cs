// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Original native API implementation. VAT is a behavior only design reference; no source or assemblies accessed.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BepInEx.Logging;
using ExtraPotions.Meadworks.Crucible;
using UnityEngine;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;
namespace ExtraPotions.Hex.Runtime.Native;

// Remote debug kill and drops. The server (dedicated or host) authorizes the request through the Mischief World policy, lists the targets
// from its ZDOs, and sends each owning client its own targets over the Crucible peer command path. Only the owner holds a live instance, so
// only the owner acts (native Damage, native Destroy); the server never assumes it has simulated or loaded anything. Host and single player
// keep running the native console commands.
internal static class HexRemoteDebug
{
    static readonly IDisposable?[] operations = new IDisposable?[4];
    static IDisposable? ownerCommand;
    static ManualLogSource? log;
    static readonly Dictionary<RequestIdentity, Work> works = new();
    static Task<OperationResult<HexKillReply>>? pending;
    static HexOwnerAction pendingAction;
    static bool pendingEvent;
    static CancellationTokenSource? cancel;
    static Player? clientActor;
    static long clientProfile;
    static ZDOID clientCharacter;
    static ZNet? clientNet;
    static long clientGeneration = -1, serverGeneration = -1;
    static ZNet? serverNet;
    sealed class Codec : IOperationCodec<HexAreaRequest, HexKillReply>
    {
        public byte[] EncodeRequest(HexAreaRequest value) => HexKillData.EncodeRequest(value);
        public HexAreaRequest DecodeRequest(byte[] bytes) => HexKillData.DecodeRequest(bytes);
        public byte[] EncodeReply(HexKillReply value) => HexKillData.EncodeReply(value);
        public HexKillReply DecodeReply(byte[] bytes) => HexKillData.DecodeReply(bytes);
    }
    sealed class EventCodec : IOperationCodec<bool, HexKillReply>
    {
        public byte[] EncodeRequest(bool value) => HexKillData.EncodeEvent(value);
        public bool DecodeRequest(byte[] bytes) => HexKillData.DecodeEvent(bytes);
        public byte[] EncodeReply(HexKillReply value) => HexKillData.EncodeReply(value);
        public HexKillReply DecodeReply(byte[] bytes) => HexKillData.DecodeReply(bytes);
    }
    sealed class OwnerCodec : IOperationCodec<HexOwnerCommand, HexOwnerReply>
    {
        public byte[] EncodeRequest(HexOwnerCommand value) => HexOwnerData.Encode(value);
        public HexOwnerCommand DecodeRequest(byte[] bytes) => HexOwnerData.Decode(bytes);
        public byte[] EncodeReply(HexOwnerReply value) => HexOwnerData.EncodeReply(value);
        public HexOwnerReply DecodeReply(byte[] bytes) => HexOwnerData.DecodeReply(bytes);
    }
    // Who asked, as the server knows them: a dedicated server holds a connected player's character as a ZDO, not as a GameObject.
    sealed class Actor
    {
        internal ZDOID Id; internal long Profile; internal Vector3 Position;
    }
    sealed class Sent
    {
        internal Task<OperationResult<HexOwnerReply>> Task = null!;
        internal int Size;
    }
    sealed class Work
    {
        internal AuthorityContext Context = null!;
        internal HexOwnerAction Action;
        internal Actor Actor = null!;
        internal ZNet Net = null!;
        internal ZNetPeer? Peer;
        internal ZRpc? Rpc;
        internal object? Socket;
        internal string Platform = "";
        internal string Token = System.Guid.NewGuid().ToString("N");
        internal HexOwnerPlan Plan = null!;
        internal HexOwnerTally Tally = null!;
        internal Vector3 Center;
        internal List<Sent> Sent = new();
        internal bool Dispatched, Complete, Uncertain;
        internal float Started = Time.realtimeSinceStartup;
        internal float Radius;
    }
    static string OperationFor(HexOwnerAction action) => action == HexOwnerAction.Kill ? HexKillData.OperationId
        : action == HexOwnerAction.Drops ? HexKillData.DropsOperationId : HexKillData.TameOperationId;
    static string Code(HexOwnerAction action, string kind) =>
        (action == HexOwnerAction.Kill ? "Kill" : action == HexOwnerAction.Drops ? "Drops" : "Tame") + kind;
    internal static void Initialize(ProductRegistration registration, ManualLogSource logger)
    {
        log = logger;
        var version = HexKillData.Version;
        operations[0] = CruciblePlugin.Services.Operations.Register(registration, new OperationDescriptor(HexKillData.OperationId, version),
            new Codec(), (context, request) => Start(context, request, HexOwnerAction.Kill), Recover);
        operations[1] = CruciblePlugin.Services.Operations.Register(registration, new OperationDescriptor(HexKillData.DropsOperationId, version),
            new Codec(), (context, request) => Start(context, request, HexOwnerAction.Drops), Recover);
        operations[2] = CruciblePlugin.Services.Operations.Register(registration, new OperationDescriptor(HexKillData.TameOperationId, version),
            new Codec(), (context, request) => Start(context, request, HexOwnerAction.Tame), Recover);
        operations[3] = CruciblePlugin.Services.Operations.Register(registration, new OperationDescriptor(HexKillData.EventOperationId, version),
            new EventCodec(), StopEvent, RecoverEvent);
        ownerCommand = CruciblePlugin.Services.PeerCommands.Register(registration, new OperationDescriptor(HexOwnerData.CommandId, HexOwnerData.Version),
            new OwnerCodec(), ApplyOwner);
    }

    // Server side.
    static bool Finite(Vector3 value) => !float.IsNaN(value.x) && !float.IsInfinity(value.x) && !float.IsNaN(value.y)
        && !float.IsInfinity(value.y) && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
    static bool Current(AuthorityContext context, out Actor? actor, out ZNetPeer? peer)
    {
        actor = null; peer = null;
        var net = ZNet.instance; var session = CruciblePlugin.Session;
        if (!Plugin.Ready || !Plugin.Active || Plugin.Settings?.Enabled.Value != true || net == null || !net.IsServer()
            || !MeadworksAuthority.ProductActive(Plugin.Guid, out _) || !session.AuthorityReady || !session.IsAuthority || context.Session.Generation != session.Generation
            || context.Session.WorldId != session.WorldId) return false;
        if (context.IsLocal)
        {
            var local = Player.m_localPlayer;
            if (!HexMovement.Owned(local) || !Finite(local.transform.position) || !MeadworksMischief.Decide(Plugin.Guid, MischiefCategory.World).Allowed) return false;
            actor = new Actor { Id = local.m_nview.GetZDO().m_uid, Profile = local.GetPlayerID(), Position = local.transform.position };
            return true;
        }
        if (!long.TryParse(context.SenderId, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var uid)) return false;
        peer = net.GetPeers().FirstOrDefault(p => p != null && p.IsReady() && p.m_uid == uid && p.m_rpc != null && p.m_rpc.IsConnected());
        var platform = peer?.m_socket?.GetHostName();
        if (peer == null || string.IsNullOrWhiteSpace(platform) || ZDOMan.instance == null) return false;
        var decision = MeadworksMischief.DecideForPeer(peer.m_rpc, MischiefCategory.World);
        if (!HexToolAccess.Allows(context.IsAdmin && net.IsAdmin(platform), AdminPermissionLink.Has(platform, "world"), decision.Allowed, decision.Reason == ConfigBlockReason.NotAdmin, true)) return false;
        var zdo = ZDOMan.instance.GetZDO(peer.m_characterID);
        if (zdo == null || zdo.m_uid != peer.m_characterID || zdo.GetBool(ZDOVars.s_dead, false) || zdo.GetOwner() != peer.m_uid) return false;
        var profile = zdo.GetLong(ZDOVars.s_playerID, 0L);
        var position = ZNetScene.instance?.FindInstance(zdo)?.transform.position ?? zdo.GetPosition();
        if (profile == 0 || !Finite(position) || long.TryParse(context.ActorId, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var claimed) && claimed != profile) return false;
        actor = new Actor { Id = zdo.m_uid, Profile = profile, Position = position };
        return true;
    }
    enum Kind { None, Creature, Spawner, Drop }
    static Kind Classify(GameObject? prefab, HexOwnerAction action)
    {
        if (prefab == null) return Kind.None;
        if (action == HexOwnerAction.Drops) return prefab.GetComponent<ItemDrop>() != null ? Kind.Drop : Kind.None;
        if (prefab.GetComponent<Piece>() != null || prefab.GetComponent<Player>() != null) return Kind.None;
        if (prefab.GetComponent<Character>() != null) return Kind.Creature;
        return action == HexOwnerAction.Tame ? Kind.None
            : prefab.GetComponent<SpawnArea>() != null && prefab.GetComponent<Destructible>() != null ? Kind.Spawner : Kind.None;
    }
    // Coarse and ZDO only: the owner re-checks every target on its live instance.
    static List<HexOwnedTarget> Scan(HexOwnerAction action, Vector3 center, float radius)
    {
        var found = new List<ZDO>(); var targets = new List<HexOwnedTarget>();
        var manager = ZDOMan.instance; var scene = ZNetScene.instance;
        if (manager == null || scene == null || !HexOwnerData.ValidRadius(radius)) return targets;
        var zone = ZoneSystem.GetZone(center); var span = Mathf.CeilToInt(radius / 64f) + 1;
        var visited = new HashSet<ZoneSystem.SectorIndex>();
        for (var x = zone.x - span; x <= zone.x + span; x++)
            for (var y = zone.y - span; y <= zone.y + span; y++)
                if (x >= -256 && x < 256 && y >= -256 && y < 256) manager.FindObjects(new Vector2s(x, y), found, visited);
        var kinds = new Dictionary<int, Kind>();
        foreach (var zdo in found)
        {
            if (zdo == null) continue;
            var hash = zdo.GetPrefab();
            if (!kinds.TryGetValue(hash, out var kind)) kinds[hash] = kind = Classify(scene.GetPrefab(hash), action);
            if (kind == Kind.None) continue;
            var position = zdo.GetPosition();
            if (!HexOwnerData.Within(center.x, center.y, center.z, position.x, position.y, position.z, radius)) continue;
            if (kind == Kind.Creature && zdo.GetBool(ZDOVars.s_tamed, false)) continue;
            targets.Add(new HexOwnedTarget(zdo.GetOwner(), new HexTargetId(zdo.m_uid.UserID, zdo.m_uid.ID)));
        }
        return targets;
    }
    static void EnsureSession()
    {
        if (serverGeneration != CruciblePlugin.Session.Generation || serverNet != ZNet.instance)
        { works.Clear(); serverGeneration = CruciblePlugin.Session.Generation; serverNet = ZNet.instance; }
    }
    static OperationExecution<HexKillReply> StopEvent(AuthorityContext context, bool confirmed)
    {
        if (!confirmed || !Current(context, out _, out _)) return OperationExecution<HexKillReply>.Rejected("EventDenied");
        var system = RandEventSystem.instance;
        if (system == null) return OperationExecution<HexKillReply>.Rejected("EventUnavailable");
        context.MarkDecisionMade();
        if (system.GetCurrentRandomEvent() == null) return OperationExecution<HexKillReply>.Success(new HexKillReply(0, 0, 0));
        try { system.ResetRandomEvent(); }
        catch
        {
            log?.LogWarning("Hex stop-event did not finish; no retry will run.");
            return OperationExecution<HexKillReply>.RecoveryRequired();
        }
        return OperationExecution<HexKillReply>.Success(new HexKillReply(1, 1, 0));
    }
    static OperationExecution<HexKillReply> RecoverEvent(RequestIdentity identity) => OperationExecution<HexKillReply>.RecoveryRequired();
    static OperationExecution<HexKillReply> Start(AuthorityContext context, HexAreaRequest request, HexOwnerAction action)
    {
        EnsureSession();
        if (!Current(context, out var actor, out var peer)) return OperationExecution<HexKillReply>.Rejected(Code(action, "Denied"));
        if (!HexOwnerData.ValidRadius(request.Radius)) return OperationExecution<HexKillReply>.Rejected("RadiusInvalid");
        // Work the router abandoned (owner lost, admin gone, 10 s deadline) never reaches Finish; it must not lock the admin out for the session.
        // The operation deadline is 10 s, so anything older is over; its outcome stays uncertain and is never replayed.
        foreach (var old in works.Where(pair => !pair.Value.Complete && Time.realtimeSinceStartup - pair.Value.Started > 30f).Select(pair => pair.Key).ToArray()) works.Remove(old);
        if (works.Count >= 256) foreach (var old in works.Where(pair => pair.Value.Complete).Select(pair => pair.Key).Take(64).ToArray()) works.Remove(old);
        if (works.Count >= 256 || works.Values.Count(work => !work.Complete) >= 4 || works.Values.Any(work => !work.Complete && work.Context.ActorId == context.ActorId))
            return OperationExecution<HexKillReply>.Rejected(Code(action, "Busy"));
        HexOwnerPlan plan;
        try { plan = HexOwnerPlanner.Plan(Scan(action, actor!.Position, request.Radius), HexKillData.MaxTargets, HexOwnerData.MaxBatch); }
        catch { return OperationExecution<HexKillReply>.Rejected(Code(action, "Unavailable")); }
        if (plan.OverLimit) return OperationExecution<HexKillReply>.Rejected(Code(action, "Limit"));
        if (plan.Unowned > 0) log?.LogDebug("Hex remote " + action + ": " + plan.Unowned + " candidates have no owner (not loaded by any client) and are left alone.");
        var work = new Work { Context = context, Action = action, Actor = actor!, Net = ZNet.instance, Peer = peer, Rpc = peer?.m_rpc, Socket = peer?.m_socket,
            Platform = peer?.m_socket?.GetHostName() ?? "", Plan = plan, Tally = new HexOwnerTally(plan.Targets), Center = actor.Position, Radius = request.Radius };
        works.Add(context.Identity, work);
        context.MarkDecisionMade();
        return Poll(work);
    }
    static bool SameConnection(Work work, ZNetPeer? peer) => peer == work.Peer && (work.Context.IsLocal
        || peer != null && ReferenceEquals(peer.m_rpc, work.Rpc) && ReferenceEquals(peer.m_socket, work.Socket)
        && ReferenceEquals(peer.m_rpc.GetSocket(), work.Socket) && peer.m_socket.GetHostName() == work.Platform);
    static bool StillAllowed(Work work)
        => work.Net == ZNet.instance && Current(work.Context, out var actor, out var peer) && SameConnection(work, peer)
            && actor!.Id == work.Actor.Id && actor.Profile == work.Actor.Profile;
    static OperationExecution<HexKillReply> Poll(Work work)
    {
        if (work.Uncertain) return OperationExecution<HexKillReply>.RecoveryRequired();
        if (work.Complete) return Finish(work);
        try
        {
            if (!work.Dispatched)
            {
                // The one permission check before anything leaves the server; after this the owners hold already-approved batches.
                if (!StillAllowed(work)) return Finish(work);
                work.Dispatched = true;
                var index = 0;
                foreach (var batch in work.Plan.Batches)
                {
                    var command = new HexOwnerCommand(work.Action, work.Center.x, work.Center.y, work.Center.z, work.Radius, batch.Ids);
                    work.Sent.Add(new Sent { Size = batch.Ids.Count, Task = CruciblePlugin.PeerCommandClient.SendAsync<HexOwnerCommand, HexOwnerReply>(
                        batch.Owner.ToString(CultureInfo.InvariantCulture), HexOwnerData.CommandId, work.Token + ":" + index++, command, CancellationToken.None) });
                }
            }
            if (work.Sent.Any(sent => !sent.Task.IsCompleted)) return OperationExecution<HexKillReply>.Pending(() => Poll(work));
            foreach (var sent in work.Sent)
            {
                // A refusal before dispatch (owner gone, no Hex on the owner, owner declined) ran nothing; any other end after dispatch is unknown.
                if (sent.Task.Status == TaskStatus.RanToCompletion && sent.Task.Result.State == OperationState.Succeeded)
                    work.Tally.Record(sent.Size, HexBatchState.Applied, sent.Task.Result.Reply);
                else if (sent.Task.Status == TaskStatus.RanToCompletion && sent.Task.Result.State == OperationState.Rejected)
                    work.Tally.Record(sent.Size, HexBatchState.Refused, default);
                else work.Tally.Record(sent.Size, HexBatchState.Unknown, default);
            }
            work.Sent.Clear();
            if (work.Tally.Uncertain) throw new InvalidOperationException("Owner outcome uncertain.");
        }
        catch
        {
            work.Uncertain = true; work.Complete = true;
            log?.LogWarning("Hex remote " + work.Action + " dispatch is uncertain; native state preserved and no retry will run.");
            return OperationExecution<HexKillReply>.RecoveryRequired();
        }
        return Finish(work);
    }
    static OperationExecution<HexKillReply> Finish(Work work)
    {
        work.Complete = true;
        // Without a dispatch nothing ran, so every target is skipped (permission or connection changed after approval).
        var reply = work.Dispatched ? work.Tally.Reply : new HexKillReply(work.Plan.Targets, 0, work.Plan.Targets);
        return OperationExecution<HexKillReply>.Success(reply);
    }
    static OperationExecution<HexKillReply> Recover(RequestIdentity identity)
        => works.TryGetValue(identity, out var work) && !work.Uncertain && (work.Complete || !work.Dispatched) ? Finish(work) : OperationExecution<HexKillReply>.RecoveryRequired();

    // Owner side.
    static OperationExecution<HexOwnerReply> ApplyOwner(PeerCommandContext context, HexOwnerCommand command)
    {
        if (!context.IsLocal && !context.ServerConnectionVerified || !Plugin.Ready || !Plugin.Active || Plugin.Settings?.Enabled.Value != true
            || !MeadworksAuthority.ProductActive(Plugin.Guid, out _) || ZDOMan.instance == null || ZNetScene.instance == null)
            return OperationExecution<HexOwnerReply>.Rejected("OwnerUnavailable");
        context.MarkDecisionMade();
        var applied = 0;
        try
        {
            foreach (var id in command.Targets)
                if (ApplyOne(command, new ZDOID(id.User, id.Id))) applied++;
        }
        catch
        {
            log?.LogWarning("Hex owner-side " + command.Action + " stopped part way; native state preserved and no retry will run.");
            return OperationExecution<HexOwnerReply>.RecoveryRequired();
        }
        return OperationExecution<HexOwnerReply>.Success(new HexOwnerReply(applied, command.Targets.Count - applied));
    }
    static bool InRange(HexOwnerCommand command, Vector3 position)
        => HexOwnerData.Within(command.X, command.Y, command.Z, position.x, position.y, position.z, command.Radius);
    static bool ApplyOne(HexOwnerCommand command, ZDOID id)
    {
        var zdo = ZDOMan.instance.GetZDO(id);
        if (zdo == null || !zdo.IsOwner()) return false;
        var view = ZNetScene.instance.FindInstance(zdo);
        if (view == null || !view.IsValid() || !view.IsOwner() || view.GetZDO() != zdo) return false;
        if (command.Action == HexOwnerAction.Drops)
        {
            // The native removedrops selection: loose item drops that are not pieces, and fish only once out of the water.
            var drop = view.GetComponent<ItemDrop>();
            if (drop == null || drop.IsPiece() || !InRange(command, drop.transform.position)) return false;
            var fish = drop.GetComponent<Fish>();
            if (fish != null && !fish.IsOutOfWater()) return false;
            view.Destroy();
            return true;
        }
        var creature = view.GetComponent<Character>();
        if (creature != null)
        {
            if (HexOwnerData.SkipCreature(command.Action, creature.IsPlayer(), creature.IsTamed()) || creature.GetComponent<Piece>() != null
                || creature.IsDead() || creature.m_nview != view || !InRange(command, creature.transform.position)) return false;
            if (command.Action == HexOwnerAction.Tame) { creature.SetTamed(true); return true; }
            // Native Damage routes the hit to the actual native owner; no ownership or health rewrite.
            creature.Damage(new HitData(1E+10f));
            return true;
        }
        if (command.Action == HexOwnerAction.Tame) return false;
        var spawner = view.GetComponent<SpawnArea>(); var breakable = view.GetComponent<Destructible>();
        if (spawner == null || breakable == null || !spawner.gameObject.activeInHierarchy || spawner.GetComponent<Character>() != null
            || spawner.GetComponent<Piece>() != null || !InRange(command, spawner.transform.position)) return false;
        breakable.Damage(new HitData(1E+10f));
        return true;
    }

    // Requesting client.
    static string Key(HexOwnerAction action, string suffix) => (action == HexOwnerAction.Kill ? "debug_kill_" : action == HexOwnerAction.Drops ? "debug_drops_" : "debug_tame_") + suffix;
    static bool Begin(out string denied)
    {
        denied = "";
        if (pending != null) { denied = "busy"; return false; }
        if (!HexDebug.Tools || !HexGrantHint.WorldAllowed()) { denied = "denied"; return false; }
        clientActor = Player.m_localPlayer; clientProfile = clientActor.GetPlayerID(); clientCharacter = clientActor.m_nview.GetZDO().m_uid;
        clientNet = ZNet.instance; clientGeneration = CruciblePlugin.Session.Generation;
        return true;
    }
    internal static string Request(HexOwnerAction action, float radius)
    {
        if (!Begin(out var denied)) return denied == "busy" ? HexText.Text(Key(action, "busy")) : HexText.Text("actions_denied");
        if (!HexOwnerData.ValidRadius(radius)) { ResetClient(); return HexText.Text("debug_radius_invalid"); }
        pendingAction = action; pendingEvent = false;
        try
        {
            cancel = new CancellationTokenSource();
            pending = CruciblePlugin.RequestClient.InvokeAsync<HexAreaRequest, HexKillReply>(OperationFor(action), new HexAreaRequest(radius), cancel.Token);
            return HexText.Text(Key(action, "waiting"));
        }
        catch { ResetClient(); return HexText.Text("debug_native_uncertain"); }
    }
    internal static string RequestEvent()
    {
        if (!Begin(out var denied)) return denied == "busy" ? HexText.Text("debug_event_busy") : HexText.Text("actions_denied");
        pendingEvent = true;
        try
        {
            cancel = new CancellationTokenSource();
            pending = CruciblePlugin.RequestClient.InvokeAsync<bool, HexKillReply>(HexKillData.EventOperationId, true, cancel.Token);
            return HexText.Text("debug_event_waiting");
        }
        catch { ResetClient(); return HexText.Text("debug_native_uncertain"); }
    }
    internal static void Tick()
    {
        EnsureSession();
        if (pending == null) return;
        if (!HexDebug.Tools || !HexGrantHint.WorldAllowed() || clientActor != Player.m_localPlayer
            || clientNet != ZNet.instance || clientGeneration != CruciblePlugin.Session.Generation
            || clientActor == null || clientActor.GetPlayerID() != clientProfile || clientActor.m_nview.GetZDO().m_uid != clientCharacter) { ResetClient(); return; }
        if (!pending.IsCompleted) return;
        string message;
        try
        {
            var result = pending.GetAwaiter().GetResult();
            if (pendingEvent)
                message = result.State == OperationState.Succeeded && result.Identity != null
                    ? HexText.Text(result.Reply.Submitted > 0 ? "debug_event_submitted" : "debug_event_none")
                    : result.State == OperationState.Rejected ? HexText.Text("debug_event_refused") : HexText.Text("debug_native_uncertain");
            else
                message = result.State == OperationState.Succeeded && result.Identity != null
                    ? HexText.Format(Key(pendingAction, "submitted"), result.Reply.Submitted, result.Reply.Skipped)
                    : result.State == OperationState.Rejected ? HexText.Text(result.Code.EndsWith("Limit", StringComparison.Ordinal) ? "debug_remote_limit" : result.Code == "RadiusInvalid" ? "debug_radius_invalid" : Key(pendingAction, "refused"))
                    : HexText.Text("debug_native_uncertain");
        }
        catch { message = HexText.Text("debug_native_uncertain"); }
        ResetClient();
        if (MessageHud.instance != null) MessageHud.instance.ShowMessage(MessageHud.MessageType.Center, message);
    }
    static void ResetClient()
    {
        var owned = cancel; cancel = null;
        try { owned?.Cancel(); } catch { } finally { owned?.Dispose(); }
        if (pending != null) _ = pending.ContinueWith(task => { _ = task.Exception; }, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
        pending = null; pendingEvent = false; clientActor = null; clientProfile = 0; clientCharacter = ZDOID.None; clientNet = null; clientGeneration = -1;
    }
    internal static void Shutdown()
    {
        ResetClient();
        for (var i = 0; i < operations.Length; i++) { operations[i]?.Dispose(); operations[i] = null; }
        ownerCommand?.Dispose(); ownerCommand = null;
        works.Clear(); log = null; serverNet = null; serverGeneration = -1;
    }
}
