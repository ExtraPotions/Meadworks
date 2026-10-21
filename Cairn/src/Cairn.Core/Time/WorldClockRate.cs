// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;

namespace ExtraPotions.Cairn.WorldTime
{
    public static class WorldClockRate
    {
        public static float ScaleDelta(float delta, float rate)
        {
            if (!(delta > 0f) || float.IsInfinity(delta) || float.IsNaN(rate) || float.IsInfinity(rate)) return delta;
            rate = Math.Min(10f, Math.Max(0.1f, rate));
            double scaled = (double)delta * rate;
            return scaled <= float.MaxValue ? (float)scaled : delta;
        }
    }
}
