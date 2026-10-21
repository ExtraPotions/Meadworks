// Thing - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: Temporary remote admin path for the `thing` verbs (user decision 2026-10-08, Thing item 1 changed to b).
// REMOVE WHEN CRUCIBLE CS-D1 (admin command relay) LANDS: delete this file and Commands/RemoteAdmin.cs in Thing.Core, drop
// the remote branch in ThingCommand.Run and send the verbs through CS-D1. Same Operations pattern as Tempest's
// TempestRemoteCommands (Tempest 563dbef).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Thing.Admin;
using ExtraPotions.Thing.Commands;
using ExtraPotions.Thing.Localization;
using ExtraPotions.Thing.Moderation;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Thing.Runtime
{
    /// <summary>
    /// Crucible Operations <c>meadworks.thing.admin_command</c> 1.0: registered on every peer (the client needs the codec to
    /// send); executed only on the authority. The server checks Thing is active there and that the caller is a server admin
    /// by the connection's platform ID (C02 context, re-checked with <c>ZNet.IsAdmin</c>), then runs the same verb code as its
    /// own console with the caller as the admin, so <c>AdminCheck</c> decides again and the audit log names the caller.
    /// Main thread only.
    /// </summary>
    public static class ThingRemoteCommands
    {
        static IDisposable? operation;
        static readonly List<Task<OperationResult<IReadOnlyList<string>>>> pending = new List<Task<OperationResult<IReadOnlyList<string>>>>();
        static MeadworksLog? log;

        sealed class Codec : IOperationCodec<IReadOnlyList<string>, IReadOnlyList<string>>
        {
            public byte[] EncodeRequest(IReadOnlyList<string> request) => RemoteAdmin.EncodeRequest(request);
            public IReadOnlyList<string> DecodeRequest(byte[] bytes) => RemoteAdmin.DecodeRequest(bytes);
            public byte[] EncodeReply(IReadOnlyList<string> reply) => RemoteAdmin.EncodeReply(reply);
            public IReadOnlyList<string> DecodeReply(byte[] bytes) => RemoteAdmin.DecodeReply(bytes);
        }

        public static bool Available => operation != null;

        public static void Initialize(ProductRegistration registration, MeadworksLog logger)
        {
            log = logger;
            operation = CruciblePlugin.Services.Operations.Register(registration,
                new OperationDescriptor(RemoteAdmin.OperationId, RemoteAdmin.OperationVersion), new Codec(), Apply);
        }

        /// <summary>Client side: sends the typed arguments to the server; the reply is printed by <see cref="Tick"/>.</summary>
        public static void Send(string[] args) =>
            pending.Add(CruciblePlugin.RequestClient.InvokeAsync<IReadOnlyList<string>, IReadOnlyList<string>>(
                RemoteAdmin.OperationId, args.Where(a => !string.IsNullOrWhiteSpace(a)).ToArray(), CancellationToken.None));

        /// <summary>Called from Update: prints finished replies to the console.</summary>
        public static void Tick()
        {
            if (pending.Count == 0) return; // no per-frame copy while nothing is pending
            foreach (var task in pending.ToArray())
            {
                if (!task.IsCompleted) continue;
                pending.Remove(task);
                IEnumerable<string> lines;
                if (task.Status != TaskStatus.RanToCompletion) lines = new[] { ThingText.Format(ThingTextKeys.CommandRemoteFailed, "Faulted") };
                else
                {
                    var result = task.Result;
                    if (result.State == OperationState.Succeeded) lines = result.Reply;
                    else
                    {
                        var key = RemoteAdmin.TextKey(result.Code);
                        lines = new[] { key == ThingTextKeys.CommandRemoteFailed ? ThingText.Format(key, result.Code) : ThingText.Text(key) };
                    }
                }
                foreach (var line in lines) global::Console.instance?.Print(line);
            }
        }

        public static void Shutdown()
        {
            operation?.Dispose();
            operation = null;
            pending.Clear();
            log = null;
        }

        static OperationExecution<IReadOnlyList<string>> Apply(AuthorityContext context, IReadOnlyList<string> args)
        {
            var net = ZNet.instance;
            var active = Plugin.Settings != null && net != null && net.IsServer() && ThingServer.Active && ThingModules.ProductActive(out _);
            var caller = Caller(context, out var hostName);
            // C02 context decides admin from the connection; re-checked against the live adminlist by platform ID.
            var isAdmin = context.IsLocal || (context.IsAdmin && hostName.Length > 0 && net != null && net.IsAdmin(hostName));
            var parsed = CommandParser.Parse(args);
            var held = isAdmin ? AdminRight.None : ThingAccess.Of(hostName);
            var self = parsed.Kind == CommandKind.Action && parsed.Action == ThingAction.GrantEdit && parsed.Text == "self";
            var covered = parsed.Kind == CommandKind.Action && AdminPermissions.Covers(held, parsed.Action);
            var playerRead = parsed.Kind == CommandKind.Status && args.Count >= 2
                && string.Equals(args[0], "ui-read", StringComparison.OrdinalIgnoreCase)
                && string.Equals(args[1], "players", StringComparison.OrdinalIgnoreCase)
                && (held & AdminRight.Inventory) == AdminRight.Inventory;
            var refusal = RemoteAdmin.Refusal(active, isAdmin || self || covered || playerRead);
            if (refusal != null)
            {
                log?.Info("Thing remote command from " + caller.Id + " refused: " + refusal + ".");
                if (active && refusal == RemoteAdmin.NotAdmin)
                    ThingServer.Audit(caller, ActionOf(args), default, Admin.AuditOutcome.Refused, "remote: not admin");
                return OperationExecution<IReadOnlyList<string>>.Rejected(refusal);
            }
            context.MarkDecisionMade();
            var lines = ThingCommand.Execute(args, caller, fromLocalAuthority: context.IsLocal, requesterIsAdmin: isAdmin, requesterConnection: context.SenderId).ToList();
            return OperationExecution<IReadOnlyList<string>>.Success(lines);
        }

        /// <summary>The caller as platform ID and name; the audit log records this ID (temporary path).</summary>
        static Actor Caller(AuthorityContext context, out string hostName)
        {
            hostName = "";
            if (context.IsLocal) return new Actor("host", Player.m_localPlayer != null ? Player.m_localPlayer.GetPlayerName() : "console");
            var net = ZNet.instance;
            if (net == null || !long.TryParse(context.SenderId, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var uid))
                return new Actor("peer:" + context.SenderId, "");
            var peer = net.GetPeers().FirstOrDefault(p => p != null && p.m_uid == uid);
            hostName = peer?.m_socket?.GetHostName() ?? "";
            return new Actor(hostName.Length > 0 ? hostName : "peer:" + context.SenderId, peer?.m_playerName ?? "");
        }

        static Admin.ThingAction ActionOf(IReadOnlyList<string> args)
        {
            var parsed = CommandParser.Parse(args);
            return parsed.Kind == CommandKind.Action ? parsed.Action : Admin.ThingAction.ListPlayers;
        }
    }
}
