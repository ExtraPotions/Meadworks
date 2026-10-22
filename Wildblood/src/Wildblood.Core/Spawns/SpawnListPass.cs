// Wildblood - ExtraPotions Meadworks creature difficulty.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
namespace ExtraPotions.Wildblood
{
    /// <summary>
    /// Whether one SpawnSystem list pass may run, and whether Horde may grow its shared rows.
    /// The area itself is decided by the game-side blocker. This only combines that answer with the list kind.
    /// </summary>
    public static class SpawnListPass
    {
        /// <summary>Ordinary world lists in a blocked area do not run. Event lists always run.</summary>
        public static bool SkipOriginal(bool eventSpawns, bool areaBlocked) => !eventSpawns && areaBlocked;

        /// <summary>
        /// Horde grows shared rows only for a non-event list that will actually run, and only when its multiplier is above one.
        /// A skipped pass stays ungrown, so the finalizer has nothing to put back.
        /// </summary>
        public static bool HordeGrows(bool eventSpawns, bool hasSpawners, bool enabled, float multiplier, bool listSkipped)
            => !listSkipped && !eventSpawns && hasSpawners && enabled && multiplier > 1f;
    }
}
