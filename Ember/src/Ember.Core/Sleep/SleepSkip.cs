// Ember - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (c) 2026 Pete Navarra (aka Vapok), MIT. Adapted from BetterSleepBruh 2.1.0 at
// fe2bb81866e58f3c4afeb832876cba6ebf1e12cd, BetterSleepBruh/Patches/Game.cs (UpdateSleepingPatch).
// The MIT notice is retained in THIRD_PARTY_NOTICES/BetterSleepBruh-MIT.txt.
// Changed by expDARE, 2026: the decision as a pure function returning an action (the plugin routes SleepStart and
// SleepStop and calls SkipToMorning); module-inactive input covers Sleep.Enabled off, Use Vanilla Sleep Window and
// step-aside; namespace Ember.
namespace ExtraPotions.Ember.Sleep
{
    /// <summary>What the server's sleep update does this frame.</summary>
    public enum SkipAction
    {
        /// <summary>Do nothing (and do not run the game's own update).</summary>
        None,

        /// <summary>Ember's sleep module is not active: run the game's own update.</summary>
        Vanilla,

        /// <summary>Everybody is in bed inside the window: SleepStart to everybody, then skip to morning.</summary>
        StartSkip,

        /// <summary>The skip has ended: SleepStop to everybody.</summary>
        StopSkip,
    }

    /// <summary>S5: the all-in-bed full skip, driven from Ember's counts.</summary>
    public static class SleepSkip
    {
        public static SkipAction Decide(bool moduleActive, bool isServer, bool gameSleeping, bool timeSkipping, bool windowOpen, bool allInBed)
        {
            if (!moduleActive)
                return SkipAction.Vanilla;
            if (!isServer)
                return SkipAction.None;
            if (gameSleeping)
                return timeSkipping ? SkipAction.None : SkipAction.StopSkip;
            if (timeSkipping || !windowOpen)
                return SkipAction.None;
            return allInBed ? SkipAction.StartSkip : SkipAction.None;
        }
    }
}
