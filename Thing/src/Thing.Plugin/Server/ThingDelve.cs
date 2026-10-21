// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: Thing's admin pages and commands for the Delve owner, through its meadworks.delve.admin capability
// (Crucible.Contracts DelveAdminContract; reply formats in Delve's DelveAdmin). No Delve assembly is referenced; Delve
// runs the regeneration, Thing keeps its admin checks, texts, pages and audit.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ExtraPotions.Meadworks.Contracts;
using ExtraPotions.Thing.Admin;
using ExtraPotions.Thing.Commands;
using ExtraPotions.Thing.Config;
using ExtraPotions.Thing.Moderation;
using ExtraPotions.Thing.UI;
using ExtraPotions.Meadworks.Crucible;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Thing.Runtime
{
    internal static class ThingDelve
    {
        static readonly CapabilityLink<Func<string, IReadOnlyList<string>, Tuple<bool, IReadOnlyList<string>>>> link =
            new CapabilityLink<Func<string, IReadOnlyList<string>, Tuple<bool, IReadOnlyList<string>>>>(DelveAdminContract.Id, DelveAdminContract.ContractVersion);

        /// <summary>The Delve owner is installed on this machine.</summary>
        internal static bool Present => CruciblePlugin.IsReady && CruciblePlugin.Services.Capabilities.Any(DelveAdminContract.Id);

        internal static IReadOnlyList<string> Run(ParsedCommand command, Actor caller, bool local)
        {
            if (!ThingServer.CurrentAuthority || !CruciblePlugin.Session.AuthorityReady || !ThingModules.ProductActive(out _)
                || (!local && !ZNet.instance.IsAdmin(caller.Id))) return new[] { ThingText.Text("refused_not_admin") };
            if (!Present) return new[] { ThingText.Text("refused_needs_delve") };
            try { return RunLoaded(command, caller, local); }
            catch { return new[] { ThingText.Text("delve_unavailable") }; }
        }

        /// <summary>One Delve operation's reply lines split into fields; throws when Delve cannot run it now (the callers answer "unavailable").</summary>
        static List<string[]> Call(string operation, params string[] arguments)
        {
            if (!link.TryGet(CruciblePlugin.Services.Capabilities, out var delve) || !DelveAdminContract.TryRun(delve, operation, arguments, out var lines))
                throw new InvalidOperationException("DelveUnavailable");
            return lines.Select(line => line.Split('\t')).ToList();
        }

        /// <summary>A long list read in pieces of the contract's line limit, up to the page limit.</summary>
        static List<string[]> CallAll(string operation, params string[] arguments)
        {
            var all = new List<string[]>();
            while (all.Count < ThingUiPage.MaxTotal)
            {
                var piece = Call(operation, arguments.Concat(new[] { "from=" + all.Count.ToString(CultureInfo.InvariantCulture) }).ToArray());
                all.AddRange(piece);
                if (piece.Count < DelveAdminContract.MaximumLines) break;
            }
            return all;
        }

        static string F(float value) => value.ToString("R", CultureInfo.InvariantCulture);
        static double D(string text) => double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
        static int I(string text) => int.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture);
        static string[] Caller(ZNetPeer? peer, bool platform = false) => peer == null ? Array.Empty<string>()
            : platform ? new[] { "caller=" + peer.m_uid.ToString(CultureInfo.InvariantCulture), "platform=" + peer.m_socket.GetHostName() }
            : new[] { "caller=" + peer.m_uid.ToString(CultureInfo.InvariantCulture) };

        static IReadOnlyList<string> RunLoaded(ParsedCommand command, Actor caller, bool local)
        {
            var status = Call(DelveAdminContract.Status);
            if (status.Count == 0 || status[0].Length != 2 || status[0][0] != "version") return Reply("delve_unavailable");
            var version = status[0][1];
            var peer = local ? null : ZNet.instance.GetPeers().FirstOrDefault(p => p != null && p.IsReady() && p.m_rpc != null && p.m_rpc.IsConnected()
                && p.m_socket != null && p.m_socket.GetHostName() == caller.Id);
            if (!local && (peer == null || !ZNet.instance.IsAdmin(caller.Id))) return Reply("refused_not_admin");
            if (command.Action == ThingAction.RegenerationPage)
            {
                var request = command.Regeneration!;
                if (request.Mode == "dryrun")
                {
                    if (Plugin.Settings?.Get(ThingSettings.Remote.Editing) != true) return Reply("refused_disabled_by_server");
                    if (request.Version != version) return Reply("message_stale");
                    string result;
                    try
                    {
                        var reply = Call(DelveAdminContract.DryRun, request.Kind, request.Enabled ? "on" : "off", "version=" + request.Version);
                        result = reply.Count == 1 && reply[0].Length == 1 && (reply[0][0] == "saved" || reply[0][0] == "stale" || reply[0][0] == "unknown")
                            ? "message_" + reply[0][0] : "message_failed";
                    }
                    catch { result = "message_failed"; }
                    ThingServer.Audit(caller, ThingAction.RemoteConfigWrite, default, result == "message_saved" ? AuditOutcome.Done : AuditOutcome.Failed, "Delve dry run " + request.Kind + ": " + result);
                    return Reply(result);
                }
                if (request.Mode == "due" || request.Mode == "nearest" || request.Mode == "nature")
                {
                    try
                    {
                        var rows = request.Mode == "nature" ? NatureRows(request) : SiteRows(request, peer, local);
                        ThingServer.Audit(caller, ThingAction.RegenerationPage, default, ThingUiPage.TryDecode(rows, out _) ? AuditOutcome.Done : AuditOutcome.Refused, "Delve " + request.Mode + " known sites read");
                        return rows;
                    }
                    catch { return Reply("delve_unavailable"); }
                }
                if (request.Mode == "actions")
                {
                    var actions = Call(DelveAdminContract.Actions);
                    ThingServer.Audit(caller, ThingAction.RegenerationPage, default, AuditOutcome.Done, "Delve " + request.Mode + " read");
                    return ThingUiPage.Encode("delve_actions", request.Offset, actions.Take(200)
                        .Select((fields, at) => new ThingUiRow("action", at.ToString(CultureInfo.InvariantCulture), string.Join("\t", fields), "")));
                }
                ThingServer.Audit(caller, ThingAction.RegenerationPage, default, AuditOutcome.Done, "Delve " + request.Mode + " read");
                return ThingUiPage.Encode("delve", request.Offset, status.Skip(1).Where(k => k.Length == 14 && k[0] == "kind").Select(k =>
                {
                    // kind Kind configuredDryRun checked tracked waiting deferred acted blocked delay clock effectiveDryRun active note
                    var detail = k[3] != "1" ? ThingText.Text("delve_not_checked") : ThingText.Format("delve_counts", I(k[4]), I(k[5]), I(k[6]), I(k[7]), I(k[8]));
                    detail += "\n" + ThingText.Format("delve_schedule", D(k[9]), k[10], k[11] == "1" ? ThingPanel.Ui("on") : ThingPanel.Ui("off"),
                        k[12] == "1" ? ThingPanel.Ui("on") : ThingPanel.Ui("off"));
                    if (!string.IsNullOrEmpty(k[13])) detail += "; " + k[13];
                    return new ThingUiRow(k[2] == "1" ? "dry_on" : "dry_off", k[1], k[1], detail);
                }).ToList(), version);
            }
            if (command.Action == ThingAction.ResetDungeons && command.Target == "nearest")
            {
                try { return ResetNearest(caller, peer, local); }
                catch { return Reply("delve_unavailable"); }
            }
            if (command.Action == ThingAction.RegenerateNature && command.Target == RegenerationRequest.Nature)
            {
                try { return Surface(true, caller, peer, local); }
                catch { return Reply("delve_unavailable"); }
            }
            IEnumerable<string> selected = command.Action == ThingAction.ResetDungeons ? new[] { "FullReset" }
                : command.Action == ThingAction.ResetResources ? new[] { "Chests", "MudPiles", "FrostCaveCrystals", "BlackCores", "Morkhalla", "Hole" }
                : new[] { command.Target };
            var answer = Call(DelveAdminContract.Request, selected.Concat(Caller(peer)).ToArray());
            if (answer.Count != 1 || answer[0].Length < 2 || answer[0][0] != "requested") return Reply("delve_unavailable");
            var verdict = answer[0][1]; var kinds = answer[0].Skip(2).ToList();
            ThingServer.Audit(caller, command.Action, default, kinds.Count > 0 ? AuditOutcome.Done : AuditOutcome.Refused,
                "Delve request " + string.Join(",", kinds) + "; " + verdict);
            var lines = new List<string> { kinds.Count == 0 ? ThingText.Text("delve_none_active")
                : ThingText.Format(verdict == "Allowed" ? "delve_requested_skip" : "delve_requested_due", string.Join(", ", kinds)) };
            if (command.Action == ThingAction.ResetResources)
            {
                // Parity row 26: Reset resources also brings back mined rocks, ore and picked stones around the admin.
                try { lines.AddRange(Surface(false, caller, peer, local)); }
                catch { lines.Add(ThingText.Text("delve_unavailable")); }
            }
            return lines;
        }

        /// <summary>Read-only known resource clocks from Delve's nature records.</summary>
        static IReadOnlyList<string> NatureRows(RegenerationRequest request)
        {
            // site Kind id prefab requested due remaining clock position dryRun
            return ThingUiPage.Encode("delve_nature", request.Offset, CallAll(DelveAdminContract.Nature, "0", "0").Where(s => s.Length == 10 && s[0] == "site").Take(ThingUiPage.MaxTotal).Select(s =>
            {
                var state = s[4] == "1" ? ThingText.Text("delve_site_requested") : s[6].Length == 0 ? ThingText.Text("delve_site_unknown")
                    : s[5] == "1" ? ThingText.Text("delve_site_due") : D(s[6]).ToString("0.##", CultureInfo.InvariantCulture) + " " + s[7];
                var detail = ThingText.Text("delve_kind_" + s[1].ToLowerInvariant()) + " | " + state
                    + " | " + s[8] + " | " + ThingPanel.Ui("delve_dryrun") + ": " + ThingPanel.Ui(s[9] == "1" ? "on" : "off");
                return new ThingUiRow("site", s[2], s[3], detail);
            }).ToList());
        }

        /// <summary>Read-only known dungeon sites: due ones, or the nearest to the authenticated caller's character.</summary>
        static IReadOnlyList<string> SiteRows(RegenerationRequest request, ZNetPeer? peer, bool local)
        {
            UnityEngine.Vector3 position = default;
            if (request.Mode == "nearest")
            {
                var character = local ? Player.m_localPlayer?.GetComponent<ZNetView>()?.GetZDO()
                    : peer == null ? null : ZDOMan.instance?.GetZDO(peer.m_characterID);
                if (character == null || character.m_uid.IsNone() || !local && character.GetOwner() != peer!.m_uid)
                    return Reply("delve_position_unknown");
                position = character.GetPosition();
                if (float.IsNaN(position.x) || float.IsInfinity(position.x) || float.IsNaN(position.z) || float.IsInfinity(position.z))
                    return Reply("delve_position_unknown");
            }
            // site Kind id generator requested due remaining clock distance dryRun, in Delve's kind and id order
            var sites = CallAll(DelveAdminContract.Sites, F(position.x), F(position.z)).Where(s => s.Length == 10 && s[0] == "site");
            var ordered = request.Mode == "due" ? sites.Where(s => s[5] == "1") : sites.OrderBy(s => D(s[8])); // stable: ties keep kind, id
            return ThingUiPage.Encode("delve_" + request.Mode, request.Offset, ordered.Take(ThingUiPage.MaxTotal).Select(s =>
            {
                var state = s[4] == "1" ? ThingText.Text("delve_site_requested") : s[6].Length == 0 ? ThingText.Text("delve_site_unknown")
                    : s[5] == "1" ? ThingText.Text("delve_site_due") : D(s[6]).ToString("0.##", CultureInfo.InvariantCulture) + " " + s[7];
                var detail = ThingText.Text("delve_kind_" + s[1].ToLowerInvariant()) + " | " + state
                    + (request.Mode == "nearest" ? " | " + D(s[8]).ToString("0", CultureInfo.InvariantCulture) + " m" : "")
                    + " | " + ThingPanel.Ui("delve_dryrun") + ": " + ThingPanel.Ui(s[9] == "1" ? "on" : "off");
                return new ThingUiRow("site", s[1] + ":" + s[2], s[3], detail);
            }).ToList());
        }

        /// <summary>One Delve request for the authenticated caller's nearest known FullReset site; Delve re-checks the caller when it runs.</summary>
        static IReadOnlyList<string> ResetNearest(Actor caller, ZNetPeer? peer, bool local)
        {
            var character = local ? Player.m_localPlayer?.GetComponent<ZNetView>()?.GetZDO()
                : peer == null ? null : ZDOMan.instance?.GetZDO(peer.m_characterID);
            if (character == null || character.m_uid.IsNone() || !local && character.GetOwner() != peer!.m_uid) return Reply("delve_position_unknown");
            var position = character.GetPosition();
            if (float.IsNaN(position.x) || float.IsInfinity(position.x) || float.IsNaN(position.z) || float.IsInfinity(position.z)) return Reply("delve_position_unknown");
            var reply = Call(DelveAdminContract.Nearest, new[] { F(position.x), F(position.z) }.Concat(Caller(peer, platform: true)).ToArray());
            var r = reply.Count == 1 ? reply[0] : Array.Empty<string>();
            if (r.Length == 1 && r[0] == "none") return Reply("delve_nearest_none");
            if (r.Length == 1 && r[0] == "position_unknown") return Reply("delve_position_unknown");
            bool queued = r.Length == 3 && r[0] == "queued";
            if (!queued && !(r.Length == 2 && r[0] == "refused")) return Reply("delve_unavailable");
            var id = queued ? r[2] : r[1];
            ThingServer.Audit(caller, ThingAction.ResetDungeons, default, queued ? AuditOutcome.Done : AuditOutcome.Refused,
                "Delve nearest known site " + id + (queued ? " queued for one guarded evaluation" : " refused"));
            return queued ? new[] { ThingText.Format("delve_nearest_queued", r[1], id) } : Reply("delve_nearest_refused");
        }

        /// <summary>Thing's Regenerate nature and Reset resources reach Delve's surface reset (parity rows 26, 27); Delve owns the reset.</summary>
        static IReadOnlyList<string> Surface(bool nature, Actor caller, ZNetPeer? peer, bool local)
        {
            var position = local ? (Player.m_localPlayer != null ? Player.m_localPlayer.transform.position : ZNet.instance.GetReferencePosition()) : peer!.GetRefPos();
            var lines = Call(nature ? DelveAdminContract.SurfaceNature : DelveAdminContract.SurfaceResources,
                new[] { F(position.x), F(position.y), F(position.z), caller.Name }.Concat(Caller(local ? null : peer)).ToArray())
                .Select(fields => string.Join("\t", fields)).ToList();
            ThingServer.Audit(caller, nature ? ThingAction.RegenerateNature : ThingAction.ResetResources, default, AuditOutcome.Done,
                "Delve surface reset " + (nature ? "nature" : "resources") + ": " + (lines.Count > 0 ? lines[0] : ""));
            return lines;
        }

        static IReadOnlyList<string> Reply(string key) => new[] { ThingText.Text(key) };
    }
}
