// Adapted from OpenKeep/OpenKeep/src/Homestead/TorchSwitch.cs and TorchNight.cs.
// Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later.
// Pin: 684671f98de54db6ea6652092ec041f44016c37d
// Changed by expDARE, 2026: pure transition decisions; native clock thresholds stay in the runtime.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;

namespace ExtraPotions.Ember.Torches
{
    public readonly struct TorchChange
    {
        public readonly bool Write;
        public readonly int Phase;
        public readonly bool? On;
        internal TorchChange(int phase, bool? on) { Write = true; Phase = phase; On = on; }
    }

    public static class TorchSchedule
    {
        public const int Day = 1, Night = 2;
        public const float MaxMargin = 4f;

        public static TorchChange Step(bool enabled, bool listed, bool kept, int applied, bool? night)
        {
            if (!enabled || !listed || kept)
                return applied == 0 ? default : new TorchChange(0, applied == Day ? (bool?)true : null);
            if (!night.HasValue) return default;
            int phase = night.Value ? Night : Day;
            return phase == applied ? default : new TorchChange(phase, night.Value);
        }

        public static bool? IsNight(float fraction, float lightsOut, float lightsOn) =>
            Finite(fraction) && Finite(lightsOut) && Finite(lightsOn)
                ? fraction <= lightsOut || fraction >= lightsOn : (bool?)null;

        public static bool Settled(float fraction, float target)
        {
            if (!Finite(fraction) || !Finite(target)) return false;
            double delta = ((fraction - target) * 360d + 180d) % 360d;
            if (delta < 0) delta += 360d;
            return Math.Abs(delta - 180d) < 10d;
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
