// Tempest - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: console verbs of design 5 and W7 (WildWeather's verbs, behaviour only) and W9.
using ExtraPotions.Tempest.Commands;
using ExtraPotions.Tempest.Config;
using ExtraPotions.Tempest.Events;
using ExtraPotions.Tempest.Integration;
using ExtraPotions.Tempest.Localization;
using Jotunn.Entities;

namespace ExtraPotions.Tempest.Runtime
{
    /// <summary>
    /// <c>tempest status | blizzard [now] | storm [now] [lightning] | lightningstorm [now] | fog [now] | ashstorm [now] | meteorshower [now] | stop |
    /// override &lt;environment&gt; [freeze] | override reset</c>.
    /// Summon and stop run on the authority; from a remote client they go to the server through the temporary
    /// <see cref="TempestRemoteCommands"/> path (remove once Crucible's CS-D1 relay exists), where admin status and the Mischief
    /// gate are decided. <c>import [apply]</c> previews or applies the NoFogBruh settings import (host or single player).
    /// </summary>
    public sealed class TempestCommand : ConsoleCommand
    {
        readonly string name;

        public TempestCommand(string name) => this.name = name;

        public override string Name => name;

        public override string Help => "Tempest weather events: status, blizzard [now], storm [now] [lightning], lightningstorm [now], fog [now], ashstorm [now], meteorshower [now], stop, override <environment> [freeze], override reset, import [apply].";

        public override void Run(string[] args)
        {
            foreach (var line in Execute(args)) Print(line);
        }

        static void Print(string line) => global::Console.instance?.Print(line);

        static System.Collections.Generic.IEnumerable<string> Execute(string[] args)
        {
            var parsed = CommandParser.Parse(args);
            var settings = Plugin.Settings;
            if (parsed.Verb == CommandVerb.Help)
            {
                yield return TempestText.Text(TempestTextKeys.CommandHelp);
                yield break;
            }
            if (parsed.Verb == CommandVerb.Import || parsed.Verb == CommandVerb.ImportApply)
            {
                // Works from the main menu too; the import itself checks that this machine may write host settings.
                var import = Plugin.Import;
                yield return import == null ? TempestText.Text(TempestTextKeys.CommandInactive) : import.Run(parsed.Verb == CommandVerb.ImportApply);
                yield break;
            }
            if (settings == null || ZNet.instance == null || !TempestModules.ProductActive(out _))
            {
                yield return TempestText.Text(TempestTextKeys.CommandInactive);
                yield break;
            }
            var module = parsed.Verb == CommandVerb.Override || parsed.Verb == CommandVerb.OverrideReset ? TempestModule.Override : TempestModule.Events;
            if (!TempestModules.Active(module))
            {
                yield return TempestText.Text(TempestTextKeys.CommandInactive);
                yield break;
            }
            var refusal = CommandRules.Decide(parsed.Verb, Facts(), settings.Get(TempestSettings.Cheats.WeatherCommands), settings.Get(TempestSettings.Cheats.PersonalOverride));
            if (refusal != CommandRefusal.None)
            {
                yield return TempestText.Text(refusal == CommandRefusal.Headless ? TempestTextKeys.CommandHeadless : TempestTextKeys.CommandCheatOff);
                yield break;
            }
            if (CommandRules.Route(parsed.Verb, Facts()) == CommandRoute.Remote)
            {
                // Temporary until Crucible's CS-D1 relay exists: the server decides admin, cheat and Mischief gate and replies.
                TempestRemoteCommands.Send(parsed.Verb, parsed.Now, parsed.Dry);
                yield return TempestText.Text(TempestTextKeys.CommandSent);
                yield break;
            }
            switch (parsed.Verb)
            {
                case CommandVerb.Status:
                {
                    var e = TempestWorld.Current;
                    var phase = e == null ? PhaseState.Idle : Timeline.At(e, TempestWorld.Now);
                    if (e == null || phase.Phase == EventPhase.Idle) yield return TempestText.Text(TempestTextKeys.CommandStatusIdle);
                    else yield return TempestText.Format(TempestTextKeys.CommandStatusEvent, TempestText.Text(TempestTextKeys.Kind(e)), TempestText.Text(TempestTextKeys.Phase(phase.Phase)),
                        TempestTextKeys.Countdown(phase.SecondsLeft));
                    break;
                }
                case CommandVerb.Blizzard:
                case CommandVerb.Storm:
                case CommandVerb.DenseFog:
                case CommandVerb.AshStorm:
                case CommandVerb.MeteorShower:
                {
                    var kind = parsed.Kind!.Value;
                    if (!settings.TryUseCheat(TempestSettings.Cheats.WeatherCommands))
                    {
                        yield return TempestText.Text(TempestTextKeys.CommandCheatOff);
                        break;
                    }
                    var outcome = TempestWorld.Summon(kind, parsed.Now, parsed.Dry, out var started);
                    yield return OutcomeText(outcome, started);
                    break;
                }
                case CommandVerb.Stop:
                {
                    if (!settings.TryUseCheat(TempestSettings.Cheats.WeatherCommands))
                    {
                        yield return TempestText.Text(TempestTextKeys.CommandCheatOff);
                        break;
                    }
                    var outcome = TempestWorld.Stop(out var kind, out var dry);
                    yield return OutcomeText(outcome, kind, dry);
                    break;
                }
                case CommandVerb.Override:
                {
                    if (!settings.TryUseCheat(TempestSettings.Cheats.PersonalOverride))
                    {
                        yield return TempestText.Text(TempestTextKeys.CommandCheatOff);
                        break;
                    }
                    yield return TempestSky.SetPersonal(parsed.Environment!, parsed.Freeze)
                        ? TempestText.Format(TempestTextKeys.OverrideSet, parsed.Environment!)
                        : TempestText.Format(TempestTextKeys.OverrideUnknown, parsed.Environment!);
                    break;
                }
                case CommandVerb.OverrideReset:
                    TempestSky.ResetPersonal();
                    yield return TempestText.Text(TempestTextKeys.OverrideReset);
                    break;
            }
        }

        /// <summary>The console line for a summon or stop outcome (local or from the server's reply).</summary>
        public static string OutcomeText(SchedulerOutcome outcome, EventKind kind, bool dry = false)
        {
            string Kind(EventKind k) => TempestText.Text(TempestTextKeys.Kind(k, dry));
            switch (outcome)
            {
                case SchedulerOutcome.Started: return TempestText.Format(TempestTextKeys.CommandStarted, Kind(kind));
                case SchedulerOutcome.Stopped: return TempestText.Format(TempestTextKeys.CommandStopped, Kind(kind));
                case SchedulerOutcome.Busy: return TempestText.Text(TempestTextKeys.CommandBusy);
                case SchedulerOutcome.Refused: return TempestText.Format(TempestTextKeys.CommandNoBiome, Kind(kind));
                default: return TempestText.Text(TempestTextKeys.CommandNothing);
            }
        }

        /// <summary>The session as the command table sees it; the server kind only matters for remote clients.</summary>
        static SessionFacts Facts()
        {
            var net = ZNet.instance;
            if (net.IsServer())
                return new SessionFacts(net.IsDedicated() ? SessionKind.DedicatedServer : Player.m_localPlayer != null && net.GetPeers().Count > 0 ? SessionKind.Host : SessionKind.SinglePlayer,
                    ServerKind.Tempest, true);
            return new SessionFacts(SessionKind.RemoteClient, ServerKind.Tempest, ExtraPotions.Meadworks.Crucible.Plugin.Session.IsAdmin);
        }
    }
}
