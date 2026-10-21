// Keel - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: per-frame cost gates found by the 2026-10-09 static audit (docs/handoffs/test-reports/2026-10-09-fps-diagnosis.md).
using System;
using System.Collections.Generic;

namespace ExtraPotions.Keel.Performance
{
    /// <summary>Lets work run at most once per interval of a clock the caller passes in (Unity-free, so it can be tested).</summary>
    public sealed class RefreshGate
    {
        private readonly double interval;
        private bool started;
        private double next;

        public RefreshGate(double intervalSeconds)
        {
            interval = double.IsNaN(intervalSeconds) ? 0.0 : Math.Max(0.0, intervalSeconds);
        }

        /// <summary>True on the first call, then once the interval has passed; also true when the clock went backwards (a new session or scene).</summary>
        public bool Due(double now)
        {
            if (started && now >= next - interval && now < next) return false;
            started = true;
            next = now + interval;
            return true;
        }

        /// <summary>The next call is due at once (the work it guards was undone or recreated).</summary>
        public void Reset() => started = false;
    }

    /// <summary>Remembers the last value written and says whether a new one differs, so unchanged UI text and renderer state is not written again.</summary>
    public sealed class ChangeGate<T>
    {
        private bool has;
        private T last = default!;

        public bool Changed(T value)
        {
            if (has && EqualityComparer<T>.Default.Equals(last, value)) return false;
            last = value;
            has = true;
            return true;
        }

        /// <summary>The next value passes, whatever it is (the thing it was written to was recreated or reset).</summary>
        public void Reset() => has = false;
    }
}
