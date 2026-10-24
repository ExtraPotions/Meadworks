// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using ExtraPotions.Meadworks.Crucible;
using UnityEngine;

namespace ExtraPotions.Almanac.Runtime
{
    internal struct StallTiming
    {
        readonly MeadworksLog? log;
        readonly long start;
        readonly int frame, thread, gc0, gc1, gc2;
        long end;
        int endFrame, deltaGc0, deltaGc1, deltaGc2;
        internal bool Enabled => log != null;
        internal StallTiming(MeadworksLog? logger)
        {
            end = 0; endFrame = deltaGc0 = deltaGc1 = deltaGc2 = 0;
            log = logger != null && logger.IsEnabled(LogSeverity.Debug) ? logger : null;
            start = Enabled ? Stopwatch.GetTimestamp() : 0;
            frame = Enabled ? Time.frameCount : 0;
            thread = Enabled ? Thread.CurrentThread.ManagedThreadId : 0;
            gc0 = Enabled ? GC.CollectionCount(0) : 0;
            gc1 = Enabled ? GC.CollectionCount(1) : 0;
            gc2 = Enabled ? GC.CollectionCount(2) : 0;
        }
        internal static string Ms(long ticks) => (ticks * 1000d / Stopwatch.Frequency).ToString("0.000", CultureInfo.InvariantCulture);
        internal StallTiming Finish()
        {
            if (Enabled)
            {
                end = Stopwatch.GetTimestamp(); endFrame = Time.frameCount;
                deltaGc0 = GC.CollectionCount(0) - gc0; deltaGc1 = GC.CollectionCount(1) - gc1; deltaGc2 = GC.CollectionCount(2) - gc2;
            }
            return this;
        }
        internal void Complete(string name, string details)
        {
            if (!Enabled) return;
            try
            {
                log!.Debug("StallTiming " + name + " frame=" + frame + " endFrame=" + endFrame
                + " thread=" + thread + " startTicks=" + start + " endTicks=" + end
                + " totalMs=" + Ms(end - start) + " gc=" + deltaGc0
                + "/" + deltaGc1 + "/" + deltaGc2 + " " + details);
            }
            catch { } // Diagnostic output must not change discovery, UI or accepted writes.
        }
    }
}
