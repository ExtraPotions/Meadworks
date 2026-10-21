// Magpie - ExtraPotions Meadworks loot handling.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;

namespace ExtraPotions.Magpie.Merging
{
    /// <summary>Ordered steps of the two-phase merge protocol (design 3.3). The plugin executes them in list order.</summary>
    public enum MergeStep
    {
        /// <summary>Step 2: write a C12 instance token on the source and wait until it has been sent.</summary>
        EnsureSourceToken,

        /// <summary>Step 3, phase 1: grow the target, write <c>magpie.merge_in</c>, keep the later spawn time, save.</summary>
        WriteTarget,

        /// <summary>Step 4: wait until the target's new revision was sent to every peer; on timeout undo phase 1.</summary>
        AwaitBarrier,

        /// <summary>Step 5, phase 2 (whole source moved): destroy the source through its owner.</summary>
        DestroySource,

        /// <summary>Step 5, phase 2 (partial): shrink the source and write <c>magpie.merge_out</c> = nonce, save.</summary>
        ReduceSource,

        /// <summary>Step 6: after the clear delay, the target's owner removes <c>magpie.merge_in</c>.</summary>
        ClearIntent,
    }

    /// <summary>One planned merge of a source stack into a target stack. Phase 1 (target) always precedes phase 2 (source).</summary>
    public sealed class MergePlan
    {
        private readonly MergeStep[] steps;

        internal MergePlan(GroundItem target, GroundItem source, int moved, string nonce, double worldTime)
        {
            Target = target;
            Source = source;
            Moved = moved;
            Nonce = nonce;
            WorldTime = worldTime;
            var list = new List<MergeStep>();
            if (NeedsSourceToken)
                list.Add(MergeStep.EnsureSourceToken);
            list.Add(MergeStep.WriteTarget);
            list.Add(MergeStep.AwaitBarrier);
            list.Add(DestroysSource ? MergeStep.DestroySource : MergeStep.ReduceSource);
            list.Add(MergeStep.ClearIntent);
            steps = list.ToArray();
        }

        public GroundItem Target { get; }
        public GroundItem Source { get; }

        /// <summary>m = min(source stack, target maximum minus target stack), always above zero.</summary>
        public int Moved { get; }

        /// <summary>Unique nonce of this merge, written into the intent and, for a partial drain, onto the source.</summary>
        public string Nonce { get; }

        /// <summary>World time at planning, recorded in the intent.</summary>
        public double WorldTime { get; }

        public int TargetStackAfter => Target.Stack + Moved;
        public int SourceStackAfter => Source.Stack - Moved;
        public bool DestroysSource => SourceStackAfter == 0;

        /// <summary>The merged stack keeps the later spawn time of the pair, so merging never makes loot despawn sooner (B8).</summary>
        public double TargetSpawnTimeAfter => Math.Max(Target.SpawnTime, Source.SpawnTime);

        public bool NeedsSourceToken => string.IsNullOrEmpty(Source.Token);

        public IReadOnlyList<MergeStep> Steps => steps;

        /// <summary>The phase 1 intent record, once the source carries its token.</summary>
        public MergeIntent Intent(string sourceToken) => new MergeIntent(Nonce, sourceToken, Moved, WorldTime);
    }
}
