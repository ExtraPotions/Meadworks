// Keel - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: per-frame cost gates found by the 2026-10-09 static audit (docs/handoffs/test-reports/2026-10-09-fps-diagnosis.md).
using System;

namespace ExtraPotions.Keel.Performance
{
    /// <summary>
    /// One value computed once per frame and session stamp. Keel asks "is the product active" and "what does the authority
    /// decide for this scope" many times per frame (and per physics step); the answer cannot change inside one frame except
    /// through a session change, which moves the stamp.
    /// </summary>
    public sealed class FrameMemo<T>
    {
        private readonly Func<T> compute;
        private bool has;
        private long frame;
        private long stamp;
        private T value = default!;

        public FrameMemo(Func<T> compute)
        {
            this.compute = compute ?? throw new ArgumentNullException(nameof(compute));
        }

        /// <summary>The value for this frame and stamp; computed again when either differs from the last call. A computation that throws keeps nothing.</summary>
        public T Get(long currentFrame, long currentStamp)
        {
            if (has && frame == currentFrame && stamp == currentStamp) return value;
            var fresh = compute();
            value = fresh;
            frame = currentFrame;
            stamp = currentStamp;
            has = true;
            return fresh;
        }

        public void Invalidate() => has = false;
    }
}
