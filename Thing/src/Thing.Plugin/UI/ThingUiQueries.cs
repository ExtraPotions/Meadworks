// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: read-only pages over existing Thing server providers and admin-command relay.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using ExtraPotions.Thing.Admin;
using ExtraPotions.Thing.Moderation;
using ExtraPotions.Thing.UI;

namespace ExtraPotions.Thing.Runtime
{
    public static class ThingUiQueries
    {
        public static bool TryRead(IReadOnlyList<string> args, Actor admin, bool fromLocalAuthority, bool requesterIsAdmin, out IReadOnlyList<string> lines, bool playersOnly = false)
        {
            lines = Array.Empty<string>();
            if (args.Count == 0 || !string.Equals(args[0], "ui-read", StringComparison.OrdinalIgnoreCase)) return false;
            if (ZNet.instance == null || !ZNet.instance.IsServer() || !ThingServer.Active || !ThingModules.ProductActive(out _))
            { lines = new[] { ThingText.Text("refused_product_off") }; return true; }
            // Remote callers have already passed the relay's live platform-ID/adminlist check; do not read first.
            if (!fromLocalAuthority && !requesterIsAdmin && !playersOnly)
            { lines = new[] { ThingText.Text("refused_not_admin") }; return true; }
            if (args.Count != 3 || !ThingUiPage.AdminReadPage(args[1]) || !int.TryParse(args[2], NumberStyles.None, CultureInfo.InvariantCulture, out var offset)
                || offset < 0 || offset >= ThingUiPage.MaxTotal || playersOnly && args[1] != "players")
            { lines = new[] { ThingText.Text(playersOnly && args.Count == 3 && args[1] != "players" ? "refused_not_admin" : "ui_read_invalid") }; return true; }
            try
            {
                IEnumerable<ThingUiRow> rows;
                if (args[1] == "players")
                    rows = ThingServer.Online().Where(p => ThingUiPage.PlatformId(p.Actor.Id)).OrderBy(p => p.Actor.Id, StringComparer.Ordinal)
                        .Select(p => new ThingUiRow("player", p.Actor.Id, p.Actor.Name, p.Actor.Id));
                else if (args[1] == "moderation")
                    rows = ThingServer.Ledger.Bans.Values.Concat(ThingServer.Ledger.Mutes.Values).Concat(ThingServer.Ledger.Warnings)
                        .Where(r => ThingUiPage.PlatformId(r.Target.Id)).OrderByDescending(r => r.AtUtc)
                        .Select(r => new ThingUiRow(r.Kind.ToString().ToLowerInvariant(), r.Target.Id, r.Target.Name,
                            r.AtUtc.ToString("u", CultureInfo.InvariantCulture) + " | " + (r.ExpiresUtc?.ToString("u", CultureInfo.InvariantCulture) ?? "-") + " | " + r.Reason));
                else if (args[1] == "permitted" || args[1] == "admins")
                    rows = ThingLists.Read(args[1] == "admins").Select(id => new ThingUiRow(args[1] == "admins" ? "admin" : "permit", id, id, ""));
                else if (args[1] == "grants") rows = ThingAccess.Rows();
                else rows = AuditTail();
                lines = ThingUiPage.Encode(args[1], offset, rows);
                var action = args[1] == "admins" ? ThingAction.AdminListView : args[1] == "permitted" ? ThingAction.PermitListView
                    : args[1] == "grants" ? ThingAction.GrantEdit : args[1] == "players" ? ThingAction.ListPlayers : ThingAction.AuditView;
                ThingServer.Audit(admin, action, default, AuditOutcome.Done, "UI page " + args[1] + " offset " + offset.ToString(CultureInfo.InvariantCulture));
            }
            catch { lines = new[] { ThingText.Text("ui_read_failed") }; }
            return true;
        }

        // At most three files, 64 KiB per file and 512 complete lines per file; never loads an entire log.
        static IEnumerable<ThingUiRow> AuditTail()
        {
            if (!Directory.Exists(ThingPaths.DataFolder)) yield break;
            var files = Directory.EnumerateFiles(ThingPaths.DataFolder, "audit-*.log").Take(512)
                .Where(path => AuditLog.TryDate(Path.GetFileName(path), out _)).OrderByDescending(path => Path.GetFileName(path), StringComparer.Ordinal).Take(3);
            var index = 0;
            foreach (var file in files)
            {
                string text; bool partial;
                using (var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    var length = (int)Math.Min(65536, stream.Length); partial = stream.Length > length;
                    stream.Seek(-length, SeekOrigin.End); var bytes = new byte[length]; var read = 0;
                    while (read < bytes.Length) { var count = stream.Read(bytes, read, bytes.Length - read); if (count == 0) break; read += count; }
                    text = Encoding.UTF8.GetString(bytes, 0, read);
                }
                var rows = text.Split('\n').Skip(partial ? 1 : 0).Where(line => line.Length <= 4096).Reverse().Take(512);
                foreach (var line in rows)
                    if (AuditEntry.TryParse(line.TrimEnd('\r'), out var entry))
                        yield return new ThingUiRow("audit", "audit-" + index++, entry.TimeUtc.ToString("u", CultureInfo.InvariantCulture) + " | " + entry.AdminName + " | " + ActionCatalog.Info(entry.Action).Verb,
                            entry.TargetName + " | " + entry.Outcome + " | " + entry.Detail);
            }
        }
    }
}
