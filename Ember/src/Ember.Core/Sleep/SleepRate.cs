// Ember - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (c) 2026 Pete Navarra (aka Vapok), MIT. Adapted from BetterSleepBruh 2.1.0 at
// fe2bb81866e58f3c4afeb832876cba6ebf1e12cd, BetterSleepBruh/Components/SleepTracker.cs (GetSleepOccupancyCounts,
// ComputeExtraRateForPartialBoost) and BetterSleepBruh/Configuration/ConfigRegistry.cs (setting ranges,
// GetEffectiveTotalPlayersForMod, GetEffectiveSleepingPlayersForMod). The MIT notice is retained in
// THIRD_PARTY_NOTICES/BetterSleepBruh-MIT.txt.
// Changed by expDARE, 2026: counts and rate as pure functions of world facts (no ZNet, no config statics); Partial
// Sleep Boost switch; window input; fake player counts clamped to 0..80; settings clamped to their ranges; namespace Ember.
using System;

namespace ExtraPotions.Ember.Sleep
{
    /// <summary>Players and sleepers as the server counts them (S8), after testing offsets (S12).</summary>
    public readonly struct SleepCounts : IEquatable<SleepCounts>
    {
        public int Players { get; }
        public int Sleeping { get; }

        public SleepCounts(int players, int sleeping)
        {
            Players = Math.Max(0, players);
            Sleeping = Math.Max(0, Math.Min(Players, sleeping));
        }

        /// <summary>Everybody is in bed (and there is somebody): the full skip applies (S5).</summary>
        public bool AllInBed => Players > 0 && Sleeping >= Players;

        public bool Equals(SleepCounts other) => Players == other.Players && Sleeping == other.Sleeping;
        public override bool Equals(object? obj) => obj is SleepCounts other && Equals(other);
        public override int GetHashCode() => (Players * 397) ^ Sleeping;
        public override string ToString() => Sleeping + "/" + Players;
    }

    /// <summary>Testing Mode cheat values (S12); the caller passes Enabled = false whenever the gate blocks the cheat.</summary>
    public readonly struct TestingOffsets
    {
        public const int MaxFake = 80;
        public const int DefaultFakeTotalPlayers = 10;
        public const int DefaultSimulatePlayersInBed = 1;

        public static TestingOffsets Off => new TestingOffsets(false, 0, 0);

        public bool Enabled { get; }
        public int FakeTotalPlayers { get; }
        public int SimulatePlayersInBed { get; }

        public TestingOffsets(bool enabled, int fakeTotalPlayers, int simulatePlayersInBed)
        {
            Enabled = enabled;
            FakeTotalPlayers = Clamp(fakeTotalPlayers, 0, MaxFake);
            SimulatePlayersInBed = Clamp(simulatePlayersInBed, 0, FakeTotalPlayers);
        }

        internal static int Clamp(int value, int min, int max) => value < min ? min : value > max ? max : value;
    }

    /// <summary>Server sleep boost settings (design 4.1), clamped to their ranges.</summary>
    public readonly struct BoostSettings
    {
        public const float DefaultBonusMultiplier = 0.6f;
        public const float DefaultBonusIncrementScale = 20f;
        public const float MaxBonusMultiplier = 1f;
        public const float MaxBonusIncrementScale = 30f;

        public static BoostSettings Default => new BoostSettings(true, DefaultBonusMultiplier, DefaultBonusIncrementScale);

        public bool PartialBoost { get; }
        public float BonusMultiplier { get; }
        public float BonusIncrementScale { get; }

        public BoostSettings(bool partialBoost, float bonusMultiplier, float bonusIncrementScale)
        {
            PartialBoost = partialBoost;
            BonusMultiplier = Range(bonusMultiplier, MaxBonusMultiplier, DefaultBonusMultiplier);
            BonusIncrementScale = Range(bonusIncrementScale, MaxBonusIncrementScale, DefaultBonusIncrementScale);
        }

        static float Range(float value, float max, float fallback) => float.IsNaN(value) ? fallback : Math.Max(0f, Math.Min(max, value));
    }

    /// <summary>S4, S8, S12: occupancy counts and the BetterSleepBruh partial boost rate.</summary>
    public static class SleepRate
    {
        /// <summary>
        /// Players = the larger of the character ZDO count and the connected player count, plus fake players; sleeping =
        /// character ZDOs flagged in bed plus fake sleepers (clamped to the fake players), clamped to the players.
        /// </summary>
        public static SleepCounts Count(int characterZdos, int sessionPlayers, int realSleeping, TestingOffsets testing)
        {
            int players = Math.Max(Math.Max(0, characterZdos), Math.Max(0, sessionPlayers));
            int sleeping = Math.Max(0, realSleeping);
            if (testing.Enabled)
            {
                players += testing.FakeTotalPlayers;
                sleeping += testing.SimulatePlayersInBed;
            }
            return new SleepCounts(players, sleeping);
        }

        /// <summary>
        /// Extra game seconds per real second while some but not all players are in bed:
        /// Bonus Multiplier x (sleeping / (players - 1)) x Bonus Increment Scale. Zero with one player, with nobody in
        /// bed, with everybody in bed (the full skip handles that), outside the window, or with Partial Sleep Boost off.
        /// </summary>
        public static double ExtraRate(SleepCounts counts, BoostSettings settings, bool windowOpen)
        {
            if (!windowOpen || !settings.PartialBoost)
                return 0.0;
            int players = counts.Players, sleeping = counts.Sleeping;
            if (players <= 1)
                return 0.0;
            if (sleeping <= 0 || sleeping >= players)
                return 0.0;
            double sleepFraction = sleeping / (double)(players - 1);
            return settings.BonusMultiplier * sleepFraction * settings.BonusIncrementScale;
        }

        /// <summary>The rate with all but one player in bed: the top of the boost range (0 when the boost is off).</summary>
        public static double MaxExtraRate(BoostSettings settings) =>
            settings.PartialBoost ? (double)settings.BonusMultiplier * settings.BonusIncrementScale : 0.0;
    }
}
