// Thing - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the `thing` console command (design 7). On the authority (single player, host) verbs run locally. A
// remote client sends them through the temporary remote admin path (ThingRemoteCommands, user decision 2026-10-08) until
// Crucible's CS-D1 relay lands; the server re-checks the caller's admin status and runs the same code with the caller as admin.
using System;
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Thing.Admin;
using ExtraPotions.Thing.Commands;
using ExtraPotions.Thing.Config;
using ExtraPotions.Thing.Integration;
using ExtraPotions.Thing.Localization;
using ExtraPotions.Thing.Moderation;
using Jotunn.Entities;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Thing.Runtime
{
    public sealed class ThingCommand : ConsoleCommand
    {
        public override string Name => CommandParser.Name;

        public override string Help => "Thing server administration: help, status, kick, ban, tempban, unban, warn, mute, unmute, freeze <player>, unfreeze <player>, say, saytop, shutdown, cancelshutdown, save, permitted [offset], permit <platform id>, unpermit <platform id>, admins [offset], admin add|remove <platform id>, grant self, grant self remove, grant <platform id> <world|spawn|teleport|freeze|inventory|ward|subadmin> <on|off>, seed, time <HH:mm|preset> [freeze], resumetime, ward [on|off], terrain <radius>, terrain confirm, terrain cancel, drops, drops confirm, drops cancel, pickup <1-10|reset>, regenerate nature, resetresources.";

        public override void Run(string[] args)
        {
            if (ThingTeleportCommands.Run(args)) return;
            if (ThingTerrainCommands.Run(args)) return;
            if (ThingDropCommands.Run(args)) return;
            if (ThingWard.Run(args)) return;
            if (ThingPickup.Run(args)) return;
            var net = ZNet.instance;
            var parsed = CommandParser.Parse(args);
            if (parsed.Kind == CommandKind.Action && parsed.Action == ThingAction.ShowSeed && !ThingModules.Active(ThingModule.WorldTools))
            {
                global::Console.instance?.Print(ThingText.Text(AdminCheck.TextKey(Verdict.ProductOff)));
                return;
            }
            // Temporary until Crucible's CS-D1 relay exists: a remote client sends server verbs to the server's Thing.
            if (net != null && !net.IsServer() && RemoteAdmin.Sendable(parsed) && ThingRemoteCommands.Available && ThingModules.ProductActive(out _))
            {
                ThingRemoteCommands.Send(args);
                global::Console.instance?.Print(ThingText.Text(ThingTextKeys.CommandRemoteSent));
                return;
            }
            foreach (var line in Execute(args, Admin(), fromLocalAuthority: true, requesterIsAdmin: true)) global::Console.instance?.Print(line);
        }

        /// <summary>
        /// Runs a verb on this machine. The local console passes the host or server as admin; the remote admin path passes the
        /// caller (platform ID and name) with <paramref name="fromLocalAuthority"/> false and its server-checked admin status.
        /// </summary>
        public static IEnumerable<string> Execute(IReadOnlyList<string> args, Actor admin, bool fromLocalAuthority, bool requesterIsAdmin, string requesterConnection = "")
        {
            var parsed = CommandParser.Parse(args);
            switch (parsed.Kind)
            {
                case CommandKind.Help: yield return ThingText.Text(ThingTextKeys.CommandHelp); yield break;
                case CommandKind.Unknown: yield return ThingText.Format(ThingTextKeys.CommandUnknownVerb, parsed.Text); yield break;
                case CommandKind.Usage: yield return ThingText.Format(ThingTextKeys.CommandUsage, parsed.Text); yield break;
            }
            var settings = Plugin.Settings;
            if (settings == null || !ThingModules.ProductActive(out _) || ZNet.instance == null)
            {
                yield return ThingText.Text(AdminCheck.TextKey(Verdict.ProductOff));
                yield break;
            }
            if (!ZNet.instance.IsServer() || !ThingServer.Active)
            {
                yield return ThingText.Text(AdminCheck.TextKey(Verdict.NeedsRelay));
                yield break;
            }
            if (parsed.Kind == CommandKind.Action && parsed.Action == ThingAction.GrantEdit && parsed.Text == "self")
            {
                var fullAdmin = fromLocalAuthority || requesterIsAdmin;
                yield return AdminPermissions.Wire(fullAdmin, fullAdmin ? AdminRight.All : ThingAccess.Of(admin.Id));
                yield break;
            }
            var playerPage = !fromLocalAuthority && !requesterIsAdmin && parsed.Kind == CommandKind.Status && args.Count >= 2
                && string.Equals(args[0], "ui-read", StringComparison.OrdinalIgnoreCase) && string.Equals(args[1], "players", StringComparison.OrdinalIgnoreCase)
                && AdminPermissions.Covers(ThingAccess.Of(admin.Id), ThingAction.InventoryView);
            if (playerPage && ThingUiQueries.TryRead(args, admin, fromLocalAuthority, requesterIsAdmin, out var grantedPage, playersOnly: true))
            {
                foreach (var line in grantedPage) yield return line;
                yield break;
            }
            var facts = new RequestFacts(Role(), fromLocalAuthority, requesterIsAdmin, delvePresent: ThingDelve.Present,
                granted: fromLocalAuthority || requesterIsAdmin ? AdminRight.None : ThingAccess.Of(admin.Id));
            if (!ActionCatalog.Visible(parsed.Action, ThingModules.StepAside.AdminToolLoaded))
            {
                yield return ThingText.Text(AdminCheck.TextKey(Verdict.ProductOff));
                yield break;
            }
            var verdict = AdminCheck.Decide(parsed.Action, facts, settings.Rules());
            if (!AdminCheck.Permits(verdict))
            {
                yield return ThingText.Text(AdminCheck.TextKey(verdict));
                yield break;
            }
            if (ThingUiQueries.TryRead(args, admin, fromLocalAuthority, requesterIsAdmin, out var pageLines))
            {
                foreach (var line in pageLines) yield return line;
                yield break;
            }
            if (parsed.Kind == CommandKind.Status)
            {
                var l = ThingServer.Ledger;
                yield return ThingText.Format(ThingTextKeys.CommandStatus, l.Bans.Count, l.Bans.Values.Count(b => b.ExpiresUtc != null), l.Mutes.Count, l.Warnings.Count);
                yield break;
            }
            var needsReason = parsed.Action == ThingAction.Kick || parsed.Action == ThingAction.Ban || parsed.Action == ThingAction.TempBan;
            if (needsReason && !Reasons.Acceptable(parsed.Text, settings.Get(ThingSettings.Moderation.KickReasonRequired)))
            {
                yield return ThingText.Text(ThingTextKeys.CommandReasonRequired);
                yield break;
            }
            var online = ThingServer.Online();
            OnlinePlayer? target = null;
            if (parsed.Target.Length > 0 && parsed.Target != "self")
            {
                var found = PlayerMatch.Find(parsed.Target, online.Select(o => o.Actor));
                if (found.HasValue) target = online.First(o => o.Actor.Id == found.Value.Id);
            }
            switch (parsed.Action)
            {
                case ThingAction.RemoteConfigRead:
                case ThingAction.RemoteConfigWrite:
                    if (parsed.ConfigFile == null) { yield return ThingText.Text("config_invalid"); yield break; }
                    foreach (var line in ThingConfigFiles.Run(parsed.ConfigFile, admin, fromLocalAuthority, requesterConnection)) yield return line;
                    yield break;
                case ThingAction.PermitListView:
                case ThingAction.PermitAdd:
                case ThingAction.PermitRemove:
                case ThingAction.AdminListView:
                case ThingAction.AdminListEdit:
                    foreach (var line in ThingLists.Run(parsed, admin, fromLocalAuthority)) yield return line;
                    yield break;
                case ThingAction.GrantEdit:
                    if (parsed.Target == "self") parsed = new ParsedCommand(parsed.Kind, parsed.Action, admin.Id, parsed.Text);
                    foreach (var line in ThingAccess.Edit(parsed, admin, fromLocalAuthority || requesterIsAdmin)) yield return line;
                    yield break;
                case ThingAction.MessageOfTheDay:
                case ThingAction.Announcements:
                    if (parsed.Message == null) { yield return ThingText.Text("message_invalid"); yield break; }
                    foreach (var line in ThingMessages.Run(parsed.Message, admin, fromLocalAuthority)) yield return line;
                    yield break;
                case ThingAction.RegenerationPage:
                case ThingAction.RegenerateNature:
                case ThingAction.ResetDungeons:
                case ThingAction.ResetResources:
                    foreach (var line in ThingDelve.Run(parsed, admin, fromLocalAuthority)) yield return line;
                    yield break;
                case ThingAction.GlobalTime:
                case ThingAction.ReleaseTime:
                    yield return ThingTime.Run(parsed, admin, fromLocalAuthority);
                    yield break;
                case ThingAction.Kick:
                case ThingAction.Warn:
                    if (!target.HasValue) { yield return ThingText.Format(ThingTextKeys.CommandUnknownPlayer, parsed.Target); yield break; }
                    if (parsed.Action == ThingAction.Kick) ThingServer.Kick(target.Value, admin, parsed.Text);
                    else if (!ThingServer.WarnWithPersistence(target.Value, admin, parsed.Text))
                    {
                        yield return PersistenceFailed(parsed.Action, target.Value.Actor);
                        yield break;
                    }
                    yield return Done(parsed.Action, target.Value.Actor);
                    yield break;
                case ThingAction.Ban:
                case ThingAction.TempBan:
                    {
                        // Offline players are banned by platform ID only; a name never stands for an ID.
                        var actor = target?.Actor ?? (LooksLikeId(parsed.Target) ? new Actor(parsed.Target, "") : (Actor?)null);
                        if (!actor.HasValue) { yield return ThingText.Format(ThingTextKeys.CommandUnknownPlayer, parsed.Target); yield break; }
                        var result = ThingServer.BanWithPersistence(actor.Value, target, admin, parsed.Text,
                            parsed.Action == ThingAction.TempBan ? parsed.Duration : null);
                        yield return result.Saved ? Done(parsed.Action, actor.Value) : PersistenceFailed(parsed.Action, actor.Value);
                        yield break;
                    }
                case ThingAction.Unban:
                    var unban = ThingServer.UnbanWithPersistence(parsed.Target, admin);
                    yield return unban.Saved ? Done(parsed.Action, new Actor(parsed.Target, ""))
                        : PersistenceFailed(parsed.Action, new Actor(parsed.Target, ""));
                    yield break;
                case ThingAction.Mute:
                    if (!target.HasValue) { yield return ThingText.Format(ThingTextKeys.CommandUnknownPlayer, parsed.Target); yield break; }
                    var duration = parsed.UntilLifted ? (System.TimeSpan?)null : parsed.Duration
                        ?? System.TimeSpan.FromMinutes(settings.Get(ThingSettings.Moderation.MuteDefaultMinutes));
                    yield return MuteReply(ThingServer.Mute(target.Value, admin, parsed.Text, duration), parsed.Action, target.Value.Actor);
                    yield break;
                case ThingAction.FreezePlayer:
                case ThingAction.UnfreezePlayer:
                    if (!target.HasValue) { yield return ThingText.Format(ThingTextKeys.CommandUnknownPlayer, parsed.Target); yield break; }
                    yield return ThingServer.SetFrozen(target.Value, admin, parsed.Action == ThingAction.FreezePlayer)
                        ? ThingText.Text("ui_pending")
                        : ThingText.Format(ThingTextKeys.CommandFailed, ActionCatalog.Info(parsed.Action).Verb + " " + target.Value.Actor.Id);
                    yield break;
                case ThingAction.Unmute:
                    var mutedActor = target?.Actor ?? (LooksLikeId(parsed.Target) ? new Actor(parsed.Target, "") : (Actor?)null);
                    if (!mutedActor.HasValue) { yield return ThingText.Format(ThingTextKeys.CommandUnknownPlayer, parsed.Target); yield break; }
                    yield return MuteReply(ThingServer.Unmute(mutedActor.Value, target, admin), parsed.Action, mutedActor.Value);
                    yield break;
                case ThingAction.Broadcast:
                    var sent = ThingServer.Say(admin, parsed.Text, parsed.TopLeft);
                    yield return ThingText.Format(sent ? ThingTextKeys.CommandDone : ThingTextKeys.CommandFailed, parsed.TopLeft ? "saytop" : "say");
                    yield break;
                case ThingAction.Shutdown:
                    ThingServer.StartShutdown(admin, parsed.Minutes);
                    yield return ThingText.Format(ThingTextKeys.CommandDone, "shutdown " + parsed.Minutes);
                    yield break;
                case ThingAction.CancelShutdown:
                    yield return ThingServer.CancelShutdown(admin) ? ThingText.Format(ThingTextKeys.CommandDone, "cancelshutdown")
                        : ThingText.Format(ThingTextKeys.CommandFailed, "no shutdown is running");
                    yield break;
                case ThingAction.SaveWorld:
                    ThingServer.SaveWorld(admin);
                    yield return ThingText.Format(ThingTextKeys.CommandDone, "save");
                    yield break;
                case ThingAction.ShowSeed:
                    var world = ZNet.instance.GetWorld();
                    if (world == null) { yield return ThingText.Text("seed_unavailable"); yield break; }
                    ThingServer.Audit(admin, parsed.Action, default, AuditOutcome.Done, "world seed requested");
                    yield return ThingText.Format("seed_value", world.m_seedName);
                    yield break;
                default:
                    yield return ThingText.Format(ThingTextKeys.CommandUnknownVerb, ActionCatalog.Info(parsed.Action).Verb);
                    yield break;
            }
        }

        static string MuteReply(MuteWriteResult result, ThingAction action, Actor target) => result == MuteWriteResult.Saved
            ? Done(action, target) : result == MuteWriteResult.LiveOnly ? ThingText.Text("mute_persistence_unknown")
            : ThingText.Text("refused_product_off");

        static string PersistenceFailed(ThingAction action, Actor target) => ThingText.Format(ThingTextKeys.CommandFailed,
            ActionCatalog.Info(action).Verb + " " + (target.Name.Length > 0 ? target.Name : target.Id));

        static string Done(ThingAction action, Actor target) =>
            ThingText.Format(ThingTextKeys.CommandDone, ActionCatalog.Info(action).Verb + " " + (target.Name.Length > 0 ? target.Name + " (" + target.Id + ")" : target.Id));

        static bool LooksLikeId(string text) => text.Length > 0 && (text.IndexOf('_') > 0 || text.All(char.IsDigit));

        static ThingRole Role()
        {
            var role = CruciblePlugin.IsReady ? CruciblePlugin.Session.Role : SessionRole.SinglePlayer;
            switch (role)
            {
                case SessionRole.Host: return ThingRole.Host;
                case SessionRole.DedicatedServer: return ThingRole.DedicatedServer;
                default: return ThingRole.SinglePlayer;
            }
        }

        static Actor Admin()
        {
            var name = Player.m_localPlayer != null ? Player.m_localPlayer.GetPlayerName() : "console";
            return new Actor(ZNet.instance != null && ZNet.instance.IsDedicated() ? "server" : "host", name);
        }
    }
}
