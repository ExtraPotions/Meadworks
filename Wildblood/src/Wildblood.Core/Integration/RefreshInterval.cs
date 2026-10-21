// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: a value refreshed at most once per interval, at once after a reset.
using System;

namespace ExtraPotions.Wildblood.Integration
{
    /// <summary>
    /// Says when a periodically shown value is due again. The first ask after construction or <see cref="Reset"/> is due
    /// at once (the line appears without delay); then at most once per interval. Main thread only.
    /// </summary>
    public sealed class RefreshInterval
    {
        readonly float seconds;
        float next;
        bool armed;

        public RefreshInterval(float seconds)
        {
            if (!(seconds > 0f) || float.IsInfinity(seconds)) throw new ArgumentOutOfRangeException(nameof(seconds));
            this.seconds = seconds;
        }

        public bool Due(float now)
        {
            if (armed && now < next) return false;
            armed = true; next = now + seconds;
            return true;
        }

        public void Reset() => armed = false;
    }
}
