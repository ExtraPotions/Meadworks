// Ember - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (c) 2026 Pete Navarra (aka Vapok), MIT. Adapted from BetterSleepBruh 2.1.0 at
// fe2bb81866e58f3c4afeb832876cba6ebf1e12cd, BetterSleepBruh/Patches/EnvMan.cs (IsInSleepWindow, CalculateCanSleepPatch).
// The MIT notice is retained in THIRD_PARTY_NOTICES/BetterSleepBruh-MIT.txt.
// Changed by expDARE, 2026: game-free inputs (day fraction, skipping flag, times) instead of EnvMan and Player;
// Sleep Start clamped to its 0..0.99 range; Sleep Any Time cheat input; namespace Ember.
using System;

namespace ExtraPotions.Ember.Sleep
{
    /// <summary>S1, S2: the sleep window (day fractions) and the can-sleep rule with the wake-up cooldown.</summary>
    public static class SleepWindow
    {
        /// <summary>Morning as a day fraction: the window always ends here.</summary>
        public const float MorningFraction = 0.25f;
        public const float DefaultSleepStart = 0.5f;
        public const float MinSleepStart = 0f;
        public const float MaxSleepStart = 0.99f;

        public static float ClampSleepStart(float sleepStart)
        {
            if (float.IsNaN(sleepStart)) return DefaultSleepStart;
            return Math.Max(MinSleepStart, Math.Min(MaxSleepStart, sleepStart));
        }

        /// <summary>
        /// True inside the window: from Sleep Start until morning (0.25), wrapping past midnight; with Sleep Start below
        /// morning, from Sleep Start to morning only. Never while the game is skipping time. Sleep Any Time (cheat, already
        /// gate-evaluated by the caller) keeps the window open at every hour.
        /// </summary>
        public static bool IsOpen(float dayFraction, float sleepStart, bool timeSkipping, bool sleepAnyTime = false)
        {
            if (timeSkipping)
                return false;
            if (sleepAnyTime)
                return true;

            float start = ClampSleepStart(sleepStart);
            if (start < MorningFraction)
                return dayFraction < MorningFraction && dayFraction >= start;

            return dayFraction >= start || dayFraction < MorningFraction;
        }

        /// <summary>
        /// The local player's can-sleep result: inside the window and past the cooldown after the last wake-up
        /// (now must exceed wake-up time + cooldown). Without a local player (null wake-up time) the window decides.
        /// </summary>
        public static bool CanSleep(bool windowOpen, double nowSeconds, double? wakeupTime, double cooldownSeconds)
        {
            if (!windowOpen)
                return false;
            if (wakeupTime.HasValue && nowSeconds <= wakeupTime.Value + cooldownSeconds)
                return false;
            return true;
        }
    }
}
