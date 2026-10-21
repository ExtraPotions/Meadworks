// Ember - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (c) 2026 Pete Navarra (aka Vapok), MIT. Adapted from BetterSleepBruh 2.1.0 at
// fe2bb81866e58f3c4afeb832876cba6ebf1e12cd, BetterSleepBruh/Patches/ZNet.cs (UpdateNetTime postfix: boost step,
// fade, morning cap, 0.25 s time sync). The MIT notice is retained in THIRD_PARTY_NOTICES/BetterSleepBruh-MIT.txt.
// Changed by expDARE, 2026: pure step function returning the new time (the plugin applies it); at or past the morning
// cap the time is left unchanged instead of being set back to the cap; fade clamped to 0..30 real seconds; sync timer
// as a small class; namespace Ember.
using System;

namespace ExtraPotions.Ember.Sleep
{
    /// <summary>Result of one boost step on the server.</summary>
    public readonly struct BoostStep
    {
        public double NewTime { get; }
        public double Applied { get; }

        /// <summary>Extra time was added: the server should sync its time to peers (subject to <see cref="TimeSyncTimer"/>).</summary>
        public bool Advanced => Applied > 0.0;

        public BoostStep(double newTime, double applied)
        {
            NewTime = newTime;
            Applied = applied;
        }
    }

    /// <summary>S6: the partial boost step with the morning cap and the linear fade before morning.</summary>
    public static class SleepBoost
    {
        public const double MaxFadeRealSeconds = 30.0;

        /// <summary>
        /// The extra rate after the fade: over the last Boost Fade real seconds before morning, scaled to game time by
        /// (1 + extra rate), the extra rate ramps linearly to 0. A fade of 0 means a hard stop at morning.
        /// </summary>
        public static double EffectiveExtraRate(double extraRate, double remainingToMorning, double fadeRealSeconds)
        {
            if (extraRate <= 0.0)
                return 0.0;
            double fade = double.IsNaN(fadeRealSeconds) ? 0.0 : Math.Max(0.0, Math.Min(MaxFadeRealSeconds, fadeRealSeconds));
            if (fade <= 0.0)
                return extraRate;
            double fadeWindowGame = fade * (1.0 + extraRate);
            if (remainingToMorning < fadeWindowGame)
                return extraRate * (Math.Max(0.0, remainingToMorning) / fadeWindowGame);
            return extraRate;
        }

        /// <summary>
        /// One network time step: adds dt x effective extra rate to the time, never past the next morning start.
        /// An infinite cap (morning unknown) adds the plain rate.
        /// </summary>
        public static BoostStep Step(double time, double dt, double extraRate, double morningCap, double fadeRealSeconds)
        {
            if (extraRate <= 0.0 || double.IsNaN(extraRate) || dt <= 0.0 || double.IsNaN(dt))
                return new BoostStep(time, 0.0);

            if (double.IsPositiveInfinity(morningCap))
                return new BoostStep(time + dt * extraRate, dt * extraRate);

            if (time >= morningCap)
                return new BoostStep(time, 0.0);

            double remainingToMorning = morningCap - time;
            double effective = EffectiveExtraRate(extraRate, remainingToMorning, fadeRealSeconds);
            double applied = Math.Min(dt * effective, remainingToMorning);
            if (applied <= 0.0)
                return new BoostStep(time, 0.0);
            return new BoostStep(applied >= remainingToMorning ? morningCap : time + applied, applied);
        }
    }

    /// <summary>S7: while boosting, the server sends its network time to ready peers every 0.25 s.</summary>
    public sealed class TimeSyncTimer
    {
        public const double IntervalSeconds = 0.25;
        double elapsed;

        /// <summary>Adds dt; true (and restarts) once 0.25 s have accumulated.</summary>
        public bool Tick(double dt)
        {
            if (dt > 0.0 && !double.IsNaN(dt))
                elapsed += dt;
            if (elapsed + 1e-9 < IntervalSeconds)
                return false;
            elapsed = 0.0;
            return true;
        }

        /// <summary>Called when the boost stops or reaches the cap.</summary>
        public void Reset() => elapsed = 0.0;
    }
}
