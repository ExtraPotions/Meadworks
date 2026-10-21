// Tempest - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: temporary admin path for summon and stop from a remote client (user decision 2026-10-08 item 5).
// Remove once Crucible's CS-D1 relay (admin command relay) exists: delete this file and Commands/RemoteCommand.cs in Tempest.Core,
// drop CommandRoute.Remote and send the verbs through CS-D1. Same Operations pattern as Saga's power commands.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BepInEx.Configuration;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Tempest.Commands;
using ExtraPotions.Tempest.Config;
using ExtraPotions.Tempest.Events;
using ExtraPotions.Tempest.Integration;
using ExtraPotions.Tempest.Localization;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Tempest.Runtime
{
    /// <summary>
    /// Crucible Operations <c>meadworks.tempest.weather_command</c> 1.0 (summon verbs and stop): registered on every peer (the client needs the
    /// codec to send); executed only on the authority. The server checks, in order: Events module active, its own Weather
    /// Commands cheat value, the caller's admin status (C02, from the connection) and the Mischief gate for the World
    /// category for that peer. Refusals return a code the caller turns into a clear text. Main thread only.
    /// </summary>
    public static class TempestRemoteCommands
    {
        static IDisposable? operation;
        /// <summary>Seconds a started-storm reply waits for the new event record before it is named a thunderstorm.</summary>
        const float RecordWait = 3f;

        sealed class Pending
        {
            internal Task<OperationResult<RemoteCommand.Reply>> Task = null!;
            internal RemoteCommand.Request Request;
            internal int? SentSerial;
            internal float WaitUntil = -1;
        }

        static readonly List<Pending> pending = new List<Pending>();
        static MeadworksLog? log;

        sealed class Codec : IOperationCodec<RemoteCommand.Request, RemoteCommand.Reply>
        {
            public byte[] EncodeRequest(RemoteCommand.Request request) => RemoteCommand.EncodeRequest(request);
            public RemoteCommand.Request DecodeRequest(byte[] bytes) => RemoteCommand.DecodeRequest(bytes);
            public byte[] EncodeReply(RemoteCommand.Reply reply) => RemoteCommand.EncodeReply(reply);
            public RemoteCommand.Reply DecodeReply(byte[] bytes) => RemoteCommand.DecodeReply(bytes);
        }

        public static void Initialize(ProductRegistration registration, MeadworksLog logger)
        {
            log = logger;
            operation = CruciblePlugin.Services.Operations.Register(registration,
                new OperationDescriptor(RemoteCommand.OperationId, RemoteCommand.OperationVersion), new Codec(), Apply);
        }

        /// <summary>Client side: sends the verb to the server; the reply is printed by <see cref="Tick"/>.</summary>
        public static void Send(CommandVerb verb, bool now, bool dry = false) => Send(new RemoteCommand.Request(verb, now, dry));

        static void Send(RemoteCommand.Request request) =>
            pending.Add(new Pending
            {
                Task = CruciblePlugin.RequestClient.InvokeAsync<RemoteCommand.Request, RemoteCommand.Reply>(RemoteCommand.OperationId, request, CancellationToken.None),
                Request = request,
                SentSerial = TempestWorld.Current?.Serial,
            });

        /// <summary>Called from Update: prints finished replies to the console.</summary>
        public static void Tick()
        {
            foreach (var item in pending.ToArray())
            {
                var task = item.Task;
                if (!task.IsCompleted) continue;
                pending.Remove(item);
                string line;
                if (task.Status != TaskStatus.RanToCompletion) line = TempestText.Format(TempestTextKeys.CommandRemoteFailed, "Faulted");
                else
                {
                    var result = task.Result;
                    if (RemoteCommand.DryUnsupported(item.Request, result.State == OperationState.Rejected, result.Code))
                    {
                        // A server without lightning storms cannot read the option and fails the request before deciding anything.
                        global::Console.instance?.Print(TempestText.Text(TempestTextKeys.CommandDryUnsupported));
                        Send(new RemoteCommand.Request(item.Request.Verb, item.Request.Now));
                        continue;
                    }
                    EventKind? current = TempestWorld.Current?.Kind;
                    if (result.State == OperationState.Succeeded && RemoteCommand.AwaitsRecord(item.Request, result.Reply))
                    {
                        // The reply cannot carry a rolled Lightning Storm; the started event's record can.
                        var e = TempestWorld.Current;
                        var arrived = e != null && e.Serial != item.SentSerial;
                        if (!arrived)
                        {
                            if (item.WaitUntil < 0) item.WaitUntil = UnityEngine.Time.unscaledTime + RecordWait;
                            if (UnityEngine.Time.unscaledTime < item.WaitUntil)
                            {
                                pending.Add(item);
                                continue;
                            }
                        }
                        current = arrived ? e!.Kind : (EventKind?)null;
                    }
                    line = result.State == OperationState.Succeeded
                        ? TempestCommand.OutcomeText(result.Reply.Outcome, RemoteCommand.Shown(item.Request, result.Reply, current))
                        : RemoteCommand.TextKey(result.Code) == TempestTextKeys.CommandRemoteFailed
                            ? TempestText.Format(TempestTextKeys.CommandRemoteFailed, result.Code)
                            : TempestText.Text(RemoteCommand.TextKey(result.Code));
                }
                global::Console.instance?.Print(line);
            }
        }

        public static void Shutdown()
        {
            operation?.Dispose();
            operation = null;
            pending.Clear();
            log = null;
        }

        static OperationExecution<RemoteCommand.Reply> Apply(AuthorityContext context, RemoteCommand.Request request)
        {
            var settings = Plugin.Settings;
            var active = settings != null && TempestWorld.IsAuthority && TempestModules.Active(TempestModule.Events);
            var cheat = settings != null && ((ConfigEntry<bool>)settings.Entry(TempestSettings.Cheats.WeatherCommands)).Value;
            var mischief = context.IsLocal
                ? MeadworksMischief.Decide(Plugin.Guid, MischiefCategory.World).Allowed
                : MeadworksMischief.DecideForPeer(PeerRpc(context.SenderId), MischiefCategory.World).Allowed;
            var refusal = RemoteCommand.Refusal(active, cheat, context.IsAdmin, mischief);
            if (refusal != null)
            {
                log?.Info("Weather command from " + context.SenderId + " refused: " + refusal + ".");
                return OperationExecution<RemoteCommand.Reply>.Rejected(refusal);
            }
            context.MarkDecisionMade();
            if (request.Verb == CommandVerb.Stop)
            {
                var stopped = TempestWorld.Stop(out var kind, out _);
                return OperationExecution<RemoteCommand.Reply>.Success(new RemoteCommand.Reply(stopped, kind));
            }
            var summoned = CommandParser.KindOf(request.Verb, request.Dry)!.Value;
            log?.Info("Weather command from " + context.SenderId + ": " + request.Verb + (request.Now ? " now" : "") + (request.Dry ? " lightning" : "") + ".");
            var outcome = TempestWorld.Summon(summoned, request.Now, request.Dry, out var started);
            // EncodeReply still sends a Lightning Storm as a thunderstorm for older clients.
            return OperationExecution<RemoteCommand.Reply>.Success(new RemoteCommand.Reply(outcome, started));
        }

        static ZRpc? PeerRpc(string senderId)
        {
            if (ZNet.instance == null || !long.TryParse(senderId, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var uid)) return null;
            return ZNet.instance.GetPeers().FirstOrDefault(p => p != null && p.m_uid == uid)?.m_rpc;
        }
    }
}
