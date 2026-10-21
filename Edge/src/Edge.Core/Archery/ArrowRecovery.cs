// Edge - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original. Recoverable arrows idea seen in BetterArchery (ishid4); implemented from its public feature list only.
using System;
using System.Collections.Generic;

namespace ExtraPotions.Edge.Archery
{
    /// <summary>What an arrow stopped on.</summary>
    public enum ArrowHit { Character, Water, Ground, Object }

    /// <summary>
    /// Classifies where a stopped arrow ended. An arrow is a water hit when the game reported water or when it stopped
    /// below the liquid surface at its hit point: the game does not report water for arrows that pass through the sea
    /// surface and stop on the seabed, so those count as lost too.
    /// </summary>
    public static class ArrowHits
    {
        /// <summary>The liquid level reported where there is no liquid (the game's own "none" height).</summary>
        public const float NoLiquid = -10000f;

        public static ArrowHit Classify(bool gameWater, bool character, bool ground, float hitY, float liquidLevel)
        {
            if (gameWater || hitY < liquidLevel) return ArrowHit.Water;
            if (character) return ArrowHit.Character;
            return ground ? ArrowHit.Ground : ArrowHit.Object;
        }
    }

    /// <summary>Outcome of one arrow's recovery roll.</summary>
    public enum RecoveryResult { NotOwned, HitCharacter, HitWater, Burned, Failed, Recovered }

    /// <summary>
    /// Ammo accounting for arrow recovery: the local player earns one credit per arrow the game took from the inventory
    /// (keyed by the arrow's prefab name); each arrow that stops spends one credit and gets at most one roll. Arrows that
    /// were never paid for (spawned by other features) find no credit, so recovery never returns more arrows than were fired.
    /// Main thread only.
    /// </summary>
    public sealed class ArrowLedger
    {
        /// <summary>Credits kept per arrow type; arrows lost without a hit cannot pile up beyond this.</summary>
        public const int MaxOutstanding = 64;

        private readonly Dictionary<string, int> outstanding = new Dictionary<string, int>(StringComparer.Ordinal);

        public void Fired(string ammo)
        {
            if (string.IsNullOrEmpty(ammo)) return;
            outstanding.TryGetValue(ammo, out var count);
            outstanding[ammo] = Math.Min(MaxOutstanding, count + 1);
        }

        public int Outstanding(string ammo) =>
            !string.IsNullOrEmpty(ammo) && outstanding.TryGetValue(ammo, out var count) ? count : 0;

        /// <summary>
        /// One stopped arrow. <paramref name="burnsUp"/>: the arrow carries fire damage. <paramref name="chancePercent"/> is
        /// clamped to 0..100; <paramref name="roll"/> is uniform in [0, 1) and recovers when below the chance.
        /// </summary>
        public RecoveryResult Resolve(string ammo, ArrowHit hit, bool burnsUp, int chancePercent, double roll)
        {
            var count = Outstanding(ammo);
            if (count <= 0) return RecoveryResult.NotOwned;
            if (count == 1) outstanding.Remove(ammo);
            else outstanding[ammo] = count - 1;
            switch (hit)
            {
                case ArrowHit.Character: return RecoveryResult.HitCharacter;
                case ArrowHit.Water: return RecoveryResult.HitWater;
            }
            if (burnsUp) return RecoveryResult.Burned;
            var chance = Math.Max(0, Math.Min(100, chancePercent)) / 100.0;
            return !double.IsNaN(roll) && roll >= 0 && roll < chance ? RecoveryResult.Recovered : RecoveryResult.Failed;
        }

        public void Clear() => outstanding.Clear();
    }
}
