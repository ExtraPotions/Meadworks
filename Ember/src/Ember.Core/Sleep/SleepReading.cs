// Ember - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the reading behind the shared meadworks.ember.sleep 1.0 capability (Cairn interop, design E05).
using System;

namespace ExtraPotions.Ember.Sleep
{
    /// <summary>
    /// One reading of Ember's sleep state in the order of Crucible's <c>meadworks.ember.sleep</c> 1.0 contract: window
    /// open, players, sleeping, required, extra rate, full skip running, full skip progress. The peer state reply has no
    /// required count or skip progress, so both are computed here from Ember's own rules. Every value is clamped to the
    /// contract bounds, which equal the peer reply bounds.
    /// </summary>
    public readonly struct SleepReading
    {
        public const int MaximumPlayers = SleepCommands.MaxPlayers;
        public const double MaximumExtraRate = SleepCommands.MaxExtraRate;

        public static SleepReading Closed => default;

        SleepReading(bool windowOpen, int players, int sleeping, int required, double extraRate, bool skipRunning, float skipProgress)
        {
            WindowOpen = windowOpen;
            Players = players;
            Sleeping = sleeping;
            Required = required;
            ExtraRate = extraRate;
            SkipRunning = skipRunning;
            SkipProgress = skipProgress;
        }

        public bool WindowOpen { get; }
        public int Players { get; }
        public int Sleeping { get; }
        public int Required { get; }
        public double ExtraRate { get; }
        public bool SkipRunning { get; }
        public float SkipProgress { get; }

        /// <summary>
        /// Players needed in bed for the full skip. Ember skips only when everyone counted is in bed
        /// (<see cref="SleepCounts.AllInBed"/>), so this is the player count; zero with nobody counted.
        /// </summary>
        public static int RequiredFor(int players) => Math.Max(0, Math.Min(MaximumPlayers, players));

        /// <summary>
        /// Full skip progress 0..1: the share of net time from the skip start to the next morning. The game moves net time
        /// linearly to that morning over the skip, so this is linear in real time too. An empty span counts as done;
        /// non-finite input gives 0.
        /// </summary>
        public static float ProgressBetween(double startSeconds, double targetSeconds, double nowSeconds)
        {
            if (!Finite(startSeconds) || !Finite(targetSeconds) || !Finite(nowSeconds))
                return 0f;
            if (!(targetSeconds > startSeconds))
                return 1f;
            return (float)Math.Max(0.0, Math.Min(1.0, (nowSeconds - startSeconds) / (targetSeconds - startSeconds)));
        }

        /// <summary>
        /// The reading for a sleep state. The skip runs when the state says so or the caller saw it locally; progress is
        /// zero while no skip runs.
        /// </summary>
        public static SleepReading From(SleepState state, bool skipRunning, float skipProgress)
        {
            int players = Math.Max(0, Math.Min(MaximumPlayers, state.Players));
            int sleeping = Math.Max(0, Math.Min(players, state.Sleeping));
            double rate = Finite(state.ExtraRate) ? Math.Max(0.0, Math.Min(MaximumExtraRate, state.ExtraRate)) : 0.0;
            bool running = state.FullSkip || skipRunning;
            float progress = running && !float.IsNaN(skipProgress) ? Math.Max(0f, Math.Min(1f, skipProgress)) : 0f;
            return new SleepReading(state.WindowOpen, players, sleeping, RequiredFor(players), rate, running, progress);
        }

        static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

        public override string ToString() =>
            (WindowOpen ? "open " : "closed ") + Sleeping + "/" + Players + " need " + Required + " rate " + ExtraRate
            + (SkipRunning ? " skip " + SkipProgress : "");
    }

    /// <summary>
    /// Follows one full skip: latches its start time and target morning when the skip is first seen running, reports
    /// progress until it stops, then starts fresh for the next skip.
    /// </summary>
    public sealed class SkipProgressTracker
    {
        double start;
        double target;

        public bool Running { get; private set; }

        /// <summary>Feeds the current skip flag and net time; <paramref name="targetSeconds"/> is read on the running edge only.</summary>
        public float Update(bool skipRunning, double nowSeconds, double targetSeconds)
        {
            if (!skipRunning)
            {
                Running = false;
                return 0f;
            }
            if (!Running)
            {
                Running = true;
                start = nowSeconds;
                target = targetSeconds;
            }
            return SleepReading.ProgressBetween(start, target, nowSeconds);
        }

        public void Reset() => Running = false;
    }
}
