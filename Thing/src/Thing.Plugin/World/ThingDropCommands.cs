// Thing - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: bounded loose-drop confirmation and existing Crucible authorityâ†’owner command path.
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
using ExtraPotions.Thing.Integration;
using ExtraPotions.Thing.Moderation;
using ExtraPotions.Thing.WorldAdmin;
using UnityEngine;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Thing.Runtime
{
    public static class ThingDropCommands
    {
        const string OperationId = "meadworks.thing.drops", OwnerId = "meadworks.thing.drops_owner", Marker = "THING_DROPS_CONFIRM";
        const int BatchSize = 48;
        static IDisposable? operation, ownerCommand;
        static readonly Dictionary<string, Preview> previews = new Dictionary<string, Preview>(StringComparer.Ordinal);
        static readonly List<Task<OperationResult<IReadOnlyList<string>>>> pending = new List<Task<OperationResult<IReadOnlyList<string>>>>();
        static readonly HashSet<Task<OperationResult<IReadOnlyList<string>>>> uiPending = new HashSet<Task<OperationResult<IReadOnlyList<string>>>>();
        public static event Action<IReadOnlyList<string>>? UiReply;
        static string token = "";
        static long generation = -1;

        sealed class Preview
        {
            public Vector3 Center;
            public int Radius;
            public long PlayerId;
            public ZNetPeer? Peer;
            public string Token = "";
            public DateTime Expires;
            public long Generation;
            public HashSet<string> Targets = new HashSet<string>(StringComparer.Ordinal);
        }
        sealed class Caller
        {
            public Actor Actor;
            public ZNetPeer? Peer;
            public bool IsLocal, ContextAdmin;
            public long Generation;
        }
        sealed class Codec : IOperationCodec<IReadOnlyList<string>, IReadOnlyList<string>>
        {
            public byte[] EncodeRequest(IReadOnlyList<string> value) => RemoteAdmin.EncodeRequest(value);
            public IReadOnlyList<string> DecodeRequest(byte[] bytes) => RemoteAdmin.DecodeRequest(bytes);
            public byte[] EncodeReply(IReadOnlyList<string> value) => RemoteAdmin.EncodeReply(value);
            public IReadOnlyList<string> DecodeReply(byte[] bytes) => RemoteAdmin.DecodeReply(bytes);
        }

        public static void Initialize(ProductRegistration registration)
        {
            generation = CruciblePlugin.Session.Generation;
            operation = CruciblePlugin.Services.Operations.Register(registration, new OperationDescriptor(OperationId, new System.Version(1, 0)), new Codec(), Apply);
            ownerCommand = CruciblePlugin.Services.PeerCommands.Register(registration, new OperationDescriptor(OwnerId, new System.Version(1, 0)), new Codec(), ApplyOwner);
        }

        public static bool Run(string[] args, bool fromUi = false)
        {
            if (args.Length == 0 || !string.Equals(args[0], "drops", StringComparison.OrdinalIgnoreCase)) return false;
            if (operation == null || !ThingModules.Active(ThingModule.WorldTools)) { Print("refused_product_off"); return true; }
            if (args.Length == 1) Send(new[] { "preview" }, fromUi);
            else if (args.Length == 2 && args[1].Equals("confirm", StringComparison.OrdinalIgnoreCase)) Send(new[] { "confirm", token }, fromUi);
            else if (args.Length == 2 && args[1].Equals("cancel", StringComparison.OrdinalIgnoreCase)) { token = ""; Send(new[] { "cancel" }, fromUi); }
            else Print("drops_usage");
            return true;
        }

        static void Send(string[] request, bool fromUi = false)
        { var task = CruciblePlugin.RequestClient.InvokeAsync<IReadOnlyList<string>, IReadOnlyList<string>>(OperationId, request, CancellationToken.None); pending.Add(task); if (fromUi) uiPending.Add(task); }
        static OperationExecution<IReadOnlyList<string>> Message(string key, params object[] values) => OperationExecution<IReadOnlyList<string>>.Success(new[] { ThingText.Format(key, values) });

        static OperationExecution<IReadOnlyList<string>> Apply(AuthorityContext context, IReadOnlyList<string> request)
        {
            var net = ZNet.instance;
            if (!net || !net.IsServer() || !ThingServer.Active || !ThingModules.Active(ThingModule.WorldTools) || Plugin.Settings == null) return Message("refused_product_off");
            var peer = !context.IsLocal && long.TryParse(context.SenderId, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var uid)
                ? net.GetPeers().FirstOrDefault(p => p != null && p.IsReady() && p.m_uid == uid) : null;
            var platform = context.IsLocal ? "host" : peer?.m_socket?.GetHostName() ?? "";
            var caller = new Caller { Actor = new Actor(platform, peer?.m_playerName ?? ""), Peer = peer, IsLocal = context.IsLocal,
                ContextAdmin = context.IsAdmin, Generation = context.Session.Generation };
            var refusal = Authorize(caller, null, out var player);
            if (refusal != null) return Message(refusal);
            var radius = Plugin.Settings.Get(ThingSettings.World.RemoveDropsRadius);
            if (request.Count == 1 && request[0] == "cancel") { previews.Remove(platform); context.MarkDecisionMade(); return Message("drops_canceled"); }
            if (request.Count == 1 && request[0] == "preview")
            {
                var center = player.transform.position;
                if (!Finite(center)) return Message("drops_unavailable");
                var targets = ThingDrops.Loaded(center, radius);
                var preview = new Preview { Center = center, Radius = radius, PlayerId = player.GetPlayerID(), Peer = peer, Token = Guid.NewGuid().ToString("N"),
                    Expires = DateTime.UtcNow.AddSeconds(20), Generation = caller.Generation, Targets = new HashSet<string>(targets.Select(t => t.Approval), StringComparer.Ordinal) };
                previews[platform] = preview;
                context.MarkDecisionMade();
                return OperationExecution<IReadOnlyList<string>>.Success(new[] { Marker, preview.Token, ThingText.Format("drops_preview", targets.Count, radius) });
            }
            if (request.Count != 2 || request[0] != "confirm" || !previews.TryGetValue(platform, out var approved) || request[1].Length == 0 || request[1] != approved.Token) return Message("drops_expired");
            previews.Remove(platform);
            if (DateTime.UtcNow > approved.Expires) return Message("drops_expired");
            refusal = Authorize(caller, approved, out player);
            if (refusal != null) return Message(refusal);
            var current = ThingDrops.Loaded(approved.Center, radius).Where(t => approved.Targets.Contains(t.Approval)).ToArray();
            var batches = new Queue<(long Owner, string[] Request)>();
            foreach (var group in current.GroupBy(t => t.Owner))
            {
                var ids = group.Select(t => t.Approval).Distinct(StringComparer.Ordinal).ToArray();
                for (var i = 0; i < ids.Length; i += BatchSize)
                    batches.Enqueue((group.Key, new[] { F(approved.Center.x), F(approved.Center.y), F(approved.Center.z), radius.ToString(CultureInfo.InvariantCulture) }.Concat(ids.Skip(i).Take(BatchSize)).ToArray()));
            }
            context.MarkDecisionMade();
            caller.Actor = new Actor(platform, player!.GetPlayerName());
            return Next(batches, caller, approved, 0);
        }

        static string? Authorize(Caller caller, Preview? expected, out Player? player)
        {
            player = null;
            var net = ZNet.instance;
            if (!net || !net.IsServer() || !ThingServer.Active || !ThingModules.Active(ThingModule.WorldTools) || Plugin.Settings == null
                || caller.Generation != CruciblePlugin.Session.Generation) return "refused_product_off";
            if (caller.Actor.Id.Length == 0) return "drops_unavailable";
            if (caller.IsLocal) player = Player.m_localPlayer;
            else
            {
                var peer = caller.Peer;
                if (peer == null || !peer.IsReady() || peer.m_rpc == null || !peer.m_rpc.IsConnected() || !net.GetPeers().Any(p => ReferenceEquals(p, peer)) || peer.m_socket == null
                    || !string.Equals(peer.m_socket.GetHostName(), caller.Actor.Id, StringComparison.Ordinal)) return "drops_unavailable";
                player = ZNetScene.instance?.FindInstance(peer.m_characterID)?.GetComponent<Player>();
            }
            if (!player || player.GetPlayerID() == 0 || !Finite(player.transform.position)) return "drops_unavailable";
            var role = net.IsDedicated() ? ThingRole.DedicatedServer : net.GetPeers().Count > 0 ? ThingRole.Host : ThingRole.SinglePlayer;
            var admin = caller.IsLocal || (caller.ContextAdmin && net.IsAdmin(caller.Actor.Id));
            var verdict = AdminCheck.Decide(ThingAction.RemoveDrops, new RequestFacts(role, caller.IsLocal, admin, false, granted: admin ? AdminRight.None : ThingAccess.Of(caller.Actor.Id)), Plugin.Settings.Rules());
            if (!AdminCheck.Permits(verdict)) return AdminCheck.TextKey(verdict);
            if (expected != null && (expected.Generation != caller.Generation || !ReferenceEquals(caller.Peer, expected.Peer) || player.GetPlayerID() != expected.PlayerId
                || Plugin.Settings.Get(ThingSettings.World.RemoveDropsRadius) != expected.Radius || Vector3.Distance(player.transform.position, expected.Center) > 2)) return "drops_expired";
            return null;
        }

        static OperationExecution<IReadOnlyList<string>> Next(Queue<(long Owner, string[] Request)> batches, Caller caller, Preview approved, int removed)
        {
            if (batches.Count == 0)
            {
                ThingServer.Audit(caller.Actor, ThingAction.RemoveDrops, default, AuditOutcome.Done, "removed stacks=" + removed);
                return Message("drops_done", removed);
            }
            // Each batch is a fresh permission boundary; a multi-frame cleanup is not a continuing grant.
            var refusal = Authorize(caller, approved, out _);
            if (refusal != null)
            {
                if (removed > 0) return Recovery(caller.Actor, removed, "authorization changed: " + refusal);
                ThingServer.Audit(caller.Actor, ThingAction.RemoveDrops, default, AuditOutcome.Refused, "before removal: " + refusal);
                return Message(refusal);
            }
            var batch = batches.Dequeue();
            var task = CruciblePlugin.PeerCommandClient.SendAsync<IReadOnlyList<string>, IReadOnlyList<string>>(batch.Owner.ToString(CultureInfo.InvariantCulture), OwnerId,
                approved.Token + ":" + batches.Count, batch.Request, CancellationToken.None);
            return Poll(task, batches, caller, approved, removed, batch.Request.Length - 4);
        }

        static OperationExecution<IReadOnlyList<string>> Poll(Task<OperationResult<IReadOnlyList<string>>> task, Queue<(long Owner, string[] Request)> batches, Caller caller, Preview approved, int removed, int approvedCount)
        {
            if (!task.IsCompleted) return OperationExecution<IReadOnlyList<string>>.Pending(() => Poll(task, batches, caller, approved, removed, approvedCount));
            if (task.Status != TaskStatus.RanToCompletion || task.Result.State != OperationState.Succeeded || task.Result.Reply.Count != 2
                || !int.TryParse(task.Result.Reply[1], NumberStyles.None, CultureInfo.InvariantCulture, out var count))
                return Recovery(caller.Actor, removed, "owner outcome unknown");
            var receipt = DropReceipt.Decide(task.Result.Reply[0], count, approvedCount, removed);
            if (receipt == DropReceiptOutcome.Recovery)
            {
                var knownCount = count >= 0 && count <= approvedCount && (task.Result.Reply[0] == "ok" || task.Result.Reply[0] == "refused") ? count : 0;
                return Recovery(caller.Actor, removed + knownCount, "partial or invalid owner receipt");
            }
            removed += count;
            if (receipt == DropReceiptOutcome.Refused)
            {
                ThingServer.Audit(caller.Actor, ThingAction.RemoveDrops, default, AuditOutcome.Refused, "owner unavailable before removals");
                return Message("drops_unavailable");
            }
            return Next(batches, caller, approved, removed);
        }

        static OperationExecution<IReadOnlyList<string>> Recovery(Actor actor, int removed, string reason)
        {
            ThingServer.Audit(actor, ThingAction.RemoveDrops, default, AuditOutcome.Failed, reason + "; observed removed stacks=" + removed + "; no automatic retry");
            return OperationExecution<IReadOnlyList<string>>.RecoveryRequired();
        }

        static OperationExecution<IReadOnlyList<string>> ApplyOwner(PeerCommandContext context, IReadOnlyList<string> request)
        {
            if ((!context.IsLocal && !context.ServerConnectionVerified) || !ThingModules.Active(ThingModule.WorldTools) || request.Count < 5 || request.Count > BatchSize + 4)
                return OperationExecution<IReadOnlyList<string>>.Rejected("DropsOwnerUnavailable");
            if (!float.TryParse(request[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x) || !float.TryParse(request[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y)
                || !float.TryParse(request[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var z) || !int.TryParse(request[3], NumberStyles.None, CultureInfo.InvariantCulture, out var radius)
                || radius < 5 || radius > 100 || !Finite(new Vector3(x, y, z))) return OperationExecution<IReadOnlyList<string>>.Rejected("MalformedDrops");
            context.MarkDecisionMade();
            try
            {
                var ok = ThingDrops.RemoveOwned(new Vector3(x, y, z), radius, request.Skip(4).Distinct(StringComparer.Ordinal).ToArray(), out var removed);
                return OperationExecution<IReadOnlyList<string>>.Success(new[] { ok ? "ok" : "refused", removed.ToString(CultureInfo.InvariantCulture) });
            }
            catch { return OperationExecution<IReadOnlyList<string>>.RecoveryRequired(); }
        }

        public static void Tick()
        {
            if (generation != CruciblePlugin.Session.Generation || !ThingModules.Active(ThingModule.WorldTools))
            {
                generation = CruciblePlugin.Session.Generation; pending.Clear(); uiPending.Clear(); previews.Clear(); token = ""; return;
            }
            // Guards skip the per-frame LINQ and copies while nothing is pending.
            if (previews.Count > 0) foreach (var id in previews.Where(p => p.Value.Expires < DateTime.UtcNow).Select(p => p.Key).ToArray()) previews.Remove(id);
            if (pending.Count > 0) foreach (var task in pending.ToArray())
            {
                if (!task.IsCompleted) continue;
                pending.Remove(task); token = "";
                var fromUi = uiPending.Remove(task);
                if (task.Status != TaskStatus.RanToCompletion || task.Result.State != OperationState.Succeeded)
                { Print("drops_unknown"); if (fromUi) UiReply?.Invoke(new[] { ThingText.Text("drops_unknown") }); continue; }
                var reply = task.Result.Reply;
                if (reply.Count == 3 && reply[0] == Marker)
                {
                    token = reply[1]; global::Console.instance?.Print(reply[2]);
                    if (!fromUi && Plugin.Settings != null && !Plugin.Settings.Get(ThingSettings.General.ConfirmDestructive)) Send(new[] { "confirm", token });
                }
                else foreach (var line in reply) global::Console.instance?.Print(line);
                if (fromUi) UiReply?.Invoke(reply);
            }
        }

        static bool Finite(Vector3 p) => !float.IsNaN(p.x) && !float.IsInfinity(p.x) && !float.IsNaN(p.y) && !float.IsInfinity(p.y) && !float.IsNaN(p.z) && !float.IsInfinity(p.z);
        static string F(float value) => value.ToString("R", CultureInfo.InvariantCulture);
        static void Print(string key) => global::Console.instance?.Print(ThingText.Text(key));
        internal static void ReleaseUiPending()
        {
            // Detached UI requests may already have executed. Ignore their later replies; never retry mutations.
            foreach (var task in uiPending)
            {
                pending.Remove(task);
                task.ContinueWith(completed => { var ignored = completed.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
            }
            uiPending.Clear();
        }

        public static void Shutdown() { operation?.Dispose(); ownerCommand?.Dispose(); operation = ownerCommand = null; pending.Clear(); uiPending.Clear(); UiReply = null; previews.Clear(); token = ""; generation = -1; }
    }
}
