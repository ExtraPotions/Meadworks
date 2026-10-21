// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: bounded pages carried by Thing's existing 16-line admin command reply.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using ExtraPotions.Thing.Admin;
using ExtraPotions.Thing.Communication;

namespace ExtraPotions.Thing.UI
{
    public sealed class ThingUiRow
    {
        public ThingUiRow(string kind, string id, string caption, string detail)
        { Kind = kind; Id = id; Caption = caption; Detail = detail; }
        public string Kind { get; }
        public string Id { get; }
        public string Caption { get; }
        public string Detail { get; }
    }

    public sealed class ThingUiPage
    {
        public const int PageSize = 12, MaxTotal = 10000;
        const string Marker = "THING_UI_PAGE";
        public string Page { get; }
        public int Offset { get; }
        public int Total { get; }
        public string Version { get; }
        public IReadOnlyList<ThingUiRow> Rows { get; }
        ThingUiPage(string page, int offset, int total, IReadOnlyList<ThingUiRow> rows, string version = "")
        { Page = page; Offset = offset; Total = total; Rows = rows; Version = version; }

        public static bool Known(string page) => page == "players" || page == "moderation" || page == "audit" || page == "permitted" || page == "admins" || page == "announcements" || page == "delve" || page == "delve_actions" || page == "delve_due" || page == "delve_nearest" || page == "delve_nature" || page == "config_files" || page == "config_entries";
        public static bool AdminReadPage(string page) => Known(page) && page != "announcements" && page != "delve" && page != "delve_actions" && page != "delve_due" && page != "delve_nearest" && page != "delve_nature" && page != "config_files" && page != "config_entries";
        public static bool PlatformId(string id) => !string.IsNullOrWhiteSpace(id) && Encoding.UTF8.GetByteCount(id) <= 256
            && id.All(c => char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == ':' || c == '.');

        public static IReadOnlyList<string> Encode(string page, int offset, IEnumerable<ThingUiRow> source, string version = "")
        {
            if (version.Length > 0 && !MessageEditing.Version(version)) throw new ArgumentException("Invalid page version.");
            if (!Known(page) || offset < 0 || offset >= MaxTotal) throw new ArgumentException("Invalid Thing page.");
            var rows = source.Take(MaxTotal).ToList();
            var result = new List<string> { Marker + "\t" + page + "\t" + offset.ToString(CultureInfo.InvariantCulture) + "\t" + rows.Count.ToString(CultureInfo.InvariantCulture) + (version.Length == 0 ? "" : "\t" + version) };
            foreach (var row in rows.Skip(offset).Take(PageSize))
            {
                if (!PlatformId(row.Id)) throw new ArgumentException("Invalid row identity.");
                result.Add(string.Join("\t", AuditEntry.Escape(Clip(row.Kind, 16)), AuditEntry.Escape(row.Id),
                    AuditEntry.Escape(Clip(row.Caption, 128)), AuditEntry.Escape(Clip(row.Detail, 224))));
            }
            return result;
        }

        // Escape expansion is at most two bytes per input byte; the full encoded row stays below the relay's 1024 bytes.
        static string Clip(string text, int bytes)
        {
            text ??= "";
            var count = 0; var length = 0;
            foreach (var c in text)
            {
                var cost = char.IsSurrogate(c) ? 3 : Encoding.UTF8.GetByteCount(c.ToString());
                if (count + cost > bytes) break;
                count += cost; length++;
            }
            if (length > 0 && char.IsHighSurrogate(text[length - 1])) length--;
            return text.Substring(0, length);
        }

        public static bool TryDecode(IReadOnlyList<string> lines, out ThingUiPage page)
        {
            page = null!;
            if (lines == null || lines.Count < 1 || lines.Count > PageSize + 1) return false;
            var header = lines[0].Split('\t');
            if ((header.Length != 4 && header.Length != 5) || (header.Length == 5 && !MessageEditing.Version(header[4])) || header[0] != Marker || !Known(header[1])
                || !int.TryParse(header[2], NumberStyles.None, CultureInfo.InvariantCulture, out var offset) || offset < 0 || offset >= MaxTotal
                || !int.TryParse(header[3], NumberStyles.None, CultureInfo.InvariantCulture, out var total) || total < 0 || total > MaxTotal
                || lines.Count - 1 != Math.Min(PageSize, Math.Max(0, total - offset))) return false;
            var rows = new List<ThingUiRow>();
            foreach (var line in lines.Skip(1))
            {
                var parts = line.Split('\t'); var fields = new string[4];
                if (parts.Length != 4) return false;
                for (var i = 0; i < fields.Length; i++) if (!AuditEntry.TryUnescape(parts[i], out fields[i])) return false;
                if (!PlatformId(fields[1])) return false;
                rows.Add(new ThingUiRow(fields[0], fields[1], fields[2], fields[3]));
            }
            page = new ThingUiPage(header[1], offset, total, rows, header.Length == 5 ? header[4] : ""); return true;
        }
    }
}
