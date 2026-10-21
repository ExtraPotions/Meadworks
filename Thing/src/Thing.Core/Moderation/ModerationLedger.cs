// Thing - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: Thing's server-side moderation ledger (design A3, A4, A7, A8). The game's bannedlist.txt stays the
// game's; this ledger records who, why and until when, and drives the automatic lift of temporary bans and mutes.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using ExtraPotions.Thing.Admin;

namespace ExtraPotions.Thing.Moderation
{
    /// <summary>A player by platform ID (the key) and the name shown at the time.</summary>
    public readonly struct Actor
    {
        public Actor(string id, string name)
        {
            Id = (id ?? "").Trim();
            Name = name ?? "";
        }

        public string Id { get; }
        public string Name { get; }
    }

    public enum LedgerChange { Unchanged, Added, Updated, Removed }

    public enum RecordKind { Ban, Mute, Warn }

    /// <summary>One ledger record; <see cref="ExpiresUtc"/> null means until lifted (bans, mutes) or not applicable (warnings).</summary>
    public sealed class LedgerRecord
    {
        public LedgerRecord(RecordKind kind, Actor target, Actor admin, string reason, DateTime atUtc, DateTime? expiresUtc)
        {
            Kind = kind;
            Target = target;
            Admin = admin;
            Reason = reason ?? "";
            AtUtc = atUtc;
            ExpiresUtc = expiresUtc;
        }

        public RecordKind Kind { get; }
        public Actor Target { get; }
        public Actor Admin { get; }
        public string Reason { get; }
        public DateTime AtUtc { get; }
        public DateTime? ExpiresUtc { get; }

        public bool ActiveAt(DateTime nowUtc) => ExpiresUtc == null || nowUtc < ExpiresUtc.Value;
    }

    public sealed class ModerationLedger
    {
        const string Header = "# Thing moderation ledger (managed by Thing on the server; one entry per line, tab-separated)";
        const string TimeFormat = "yyyy-MM-dd'T'HH:mm:ss'Z'";

        readonly Dictionary<string, LedgerRecord> bans = new Dictionary<string, LedgerRecord>(StringComparer.Ordinal);
        readonly Dictionary<string, LedgerRecord> mutes = new Dictionary<string, LedgerRecord>(StringComparer.Ordinal);
        readonly List<LedgerRecord> warnings = new List<LedgerRecord>();

        public IReadOnlyDictionary<string, LedgerRecord> Bans => bans;
        public IReadOnlyDictionary<string, LedgerRecord> Mutes => mutes;
        public IReadOnlyList<LedgerRecord> Warnings => warnings;

        /// <summary>Bans or re-bans; <paramref name="duration"/> null is permanent. Unchanged when reason and expiry already match.</summary>
        public LedgerChange Ban(Actor target, Actor admin, string reason, DateTime nowUtc, TimeSpan? duration) => Put(bans, RecordKind.Ban, target, admin, reason, nowUtc, duration);

        public LedgerChange Unban(string id) => bans.Remove((id ?? "").Trim()) ? LedgerChange.Removed : LedgerChange.Unchanged;

        public bool IsBanned(string id, DateTime nowUtc) => bans.TryGetValue((id ?? "").Trim(), out var r) && r.ActiveAt(nowUtc);

        /// <summary>Removes and returns the temporary bans whose time has come; the server then lifts them in the game's list.</summary>
        public IReadOnlyList<LedgerRecord> TakeExpired(DateTime nowUtc) => Take(bans, nowUtc);

        public LedgerChange Mute(Actor target, Actor admin, string reason, DateTime nowUtc, TimeSpan? duration) => Put(mutes, RecordKind.Mute, target, admin, reason, nowUtc, duration);

        public LedgerChange Unmute(string id) => mutes.Remove((id ?? "").Trim()) ? LedgerChange.Removed : LedgerChange.Unchanged;

        public bool IsMuted(string id, DateTime nowUtc) => mutes.TryGetValue((id ?? "").Trim(), out var r) && r.ActiveAt(nowUtc);

        public IReadOnlyList<LedgerRecord> TakeExpiredMutes(DateTime nowUtc) => Take(mutes, nowUtc);

        public void Warn(Actor target, Actor admin, string reason, DateTime nowUtc)
        {
            Require(target);
            warnings.Add(new LedgerRecord(RecordKind.Warn, target, admin, Reasons.Normalize(reason), Second(nowUtc), null));
        }

        public IReadOnlyList<LedgerRecord> WarningsFor(string id) => warnings.Where(w => w.Target.Id == (id ?? "").Trim()).ToList();

        /// <summary>The ledger file text: a header comment, bans and mutes by platform ID, then warnings in order.</summary>
        public string Save()
        {
            var b = new StringBuilder(Header).Append('\n');
            foreach (var r in bans.Values.OrderBy(r => r.Target.Id, StringComparer.Ordinal)) b.Append(Line(r)).Append('\n');
            foreach (var r in mutes.Values.OrderBy(r => r.Target.Id, StringComparer.Ordinal)) b.Append(Line(r)).Append('\n');
            foreach (var r in warnings) b.Append(Line(r)).Append('\n');
            return b.ToString();
        }

        /// <summary>Reads a ledger file; comment and blank lines are ignored, malformed lines are counted in <paramref name="skipped"/>.</summary>
        public static ModerationLedger Load(string? text, out int skipped)
        {
            var ledger = new ModerationLedger();
            skipped = 0;
            foreach (var raw in (text ?? "").Split('\n'))
            {
                var line = raw.TrimEnd('\r');
                if (line.Trim().Length == 0 || line.StartsWith("#", StringComparison.Ordinal)) continue;
                if (!TryParse(line, out var r) || r.Target.Id.Length == 0) { skipped++; continue; }
                switch (r.Kind)
                {
                    case RecordKind.Ban: ledger.bans[r.Target.Id] = r; break;
                    case RecordKind.Mute: ledger.mutes[r.Target.Id] = r; break;
                    default: ledger.warnings.Add(r); break;
                }
            }
            return ledger;
        }

        static LedgerChange Put(Dictionary<string, LedgerRecord> map, RecordKind kind, Actor target, Actor admin, string reason, DateTime nowUtc, TimeSpan? duration)
        {
            Require(target);
            if (duration.HasValue && duration.Value <= TimeSpan.Zero) throw new ArgumentException("Expected a positive duration.", nameof(duration));
            var at = Second(nowUtc);
            var expires = duration.HasValue ? at + duration.Value : (DateTime?)null;
            var text = Reasons.Normalize(reason);
            if (map.TryGetValue(target.Id, out var old))
            {
                if (old.Reason == text && old.ExpiresUtc == null && expires == null) return LedgerChange.Unchanged;
                map[target.Id] = new LedgerRecord(kind, target, admin, text, at, expires);
                return LedgerChange.Updated;
            }
            map[target.Id] = new LedgerRecord(kind, target, admin, text, at, expires);
            return LedgerChange.Added;
        }

        static IReadOnlyList<LedgerRecord> Take(Dictionary<string, LedgerRecord> map, DateTime nowUtc)
        {
            var due = map.Values.Where(r => !r.ActiveAt(nowUtc)).OrderBy(r => r.Target.Id, StringComparer.Ordinal).ToList();
            foreach (var r in due) map.Remove(r.Target.Id);
            return due;
        }

        static void Require(Actor target)
        {
            if (target.Id.Length == 0) throw new ArgumentException("A moderation target needs a platform ID.", nameof(target));
        }

        static DateTime Second(DateTime utc) => DateTime.SpecifyKind(new DateTime(utc.Ticks - utc.Ticks % TimeSpan.TicksPerSecond), DateTimeKind.Utc);

        static string Time(DateTime utc) => utc.ToString(TimeFormat, CultureInfo.InvariantCulture);

        static string Line(LedgerRecord r) => string.Join("\t", r.Kind.ToString().ToLowerInvariant(), AuditEntry.Escape(r.Target.Id), AuditEntry.Escape(r.Target.Name),
            AuditEntry.Escape(r.Admin.Id), AuditEntry.Escape(r.Admin.Name), Time(r.AtUtc), r.ExpiresUtc.HasValue ? Time(r.ExpiresUtc.Value) : "-", AuditEntry.Escape(r.Reason));

        static bool TryParse(string line, out LedgerRecord record)
        {
            record = null!;
            var f = line.Split('\t');
            if (f.Length != 8) return false;
            RecordKind kind;
            switch (f[0])
            {
                case "ban": kind = RecordKind.Ban; break;
                case "mute": kind = RecordKind.Mute; break;
                case "warn": kind = RecordKind.Warn; break;
                default: return false;
            }
            if (!TryTime(f[5], out var at)) return false;
            DateTime? expires = null;
            if (f[6] != "-")
            {
                if (kind == RecordKind.Warn || !TryTime(f[6], out var e)) return false;
                expires = e;
            }
            if (!AuditEntry.TryUnescape(f[1], out var id) || !AuditEntry.TryUnescape(f[2], out var name) || !AuditEntry.TryUnescape(f[3], out var adminId)
                || !AuditEntry.TryUnescape(f[4], out var adminName) || !AuditEntry.TryUnescape(f[7], out var reason)) return false;
            record = new LedgerRecord(kind, new Actor(id, name), new Actor(adminId, adminName), reason, at, expires);
            return true;
        }

        static bool TryTime(string text, out DateTime utc)
        {
            var ok = DateTime.TryParseExact(text, TimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out utc);
            utc = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
            return ok;
        }
    }
}
