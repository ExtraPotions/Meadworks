// Thing - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: terrain-only authenticated preview/confirm operation; the general admin relay stays admin-only.
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
    public static class ThingTerrainCommands
    {
        const string OperationId = "meadworks.thing.terrain", OwnerCommand = "meadworks.thing.terrain_owner", PreviewMarker = "THING_TERRAIN_PREVIEW";
        const double PreviewSeconds = 20;
        static readonly Dictionary<string, Preview> previews = new Dictionary<string, Preview>(StringComparer.Ordinal);
        static readonly List<Task<OperationResult<IReadOnlyList<string>>>> pending = new List<Task<OperationResult<IReadOnlyList<string>>>>();
        static readonly HashSet<Task<OperationResult<IReadOnlyList<string>>>> uiPending = new HashSet<Task<OperationResult<IReadOnlyList<string>>>>();
        public static event Action<IReadOnlyList<string>>? UiReply;
        static IDisposable? operation;
        static IDisposable? ownerCommand;
        static GameObject? circle;
        static Material? material;
        static string token = "";
        static DateTime visibleUntil;
        static long generation = -1;

        sealed class Preview
        {
            public Vector3 Center;
            public int Radius;
            public long PlayerId;
            public string Token = "";
            public DateTime Expires;
            public ZNetPeer? Peer;
        }

        sealed class Codec : IOperationCodec<IReadOnlyList<string>, IReadOnlyList<string>>
        {
            public byte[] EncodeRequest(IReadOnlyList<string> request) => RemoteAdmin.EncodeRequest(request);
            public IReadOnlyList<string> DecodeRequest(byte[] bytes) => RemoteAdmin.DecodeRequest(bytes);
            public byte[] EncodeReply(IReadOnlyList<string> reply) => RemoteAdmin.EncodeReply(reply);
            public IReadOnlyList<string> DecodeReply(byte[] bytes) => RemoteAdmin.DecodeReply(bytes);
        }

        public static void Initialize(ProductRegistration registration)
        {
            generation = CruciblePlugin.Session.Generation;
            operation = CruciblePlugin.Services.Operations.Register(registration,
                new OperationDescriptor(OperationId, new System.Version(1, 0)), new Codec(), Apply);
            ownerCommand = CruciblePlugin.Services.PeerCommands.Register(registration,
                new OperationDescriptor(OwnerCommand, new System.Version(1, 0)), new Codec(), ApplyOwner);
        }

        public static bool Run(string[] args, bool fromUi = false)
        {
            if (args.Length == 0 || !string.Equals(args[0], "terrain", StringComparison.OrdinalIgnoreCase)) return false;
            if (operation == null || !ThingModules.Active(ThingModule.WorldTools)) { Print("refused_product_off"); return true; }
            string[] request;
            if (args.Length == 2 && string.Equals(args[1], "cancel", StringComparison.OrdinalIgnoreCase))
            {
                ClearVisual();
                request = new[] { "cancel" };
            }
            else if (args.Length == 2 && string.Equals(args[1], "confirm", StringComparison.OrdinalIgnoreCase)) request = new[] { "confirm", token };
            else if (args.Length == 2 && int.TryParse(args[1], NumberStyles.None, CultureInfo.InvariantCulture, out var radius) && radius > 0 && radius <= 50)
                request = new[] { "preview", args[1] };
            else { Print("terrain_usage"); return true; }
            var task = CruciblePlugin.RequestClient.InvokeAsync<IReadOnlyList<string>, IReadOnlyList<string>>(OperationId, request, CancellationToken.None);
            pending.Add(task); if (fromUi) uiPending.Add(task);
            return true;
        }

        static OperationExecution<IReadOnlyList<string>> Apply(AuthorityContext context, IReadOnlyList<string> request)
        {
            IReadOnlyList<string> Reply(string key, params object[] values) => new[] { ThingText.Format(key, values) };
            OperationExecution<IReadOnlyList<string>> Fail(string key) => OperationExecution<IReadOnlyList<string>>.Success(Reply(key));
            var net = ZNet.instance;
            if (!net || !net.IsServer() || !ThingServer.Active || !ThingModules.Active(ThingModule.WorldTools) || Plugin.Settings == null) return Fail("refused_product_off");
            var peer = !context.IsLocal && long.TryParse(context.SenderId, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var uid)
                ? net.GetPeers().FirstOrDefault(p => p != null && p.IsReady() && p.m_uid == uid) : null;
            var platform = context.IsLocal ? "host" : peer?.m_socket?.GetHostName() ?? "";
            var player = context.IsLocal ? Player.m_localPlayer : peer == null ? null : ZNetScene.instance?.FindInstance(peer.m_characterID)?.GetComponent<Player>();
            if (platform.Length == 0 || !player || player.GetPlayerID() == 0) return Fail(ThingTerrain.Incomplete);
            var admin = context.IsLocal || (context.IsAdmin && net.IsAdmin(platform));
            var role = net.IsDedicated() ? ThingRole.DedicatedServer : net.GetPeers().Count > 0 ? ThingRole.Host : ThingRole.SinglePlayer;
            var verdict = AdminCheck.Decide(ThingAction.TerrainReset, new RequestFacts(role, context.IsLocal, admin, false), Plugin.Settings.Rules());
            if (!AdminCheck.Permits(verdict)) return Fail(AdminCheck.TextKey(verdict));
            if (request.Count == 1 && request[0] == "cancel") { previews.Remove(platform); context.MarkDecisionMade(); return Fail("terrain_canceled"); }
            if (request.Count != 2) return Fail("terrain_usage");
            if (request[0] == "preview")
            {
                previews.Remove(platform);
                if (!int.TryParse(request[1], NumberStyles.None, CultureInfo.InvariantCulture, out var requested) || requested < 1 || requested > 50) return Fail("terrain_usage");
                var radius = TerrainReset.Radius(requested, Plugin.Settings.Get(ThingSettings.World.TerrainMaxRadius));
                var center = player.transform.position;
                var refused = ThingTerrain.Check(center, radius, verdict, player.GetPlayerID());
                if (refused != null) return Fail(refused);
                var preview = new Preview { Center = center, Radius = radius, PlayerId = player.GetPlayerID(), Peer = peer, Token = Guid.NewGuid().ToString("N"), Expires = DateTime.UtcNow.AddSeconds(PreviewSeconds) };
                previews[platform] = preview;
                context.MarkDecisionMade();
                return OperationExecution<IReadOnlyList<string>>.Success(new[] { PreviewMarker, preview.Token, F(center.x), F(center.y), F(center.z), radius.ToString(CultureInfo.InvariantCulture), ThingText.Format("terrain_preview", radius) });
            }
            if (request[0] != "confirm" || !previews.TryGetValue(platform, out var approved) || request[1].Length == 0 || request[1] != approved.Token) return Fail("terrain_expired");
            previews.Remove(platform); // Single-use, including failures. Permissions/area/owners are checked again now.
            if (DateTime.UtcNow > approved.Expires || !ReferenceEquals(approved.Peer, peer) || approved.PlayerId != player.GetPlayerID()
                || approved.Radius > Plugin.Settings.Get(ThingSettings.World.TerrainMaxRadius) || Utils.DistanceXZ(player.transform.position, approved.Center) > 2) return Fail("terrain_expired");
            var actor = new Actor(platform, player.GetPlayerName());
            var check = ThingTerrain.Check(approved.Center, approved.Radius, verdict, approved.PlayerId);
            if (check != null) return Fail(check);
            var owner = ThingTerrain.Owner(approved.Center, approved.Radius);
            if (owner == 0) return Fail(ThingTerrain.Ownership);
            context.MarkDecisionMade();
            if (owner == ZNet.GetUID())
            {
                var changes = 0;
                try
                {
                    var failure = ThingTerrain.Reset(approved.Center, approved.Radius, verdict, approved.PlayerId, out changes);
                    return Finish(actor, approved.Radius, failure, changes);
                }
                catch (Exception error)
                {
                    ThingServer.Audit(actor, ThingAction.TerrainReset, default, AuditOutcome.Failed,
                        "outcome unknown; observed changes=" + changes + "; error=" + error.GetType().Name + "; no automatic retry");
                    return OperationExecution<IReadOnlyList<string>>.RecoveryRequired();
                }
            }
            // Existing Crucible authenticated serverâ†’actual-owner path, as used by Burrow. Never claim terrain ownership.
            var command = new[] { F(approved.Center.x), F(approved.Center.y), F(approved.Center.z), approved.Radius.ToString(CultureInfo.InvariantCulture),
                approved.PlayerId.ToString(CultureInfo.InvariantCulture), verdict == Verdict.Allowed ? "admin" : "own" };
            var task = CruciblePlugin.PeerCommandClient.SendAsync<IReadOnlyList<string>, IReadOnlyList<string>>(owner.ToString(CultureInfo.InvariantCulture),
                OwnerCommand, approved.Token, command, CancellationToken.None);
            return PollOwner(task, actor, approved.Radius);
        }

        static OperationExecution<IReadOnlyList<string>> ApplyOwner(PeerCommandContext context, IReadOnlyList<string> request)
        {
            if ((!context.IsLocal && !context.ServerConnectionVerified) || !ThingModules.Active(ThingModule.WorldTools) || request.Count != 6)
                return OperationExecution<IReadOnlyList<string>>.Rejected("TerrainOwnerUnavailable");
            if (!float.TryParse(request[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x) || !float.TryParse(request[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y)
                || !float.TryParse(request[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var z) || !int.TryParse(request[3], out var radius) || radius < 1 || radius > 50
                || !long.TryParse(request[4], out var playerId) || playerId == 0 || (request[5] != "admin" && request[5] != "own")
                || float.IsNaN(x) || float.IsInfinity(x) || float.IsNaN(y) || float.IsInfinity(y) || float.IsNaN(z) || float.IsInfinity(z))
                return OperationExecution<IReadOnlyList<string>>.Rejected("MalformedTerrain");
            var center = new Vector3(x, y, z);
            var verdict = request[5] == "admin" ? Verdict.Allowed : Verdict.AllowedOwnPiecesOnly;
            var refused = ThingTerrain.Check(center, radius, verdict, playerId, true, false);
            if (refused != null) return OperationExecution<IReadOnlyList<string>>.Success(new[] { refused, "0" });
            context.MarkDecisionMade();
            try
            {
                var failure = ThingTerrain.Reset(center, radius, verdict, playerId, out var changes, false);
                return OperationExecution<IReadOnlyList<string>>.Success(new[] { failure ?? "ok", changes.ToString(CultureInfo.InvariantCulture) });
            }
            catch { return OperationExecution<IReadOnlyList<string>>.RecoveryRequired(); }
        }

        static OperationExecution<IReadOnlyList<string>> PollOwner(Task<OperationResult<IReadOnlyList<string>>> task, Actor actor, int radius)
        {
            if (!task.IsCompleted) return OperationExecution<IReadOnlyList<string>>.Pending(() => PollOwner(task, actor, radius));
            if (task.Status != TaskStatus.RanToCompletion || task.Result.State != OperationState.Succeeded || task.Result.Reply.Count != 2
                || !int.TryParse(task.Result.Reply[1], NumberStyles.None, CultureInfo.InvariantCulture, out var changes) || changes < 0)
            {
                ThingServer.Audit(actor, ThingAction.TerrainReset, default, AuditOutcome.Failed, "owner outcome unknown; no automatic retry");
                return OperationExecution<IReadOnlyList<string>>.RecoveryRequired();
            }
            return Finish(actor, radius, task.Result.Reply[0] == "ok" ? null : task.Result.Reply[0], changes);
        }

        static OperationExecution<IReadOnlyList<string>> Finish(Actor actor, int radius, string? failure, int changes)
        {
            if (failure != null && changes > 0)
            {
                ThingServer.Audit(actor, ThingAction.TerrainReset, default, AuditOutcome.Failed,
                    "partial terrain reset; observed changes=" + changes + "; failure=" + failure + "; no automatic retry");
                // RecoveryRequired has no success payload; observed partial count stays in the server audit.
                // The client receives terrain_unknown and must inspect the world before another request.
                return OperationExecution<IReadOnlyList<string>>.RecoveryRequired();
            }
            ThingServer.Audit(actor, ThingAction.TerrainReset, default, failure == null ? AuditOutcome.Done : AuditOutcome.Refused,
                failure ?? "radius=" + radius + "; changes=" + changes);
            return OperationExecution<IReadOnlyList<string>>.Success(new[] { failure == null ? ThingText.Format("terrain_done", changes) : ThingText.Text(failure) });
        }

        public static void Tick()
        {
            var session = CruciblePlugin.Session.Generation;
            if (generation != session || !ThingModules.Active(ThingModule.WorldTools))
            {
                generation = session;
                pending.Clear(); uiPending.Clear(); previews.Clear(); ClearVisual();
                return;
            }
            // Guards skip the per-frame LINQ and copies while nothing is pending.
            if (previews.Count > 0) foreach (var key in previews.Where(p => p.Value.Expires < DateTime.UtcNow).Select(p => p.Key).ToArray()) previews.Remove(key);
            if (pending.Count > 0) foreach (var task in pending.ToArray())
            {
                if (!task.IsCompleted) continue;
                pending.Remove(task);
                var fromUi = uiPending.Remove(task);
                ClearVisual();
                if (task.Status != TaskStatus.RanToCompletion || task.Result.State != OperationState.Succeeded)
                { Print("terrain_unknown"); if (fromUi) UiReply?.Invoke(new[] { ThingText.Text("terrain_unknown") }); continue; }
                var reply = task.Result.Reply;
                if (reply.Count == 7 && reply[0] == PreviewMarker)
                {
                    token = reply[1]; visibleUntil = DateTime.UtcNow.AddSeconds(PreviewSeconds);
                    Draw(new Vector3(Parse(reply[2]), Parse(reply[3]), Parse(reply[4])), int.Parse(reply[5], CultureInfo.InvariantCulture));
                    global::Console.instance?.Print(reply[6]);
                }
                else foreach (var line in reply) global::Console.instance?.Print(line);
                if (fromUi) UiReply?.Invoke(reply);
            }
            if (token.Length > 0 && DateTime.UtcNow > visibleUntil) ClearVisual();
        }

        static void Draw(Vector3 center, int radius)
        {
            if (!Plugin.AllowsUi) return;
            circle = new GameObject("Thing terrain preview");
            var line = circle.AddComponent<LineRenderer>();
            var shader = Shader.Find("Sprites/Default");
            if (!shader) { ClearVisual(); return; }
            material = new Material(shader);
            line.sharedMaterial = material; line.startColor = line.endColor = Color.yellow;
            line.startWidth = line.endWidth = 0.08f; line.loop = true; line.useWorldSpace = true; line.positionCount = 96;
            for (var i = 0; i < 96; i++)
            {
                var angle = i * Mathf.PI * 2 / 96;
                var point = center + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
                if (Heightmap.GetHeight(point, out var height)) point.y = height;
                point.y += 0.15f;
                line.SetPosition(i, point);
            }
        }

        static string F(float value) => value.ToString("R", CultureInfo.InvariantCulture);
        static float Parse(string value) => float.Parse(value, CultureInfo.InvariantCulture);
        static void Print(string key) => global::Console.instance?.Print(ThingText.Text(key));
        static void ClearVisual()
        {
            if (circle) UnityEngine.Object.Destroy(circle);
            if (material) UnityEngine.Object.Destroy(material);
            circle = null; material = null; token = "";
        }
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

        public static void Shutdown() { operation?.Dispose(); operation = null; ownerCommand?.Dispose(); ownerCommand = null; pending.Clear(); uiPending.Clear(); UiReply = null; previews.Clear(); generation = -1; ClearVisual(); }
    }
}
