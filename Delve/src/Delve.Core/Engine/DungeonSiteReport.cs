// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;

namespace ExtraPotions.Delve.Engine
{
    /// <summary>A read-only dungeon visit clock, not a verdict that its regeneration guards permit an action.</summary>
    public sealed class DungeonSiteReport
    {
        public DungeonSiteReport(string id, DelveKind kind, string generator, Vec3 entrance, ClockKind clock,
            double delay, Stamp? lastVisit, Stamp now, bool requested, bool dryRun, bool evaluationPending = false)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Expected a site identity.", nameof(id));
            if (string.IsNullOrWhiteSpace(generator)) throw new ArgumentException("Expected a generator name.", nameof(generator));
            if (!Finite(entrance.X) || !Finite(entrance.Y) || !Finite(entrance.Z))
                throw new ArgumentException("Expected a finite entrance position.", nameof(entrance));
            Id = id; Kind = kind; Generator = generator; Entrance = entrance; Clock = clock;
            Requested = requested; DryRun = dryRun; EvaluationPending = evaluationPending;
            var timing = ClockRules.Inspect(clock, delay, lastVisit, now, requested);
            Remaining = timing.Remaining; Due = timing.Due;
        }

        public string Id { get; }
        public DelveKind Kind { get; }
        public string Generator { get; }
        public Vec3 Entrance { get; }
        public ClockKind Clock { get; }
        /// <summary>Null when the selected clock has no recorded visit component. Reading never starts that clock.</summary>
        public double? Remaining { get; }
        public bool Requested { get; }
        public bool EvaluationPending { get; }
        /// <summary>Only the configured delay or existing regenerate-now flag; guards and sweep timing still apply.</summary>
        public bool Due { get; }
        public bool DryRun { get; }

        private static bool Finite(float n) => !float.IsNaN(n) && !float.IsInfinity(n);
    }
}
