// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: admin teleport admission and target consent over existing Crucible Operations/PeerCommands.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Thing.Admin;
using ExtraPotions.Thing.Commands;
using ExtraPotions.Thing.Config;
using ExtraPotions.Thing.Moderation;
using UnityEngine;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Thing.Runtime
{
    internal static class ThingTeleportCommands
    {
        internal const string OperationId = "meadworks.thing.teleport", OwnerId = "meadworks.thing.teleport_owner";
        static IDisposable? operation, owner;
        static long generation = -1;
        sealed class Grant
        {
            internal string Token = "", Verb = "", CallerId = "", CallerName = "", CallerCharacter = "", TargetCharacter = "";
            internal ZNetPeer? Caller;
            internal OnlinePlayer Target;
            internal bool Local, Consent;
            internal long Generation;
            internal float Expires;
        }
        static readonly Dictionary<string, Grant> grants = new Dictionary<string, Grant>(StringComparer.Ordinal);
        static readonly List<Task<OperationResult<IReadOnlyList<string>>>> pending = new List<Task<OperationResult<IReadOnlyList<string>>>>();
        sealed class Codec : IOperationCodec<IReadOnlyList<string>, IReadOnlyList<string>>
        {
            public byte[] EncodeRequest(IReadOnlyList<string> value) => RemoteAdmin.EncodeRequest(value);
            public IReadOnlyList<string> DecodeRequest(byte[] bytes) => RemoteAdmin.DecodeRequest(bytes);
            public byte[] EncodeReply(IReadOnlyList<string> value) => RemoteAdmin.EncodeReply(value);
            public IReadOnlyList<string> DecodeReply(byte[] bytes) => RemoteAdmin.DecodeReply(bytes);
        }
        internal static void Initialize(ProductRegistration registration)
        {
            operation = CruciblePlugin.Services.Operations.Register(registration, new OperationDescriptor(OperationId, new System.Version(1, 0)), new Codec(), Apply);
            owner = CruciblePlugin.Services.PeerCommands.Register(registration, new OperationDescriptor(OwnerId, new System.Version(1, 0)), new Codec(), ThingTeleportOwner.Apply);
            generation = CruciblePlugin.Session.Generation;
        }
        internal static bool Run(string[] args)
        {
            if (args.Length == 0 || (!args[0].Equals("goto", StringComparison.OrdinalIgnoreCase) && !args[0].Equals("bring", StringComparison.OrdinalIgnoreCase))) return false;
            var parsed = CommandParser.Parse(args);
            if (args.Length != 2 || parsed.Kind != CommandKind.Action) { Print("teleport_usage"); return true; }
            Submit(new[] { args[0].ToLowerInvariant(), parsed.Target }); return true;
        }
        internal static void Submit(string[] args)
        {
            if (!Active || pending.Count >= 16) { Print("teleport_unavailable"); return; }
            try { pending.Add(CruciblePlugin.RequestClient.InvokeAsync<IReadOnlyList<string>, IReadOnlyList<string>>(OperationId, args, CancellationToken.None)); }
            catch { Print("teleport_unknown"); }
        }
        static bool Active => Plugin.Ready && CruciblePlugin.IsReady && CruciblePlugin.Session.AuthorityReady && ThingModules.Active(Integration.ThingModule.WorldTools);
        static ZNetPeer? Sender(AuthorityContext context) => !context.IsLocal && long.TryParse(context.SenderId, out var uid)
            ? ZNet.instance?.GetPeers().FirstOrDefault(p => p != null && p.IsReady() && p.m_uid == uid && p.m_rpc != null && p.m_rpc.IsConnected() && p.m_socket != null) : null;
        static bool Character(ZNetPeer? peer, bool local, out ZDO? zdo)
        {
            zdo = local ? Player.m_localPlayer?.GetComponent<ZNetView>()?.GetZDO() : peer == null ? null : ZDOMan.instance?.GetZDO(peer.m_characterID);
            if (zdo == null || zdo.m_uid.IsNone() || !Valid(zdo.GetPosition()) || !local && zdo.GetOwner() != peer!.m_uid) return false;
            var player = local ? Player.m_localPlayer : ZNetScene.instance?.FindInstance(peer!.m_characterID)?.GetComponent<Player>();
            return player == null || !player.IsDead();
        }
        static bool Authorize(Grant grant, out ZDO? caller, out ZDO? target)
        {
            caller = target = null; var net = ZNet.instance;
            var action = grant.Verb == "goto" ? ThingAction.TeleportTo : ThingAction.Bring;
            if (!Active || !ThingServer.CurrentAuthority || net == null || grant.Generation != CruciblePlugin.Session.Generation) return false;
            if (!grant.Local && (grant.Caller == null || !grant.Caller.IsReady() || grant.Caller.m_rpc == null || !grant.Caller.m_rpc.IsConnected()
                || !net.GetPeers().Any(p => ReferenceEquals(p, grant.Caller)) || grant.Caller.m_socket?.GetHostName() != grant.CallerId
                || !net.IsAdmin(grant.CallerId) && !AdminPermissions.Covers(ThingAccess.Of(grant.CallerId), action))) return false;
            if (!grant.Target.Peer.IsReady() || grant.Target.Peer.m_rpc == null || !grant.Target.Peer.m_rpc.IsConnected() || !net.GetPeers().Any(p => ReferenceEquals(p, grant.Target.Peer))
                || grant.Target.Peer.m_socket?.GetHostName() != grant.Target.Actor.Id) return false;
            var role = net.IsDedicated() ? ThingRole.DedicatedServer : ThingRole.Host;
            var allowed = grant.Local || net.IsAdmin(grant.CallerId);
            if (!ActionCatalog.Visible(action, ThingModules.StepAside.AdminToolLoaded)
                || !AdminCheck.Permits(AdminCheck.Decide(action, new RequestFacts(role, grant.Local, allowed, false, granted: allowed ? AdminRight.None : ThingAccess.Of(grant.CallerId)), Plugin.Settings!.Rules()))) return false;
            if (!Character(grant.Caller, grant.Local, out caller) || !Character(grant.Target.Peer, false, out target)) return false;
            return caller!.m_uid.ToString() == grant.CallerCharacter && target!.m_uid.ToString() == grant.TargetCharacter;
        }
        static OperationExecution<IReadOnlyList<string>> Apply(AuthorityContext context, IReadOnlyList<string> request)
        {
            if (!Active || !ThingServer.CurrentAuthority) return Reply("teleport_unavailable");
            var incoming = Sender(context);
            if (request.Count == 3 && request[0] == "respond")
            {
                if (incoming == null || !grants.TryGetValue(request[1], out var grant) || !ReferenceEquals(incoming, grant.Target.Peer)
                    || incoming.m_socket.GetHostName() != grant.Target.Actor.Id || incoming.m_characterID.ToString() != grant.TargetCharacter
                    || grant.Generation != context.Session.Generation || Time.realtimeSinceStartup >= grant.Expires || (request[2] != "yes" && request[2] != "no")) return Reply("teleport_expired");
                grants.Remove(grant.Token);
                if (request[2] == "no") { Notify(grant, "teleport_declined"); return Reply("teleport_declined"); }
                grant.Consent = true;
                if (!Authorize(grant, out _, out _)) { Notify(grant, "teleport_expired"); return Reply("teleport_expired"); }
                return Move(context, grant);
            }
            if (request.Count != 2 || (request[0] != "goto" && request[0] != "bring") || CommandParser.Parse(request).Kind != CommandKind.Action) return Reply("teleport_usage");
            var id = context.IsLocal ? "host" : incoming?.m_socket.GetHostName() ?? "";
            var allowedCaller = context.IsLocal || context.IsAdmin && incoming != null && ZNet.instance.IsAdmin(id);
            var action = request[0] == "goto" ? ThingAction.TeleportTo : ThingAction.Bring;
            if ((!context.IsLocal && incoming == null) || (!allowedCaller && !AdminPermissions.Covers(ThingAccess.Of(id), action))) return Reply("refused_not_admin");
            var online = ThingServer.Online(); var found = PlayerMatch.Find(request[1].Trim(), online.Select(p => p.Actor));
            if (!found.HasValue) return Reply("teleport_unavailable");
            var selected = online.First(p => p.Actor.Id == found.Value.Id);
            if (ReferenceEquals(selected.Peer, incoming) || !Character(incoming, context.IsLocal, out var caller) || !Character(selected.Peer, false, out var target)) return Reply("teleport_unavailable");
            var next = new Grant { Token = Guid.NewGuid().ToString("N"), Verb = request[0], CallerId = id, CallerName = incoming?.m_playerName ?? Player.m_localPlayer?.GetPlayerName() ?? "host",
                Caller = incoming, Local = context.IsLocal, Target = selected, Generation = context.Session.Generation,
                CallerCharacter = caller!.m_uid.ToString(), TargetCharacter = target!.m_uid.ToString(), Expires = Time.realtimeSinceStartup + 30 };
            if (!Authorize(next, out _, out _)) return Reply("teleport_unavailable");
            if (!Plugin.Settings!.Get(ThingSettings.Moderation.TeleportNeedsTargetConfirmation)) return Move(context, next);
            if (grants.Count >= 32 || grants.Values.Any(g => ReferenceEquals(g.Target.Peer, selected.Peer))) return Reply("teleport_busy");
            context.MarkDecisionMade();
            grants.Add(next.Token, next);
            var task = SendOwner(selected.Peer.m_uid, next.Token, new[] { "prompt", next.Token, next.Verb, Reasons.Normalize(next.CallerName), next.TargetCharacter });
            return PromptResult(task, next);
        }
        static OperationExecution<IReadOnlyList<string>> PromptResult(Task<OperationResult<IReadOnlyList<string>>> task, Grant grant)
        {
            if (!task.IsCompleted) return OperationExecution<IReadOnlyList<string>>.Pending(() => PromptResult(task, grant));
            if (task.Status != TaskStatus.RanToCompletion || task.Result.State != OperationState.Succeeded || task.Result.Reply.Count != 1 || task.Result.Reply[0] != "shown")
            { var ignored = task.Exception; grants.Remove(grant.Token); return Reply("teleport_unknown"); }
            return Reply("teleport_waiting");
        }
        static OperationExecution<IReadOnlyList<string>> Move(AuthorityContext context, Grant grant)
        {
            if (!Authorize(grant, out var caller, out var target) || Plugin.Settings!.Get(ThingSettings.Moderation.TeleportNeedsTargetConfirmation) && !grant.Consent) return Reply("teleport_expired");
            var destination = (grant.Verb == "goto" ? target : caller)!.GetPosition();
            var character = grant.Verb == "goto" ? grant.CallerCharacter : grant.TargetCharacter;
            var mover = grant.Verb == "goto" ? grant.Caller?.m_uid ?? ZNet.GetUID() : grant.Target.Peer.m_uid;
            context.MarkDecisionMade();
            var task = SendOwner(mover, grant.Token + ":move", new[] { "move", character, F(destination.x), F(destination.y), F(destination.z) });
            return MoveResult(task, grant);
        }
        static OperationExecution<IReadOnlyList<string>> MoveResult(Task<OperationResult<IReadOnlyList<string>>> task, Grant grant)
        {
            if (!task.IsCompleted) return OperationExecution<IReadOnlyList<string>>.Pending(() => MoveResult(task, grant));
            var key = "teleport_unknown";
            if (task.Status == TaskStatus.RanToCompletion && task.Result.State == OperationState.Succeeded && task.Result.Reply.Count == 1)
                key = task.Result.Reply[0] == "accepted" ? "teleport_accepted" : "teleport_unavailable";
            var ignored = task.Exception;
            ThingServer.Audit(new Actor(grant.CallerId, grant.CallerName), grant.Verb == "goto" ? ThingAction.TeleportTo : ThingAction.Bring, grant.Target.Actor,
                key == "teleport_accepted" ? AuditOutcome.Done : AuditOutcome.Failed, key + "; native acceptance only, arrival not acknowledged");
            Notify(grant, key); return Reply(key);
        }
        static Task<OperationResult<IReadOnlyList<string>>> SendOwner(long peer, string key, string[] request) => CruciblePlugin.PeerCommandClient.SendAsync<IReadOnlyList<string>, IReadOnlyList<string>>(
            peer.ToString(CultureInfo.InvariantCulture), OwnerId, key, request, CancellationToken.None);
        static void Notify(Grant grant, string key)
        { try { ThingNotices.Send(grant.Caller?.m_uid ?? ZNet.GetUID(), key); } catch { /* Completion remains inspectable in the authority audit. */ } }
        static OperationExecution<IReadOnlyList<string>> Reply(string key) => OperationExecution<IReadOnlyList<string>>.Success(new[] { ThingText.Text(key) });
        internal static bool Valid(Vector3 p) => !float.IsNaN(p.x) && !float.IsNaN(p.y) && !float.IsNaN(p.z) && !float.IsInfinity(p.x) && !float.IsInfinity(p.y) && !float.IsInfinity(p.z) && Mathf.Abs(p.x) < 12000 && Mathf.Abs(p.z) < 12000;
        static string F(float value) => value.ToString("R", CultureInfo.InvariantCulture);
        static void Print(string key) => global::Console.instance?.Print(ThingText.Text(key));
        internal static void Tick()
        {
            if (generation != CruciblePlugin.Session.Generation || !Active)
            { grants.Clear(); foreach (var task in pending) Observe(task); pending.Clear(); ThingTeleportOwner.Reset(); generation = CruciblePlugin.Session.Generation; return; }
            if (grants.Count > 0) foreach (var pair in grants.ToArray()) if (Time.realtimeSinceStartup >= pair.Value.Expires || !Authorize(pair.Value, out _, out _))
            {
                grants.Remove(pair.Key);
                try { Observe(SendOwner(pair.Value.Target.Peer.m_uid, pair.Key + ":cancel", new[] { "cancel", pair.Key })); } catch { }
            }
            if (pending.Count > 0) foreach (var task in pending.ToArray()) if (task.IsCompleted)
            {
                pending.Remove(task);
                if (task.Status == TaskStatus.RanToCompletion && task.Result.State == OperationState.Succeeded)
                    foreach (var line in task.Result.Reply) { global::Console.instance?.Print(line); MessageHud.instance?.ShowMessage(MessageHud.MessageType.Center, line); }
                else { var ignored = task.Exception; Print("teleport_unknown"); }
            }
            ThingTeleportOwner.Tick();
        }
        static void Observe(Task task) => task.ContinueWith(done => { var ignored = done.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
        internal static void Shutdown() { operation?.Dispose(); owner?.Dispose(); operation = owner = null; foreach (var task in pending) Observe(task); pending.Clear(); grants.Clear(); ThingTeleportOwner.Reset(); generation = -1; }
    }
}
