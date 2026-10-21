// Thing - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the audit log of design A9 and E5 (separate from the game log, one file per UTC day, Audit Log Days).
using System;
using System.Globalization;
using System.Text;

namespace ExtraPotions.Thing.Admin
{
    public enum AuditOutcome { Done, Refused, Failed }

    /// <summary>
    /// One audit line: UTC time, admin platform ID and name, action verb, target platform ID and name, outcome, detail.
    /// Fields are tab-separated; backslash, tab, carriage return and newline are escaped so an entry is always one line.
    /// </summary>
    public sealed class AuditEntry
    {
        const string TimeFormat = "yyyy-MM-dd'T'HH:mm:ss'Z'";

        public AuditEntry(DateTime timeUtc, string adminId, string adminName, ThingAction action, string targetId, string targetName, AuditOutcome outcome, string detail)
        {
            TimeUtc = DateTime.SpecifyKind(new DateTime(timeUtc.Ticks - timeUtc.Ticks % TimeSpan.TicksPerSecond), DateTimeKind.Utc);
            AdminId = adminId ?? "";
            AdminName = adminName ?? "";
            Action = action;
            TargetId = targetId ?? "";
            TargetName = targetName ?? "";
            Outcome = outcome;
            Detail = detail ?? "";
        }

        public DateTime TimeUtc { get; }
        public string AdminId { get; }
        public string AdminName { get; }
        public ThingAction Action { get; }
        public string TargetId { get; }
        public string TargetName { get; }
        public AuditOutcome Outcome { get; }
        public string Detail { get; }

        public string ToLine() => string.Join("\t", TimeUtc.ToString(TimeFormat, CultureInfo.InvariantCulture), Escape(AdminId), Escape(AdminName),
            ActionCatalog.Info(Action).Verb, Escape(TargetId), Escape(TargetName), Outcome.ToString().ToLowerInvariant(), Escape(Detail));

        public static bool TryParse(string? line, out AuditEntry entry)
        {
            entry = null!;
            if (string.IsNullOrEmpty(line)) return false;
            var f = line!.Split('\t');
            if (f.Length != 8) return false;
            if (!DateTime.TryParseExact(f[0], TimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var time)) return false;
            var info = ActionCatalog.FromVerb(f[3]);
            if (info == null) return false;
            AuditOutcome outcome;
            switch (f[6])
            {
                case "done": outcome = AuditOutcome.Done; break;
                case "refused": outcome = AuditOutcome.Refused; break;
                case "failed": outcome = AuditOutcome.Failed; break;
                default: return false;
            }
            if (!TryUnescape(f[1], out var adminId) || !TryUnescape(f[2], out var adminName) || !TryUnescape(f[4], out var targetId)
                || !TryUnescape(f[5], out var targetName) || !TryUnescape(f[7], out var detail)) return false;
            entry = new AuditEntry(DateTime.SpecifyKind(time, DateTimeKind.Utc), adminId, adminName, info.Action, targetId, targetName, outcome, detail);
            return true;
        }

        public static string Escape(string text)
        {
            var b = new StringBuilder(text.Length);
            foreach (var c in text)
            {
                switch (c)
                {
                    case '\\': b.Append("\\\\"); break;
                    case '\t': b.Append("\\t"); break;
                    case '\r': b.Append("\\r"); break;
                    case '\n': b.Append("\\n"); break;
                    default: b.Append(c); break;
                }
            }
            return b.ToString();
        }

        public static bool TryUnescape(string text, out string value)
        {
            var b = new StringBuilder(text.Length);
            value = "";
            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];
                if (c != '\\') { b.Append(c); continue; }
                if (++i >= text.Length) return false;
                switch (text[i])
                {
                    case '\\': b.Append('\\'); break;
                    case 't': b.Append('\t'); break;
                    case 'r': b.Append('\r'); break;
                    case 'n': b.Append('\n'); break;
                    default: return false;
                }
            }
            value = b.ToString();
            return true;
        }
    }

    /// <summary>Audit files <c>audit-yyyy-MM-dd.log</c> in the data folder, one per UTC day, kept for Audit Log Days (1 to 365).</summary>
    public static class AuditLog
    {
        const string Prefix = "audit-";
        const string Suffix = ".log";

        public static int ClampDays(int days) => Math.Min(365, Math.Max(1, days));

        public static string FileName(DateTime utc) => Prefix + utc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + Suffix;

        public static bool TryDate(string? fileName, out DateTime date)
        {
            date = default;
            if (fileName == null || !fileName.StartsWith(Prefix, StringComparison.Ordinal) || !fileName.EndsWith(Suffix, StringComparison.Ordinal)) return false;
            var mid = fileName.Substring(Prefix.Length, fileName.Length - Prefix.Length - Suffix.Length);
            if (!DateTime.TryParseExact(mid, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)) return false;
            date = DateTime.SpecifyKind(d.Date, DateTimeKind.Utc);
            return true;
        }

        /// <summary>An audit file older than the retention; never true for a file that is not an audit file.</summary>
        public static bool Expired(string fileName, DateTime nowUtc, int days) =>
            TryDate(fileName, out var date) && (nowUtc.Date - date).TotalDays >= ClampDays(days);
    }
}
