// Delve - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the status model (design B10): the day, each kind's tracked, waiting, deferred, acted and blocked
// counts from its last sweep, dry-run state and the last actions (General / Status Report Lines). Thing draws it later;
// the kick fallback prints it.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ExtraPotions.Delve.Engine
{
    /// <summary>One kind's counts from its last sweep.</summary>
    public sealed class KindReport
    {
        public KindReport(int tracked, int waiting, int deferred, int acted, IReadOnlyDictionary<GuardResult, int>? blocked, bool active, bool dryRun, string? note = null)
        {
            Tracked = tracked;
            Waiting = waiting;
            Deferred = deferred;
            Acted = acted;
            Blocked = blocked ?? new Dictionary<GuardResult, int>();
            Active = active;
            DryRun = dryRun;
            Note = note;
        }

        public int Tracked { get; }
        public int Waiting { get; }
        public int Deferred { get; }
        public int Acted { get; }
        public IReadOnlyDictionary<GuardResult, int> Blocked { get; }
        public bool Active { get; }
        public bool DryRun { get; }

        /// <summary>A short reason shown with the counts (stepped aside, postponed by a tombstone, waits for a native probe).</summary>
        public string? Note { get; }
    }

    public sealed class EngineStatus
    {
        private readonly Dictionary<DelveKind, KindReport> kinds = new Dictionary<DelveKind, KindReport>();
        private readonly LinkedList<string> actions = new LinkedList<string>();
        private int capacity;

        public EngineStatus(int lines)
        {
            Resize(lines);
        }

        /// <summary>Newest first.</summary>
        public IEnumerable<string> LastActions => actions;

        public void Resize(int lines)
        {
            capacity = Math.Max(1, lines);
            while (actions.Count > capacity) actions.RemoveLast();
        }

        public void Record(DelveKind kind, string what, bool dryRun, Stamp when)
        {
            actions.AddFirst(Day(when) + " " + kind + ": " + (dryRun ? "dry run: would have " : "") + what);
            while (actions.Count > capacity) actions.RemoveLast();
        }

        public void Set(DelveKind kind, KindReport report) => kinds[kind] = report ?? throw new ArgumentNullException(nameof(report));

        public KindReport? Get(DelveKind kind) => kinds.TryGetValue(kind, out var r) ? r : null;

        /// <param name="reason">The admin-facing name of a block reason (the catalog's guard_* text); the enum name when null.</param>
        public IEnumerable<string> Lines(Stamp now, bool dryRunAll, Func<GuardResult, string>? reason = null)
        {
            yield return "Delve status, " + Day(now) + (dryRunAll ? " (DRY RUN for every kind)" : "");
            foreach (DelveKind kind in Enum.GetValues(typeof(DelveKind)))
            {
                if (!kinds.TryGetValue(kind, out var r)) continue;
                if (!r.Active)
                {
                    yield return kind + ": off" + (r.Note != null ? " (" + r.Note + ")" : "");
                    continue;
                }
                var blocked = string.Join(", ", r.Blocked.Where(b => b.Value > 0).Select(b => (reason != null ? reason(b.Key) : b.Key.ToString()) + " " + b.Value));
                yield return kind + ": " + r.Tracked + " tracked, " + r.Waiting + " waiting, " + r.Deferred + " deferred, " + r.Acted + " acted"
                    + (blocked.Length > 0 ? ", blocked: " + blocked : "") + (r.DryRun ? " (dry run)" : "") + (r.Note != null ? " (" + r.Note + ")" : "");
            }
            foreach (var line in actions) yield return "  " + line;
        }

        private static string Day(Stamp s) => s.HasDay ? "day " + s.Day.ToString("0.##", CultureInfo.InvariantCulture) : "day ?";
    }
}
