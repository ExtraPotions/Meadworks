// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using ExtraPotions.Delve.Engine;

namespace ExtraPotions.Delve.Runtime
{
    // Created only for a Debug-enabled check. Keep formatting after the measured interval.
    internal sealed class DelveCheckTiming
    {
        private readonly long started = Stopwatch.GetTimestamp();
        private readonly int gc0 = GC.CollectionCount(0), gc1 = GC.CollectionCount(1), gc2 = GC.CollectionCount(2);
        private readonly List<(string Name, DelveKind? Kind, long Ticks, int Scanned, int Candidates, int Chars)> stages =
            new List<(string, DelveKind?, long, int, int, int)>();
        private readonly List<(DelveKind Kind, int Tracked, int Waiting, int Deferred, int Acted)> reports =
            new List<(DelveKind, int, int, int, int)>();

        internal static DelveCheckTiming? Begin(bool enabled) => enabled ? new DelveCheckTiming() : null;
        internal static long Start(DelveCheckTiming? timing) => timing == null ? 0L : Stopwatch.GetTimestamp();

        internal void Stage(string name, long start, DelveKind? kind = null, int scanned = 0, int candidates = 0, int chars = 0)
        {
            long ticks = Stopwatch.GetTimestamp() - start;
            stages.Add((name, kind, ticks, scanned, candidates, chars));
        }

        internal void Report(DelveKind kind, KindReport report) =>
            reports.Add((kind, report.Tracked, report.Waiting, report.Deferred, report.Acted));

        internal string Finish(int frame, int thread, bool dawn, double day, int worldZdos,
            IEnumerable<DelveKind> sweeping, int budgetLeft, bool completed, bool sidecarDirty, int serviceFailures)
        {
            long ticks = Stopwatch.GetTimestamp() - started;
            int collections0 = GC.CollectionCount(0) - gc0, collections1 = GC.CollectionCount(1) - gc1, collections2 = GC.CollectionCount(2) - gc2;
            var line = new StringBuilder("stall-timing event=delve-check");
            line.Append(" frame=").Append(frame).Append(" thread=").Append(thread).Append(" dawn=").Append(dawn)
                .Append(" day=").Append(day.ToString("F6", CultureInfo.InvariantCulture)).Append(" world_zdos=").Append(worldZdos)
                .Append(" ticks=").Append(ticks).Append(" frequency=").Append(Stopwatch.Frequency)
                .Append(" ms=").Append((ticks * 1000.0 / Stopwatch.Frequency).ToString("F3", CultureInfo.InvariantCulture))
                .Append(" gc=").Append(collections0).Append('/').Append(collections1).Append('/').Append(collections2)
                .Append(" completed=").Append(completed).Append(" service_failures=").Append(serviceFailures).Append(" budget_left=").Append(budgetLeft)
                .Append(" sidecar_dirty=").Append(sidecarDirty).Append(" sweeping=[");
            bool first = true;
            foreach (var kind in sweeping)
            {
                if (!first) line.Append(',');
                line.Append(kind); first = false;
            }
            line.Append("] stages=[");
            for (int i = 0; i < stages.Count; i++)
            {
                if (i > 0) line.Append(';');
                var stage = stages[i];
                line.Append(stage.Name);
                if (stage.Kind.HasValue) line.Append('.').Append(stage.Kind.Value);
                line.Append(":ticks=").Append(stage.Ticks).Append(",scanned=").Append(stage.Scanned).Append(",candidates=").Append(stage.Candidates).Append(",chars=").Append(stage.Chars);
            }
            line.Append("] reports=[");
            for (int i = 0; i < reports.Count; i++)
            {
                if (i > 0) line.Append(';');
                var report = reports[i];
                line.Append(report.Kind).Append(":tracked=").Append(report.Tracked).Append(",waiting=").Append(report.Waiting)
                    .Append(",deferred=").Append(report.Deferred).Append(",acted=").Append(report.Acted);
            }
            return line.Append(']').ToString();
        }
    }
}
