// Tempest - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: temporary admin path for summon and stop from a remote client (user decision 2026-10-08 item 5).
// Remove once Crucible's CS-D1 relay (admin command relay) exists: delete this file, CommandRoute.Remote and the plugin's
// TempestRemoteCommands, and route the verbs through CS-D1 instead.
using System;
using ExtraPotions.Tempest.Events;
using ExtraPotions.Tempest.Localization;

namespace ExtraPotions.Tempest.Commands
{
    /// <summary>
    /// The Crucible Operations request a remote client sends for <c>tempest blizzard|storm [now] [lightning]</c> and <c>tempest stop</c>,
    /// its reply, and the server's decision (design 5): the Events module must be active, the server's Weather Commands
    /// cheat on, the caller a server admin (C02), and the Mischief gate (World category) must allow the caller.
    /// Temporary until Crucible's CS-D1 relay exists.
    /// </summary>
    public static class RemoteCommand
    {
        public const string OperationId = "meadworks.tempest.weather_command";
        public static readonly Version OperationVersion = new Version(1, 0);
        const byte Format = 1;

        public const string Inactive = "TempestInactive";
        public const string CheatOff = "WeatherCommandsOff";
        public const string NotAdmin = "NotAdmin";
        public const string MischiefDenied = "MischiefDenied";

        public readonly struct Request
        {
            public Request(CommandVerb verb, bool now, bool dry = false)
            {
                Verb = verb;
                Now = now;
                Dry = dry && verb == CommandVerb.Storm;
            }

            public CommandVerb Verb { get; }
            public bool Now { get; }

            /// <summary>A dry thunderstorm (storm only). Sent as bit 2 of the options byte; a server without dry storms cannot decode it (see <see cref="DryUnsupported"/>).</summary>
            public bool Dry { get; }
        }

        public readonly struct Reply
        {
            public Reply(SchedulerOutcome outcome, EventKind kind)
            {
                Outcome = outcome;
                Kind = kind;
            }

            public SchedulerOutcome Outcome { get; }
            public EventKind Kind { get; }
        }

        /// <summary>Summon verbs and stop. A server older than Dense Fog, Ash Storm and Meteor Shower cannot decode those verbs and fails the request.</summary>
        public static bool Sendable(CommandVerb verb) => verb == CommandVerb.Stop || CommandParser.Summons(verb);

        public static byte[] EncodeRequest(Request request)
        {
            if (!Sendable(request.Verb)) throw new ArgumentException("Only summon verbs and stop are sent to the server.", nameof(request));
            return new[] { Format, (byte)request.Verb, (byte)((request.Now ? 1 : 0) | (request.Dry ? 2 : 0)) };
        }

        public static Request DecodeRequest(byte[] bytes)
        {
            if (bytes == null || bytes.Length != 3 || bytes[0] != Format || bytes[2] > 3) throw new FormatException("Tempest weather command");
            var verb = (CommandVerb)bytes[1];
            if (!Enum.IsDefined(typeof(CommandVerb), verb) || !Sendable(verb)) throw new FormatException("Tempest weather command verb");
            if ((bytes[2] & 2) != 0 && verb != CommandVerb.Storm) throw new FormatException("Tempest weather command options");
            return new Request(verb, (bytes[2] & 1) != 0, (bytes[2] & 2) != 0);
        }

        /// <summary>The code Crucible returns when the server's handler failed, including a request it could not decode.</summary>
        public const string HandlerFailed = "HandlerFailed";

        /// <summary>
        /// A dry summon refused because the server could not handle it: an older server cannot decode the dry option. The
        /// client then sends a normal storm and says so.
        /// </summary>
        public static bool DryUnsupported(Request request, bool rejected, string? code) => request.Dry && rejected && code == HandlerFailed;

        /// <summary>
        /// A Lightning Storm is sent as a thunderstorm: clients older than the Lightning Storm kind refuse kind numbers they do
        /// not know, and the caller already knows it asked for one (or sees the stopped event itself).
        /// </summary>
        public static byte[] EncodeReply(Reply reply) =>
            new[] { Format, (byte)reply.Outcome, (byte)(reply.Kind == EventKind.LightningStorm ? EventKind.Thunderstorm : reply.Kind) };

        /// <summary>
        /// The kind a client names for a reply: a thunderstorm reply is a Lightning Storm when the request asked for one, when a
        /// stop stopped one, or when a started storm rolled Lightning Storm Chance (<paramref name="currentKind"/> is then the
        /// started event's record; the caller passes null while that record has not reached it).
        /// </summary>
        public static EventKind Shown(Request request, Reply reply, EventKind? currentKind)
        {
            if (reply.Kind != EventKind.Thunderstorm) return reply.Kind;
            if (request.Verb == CommandVerb.Stop) return currentKind == EventKind.LightningStorm ? EventKind.LightningStorm : EventKind.Thunderstorm;
            return request.Dry || (reply.Outcome == SchedulerOutcome.Started && currentKind == EventKind.LightningStorm) ? EventKind.LightningStorm : EventKind.Thunderstorm;
        }

        /// <summary>
        /// A started storm reply cannot say whether the storm rolled dry (the wire names a Lightning Storm as a thunderstorm for
        /// older clients), so the caller waits briefly for the new event record before naming it.
        /// </summary>
        public static bool AwaitsRecord(Request request, Reply reply) =>
            request.Verb == CommandVerb.Storm && !request.Dry && reply.Outcome == SchedulerOutcome.Started && reply.Kind == EventKind.Thunderstorm;

        public static Reply DecodeReply(byte[] bytes)
        {
            if (bytes == null || bytes.Length != 3 || bytes[0] != Format) throw new FormatException("Tempest weather reply");
            var outcome = (SchedulerOutcome)bytes[1];
            var kind = (EventKind)bytes[2];
            if (!Enum.IsDefined(typeof(SchedulerOutcome), outcome) || !Enum.IsDefined(typeof(EventKind), kind)) throw new FormatException("Tempest weather reply values");
            return new Reply(outcome, kind);
        }

        /// <summary>The server's refusal code, or null when the command may run.</summary>
        public static string? Refusal(bool eventsActive, bool cheatOn, bool isAdmin, bool mischiefAllowed)
        {
            if (!eventsActive) return Inactive;
            if (!cheatOn) return CheatOff;
            if (!isAdmin) return NotAdmin;
            if (!mischiefAllowed) return MischiefDenied;
            return null;
        }

        /// <summary>The text shown to the caller for a refusal or transport code.</summary>
        public static string TextKey(string code)
        {
            switch (code)
            {
                case Inactive: return TempestTextKeys.CommandInactive;
                case CheatOff: return TempestTextKeys.CommandCheatOff;
                case NotAdmin: return TempestTextKeys.CommandNotAdmin;
                case MischiefDenied: return TempestTextKeys.CommandMischiefDenied;
                default: return TempestTextKeys.CommandRemoteFailed;
            }
        }
    }
}
