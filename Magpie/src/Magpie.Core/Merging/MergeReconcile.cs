// Magpie - ExtraPotions Meadworks loot handling.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;

namespace ExtraPotions.Magpie.Merging
{
    /// <summary>What the reconcile search found for the source named in an intent record.</summary>
    public enum SourceObservation
    {
        /// <summary>No loaded item carries the source token within the search radius.</summary>
        NotFound,

        /// <summary>The source exists and does not carry the intent's nonce: phase 2 never landed.</summary>
        FoundWithoutNonce,

        /// <summary>The source exists and carries the nonce in <c>magpie.merge_out</c>: phase 2 landed.</summary>
        FoundWithNonce,
    }

    public enum ReconcileAction
    {
        /// <summary>Too early or the area is not loaded: decide later.</summary>
        Wait,

        /// <summary>Phase 2 never landed: undo the added count, not below the pre-merge stack, then clear the record.</summary>
        RollBackTarget,

        /// <summary>The merge completed: clear the record.</summary>
        Clear,

        /// <summary>Phase 2 was saved as a removal and that source is still loaded: remove it once, leave the target stack.</summary>
        FinishDestroy,
    }

    /// <summary>
    /// Reconcile for an open <c>magpie.merge_in</c>. v2 rolls the target back when phase 2 was not saved.
    /// A saved removal is not rolled back; a source that is still loaded is removed once. v1 still clears when the
    /// source is missing. The rollback clears the record, so it applies at most once.
    /// </summary>
    public static class MergeReconcile
    {
        /// <summary>Records younger than this are left alone; equal to the barrier timeout.</summary>
        public const double ReconcileAfterSeconds = 10;

        /// <summary>Barrier timeout of step 4: phase 1 is undone when the barrier has not passed by then.</summary>
        public const double BarrierTimeoutSeconds = 10;

        /// <summary>Step 6: the target's owner clears a completed record after this much world time.</summary>
        public const double ClearAfterSeconds = 60;

        /// <summary>Extra search distance for the source around the target, added to the merge radius.</summary>
        public const float SearchMargin = 2f;

        public static ReconcileAction Decide(MergeIntent intent, double now, bool areaReady, SourceObservation source)
        {
            // The source is loaded and the removal was already saved: finish that removal without waiting out the rollback delay.
            if (intent.Phase2Saved && intent.DestroysSource == true && source == SourceObservation.FoundWithoutNonce)
                return ReconcileAction.FinishDestroy;
            if (!areaReady || now - intent.WorldTime < ReconcileAfterSeconds)
                return ReconcileAction.Wait;
            if (source == SourceObservation.FoundWithNonce || intent.Phase2Saved)
                return ReconcileAction.Clear;
            if (intent.TracksPhase2 || source == SourceObservation.FoundWithoutNonce)
                return ReconcileAction.RollBackTarget;
            return ReconcileAction.Clear;
        }

        /// <summary>
        /// Phase-2 miss. Subtract the moved count only while the saved stack is still the phase-1 size; otherwise stop at the pre-merge count.
        /// A missing pre-merge count keeps the old full subtraction.
        /// </summary>
        public static int StackAfterRollback(int savedStack, int moved, int? preMergeStack)
        {
            if (savedStack < 1)
                savedStack = 1;
            if (moved < 1)
                return savedStack;
            if (preMergeStack == null)
                return Math.Max(1, savedStack - moved);
            var preMerge = preMergeStack.Value;
            var inflated = preMerge + moved;
            if (savedStack == inflated)
                return Math.Max(1, savedStack - moved);
            var reduced = savedStack - moved;
            if (reduced >= preMerge)
                return reduced;
            return Math.Max(1, Math.Min(savedStack, preMerge));
        }

        public static float SearchRadius(MergeSettings settings) => settings.Radius + SearchMargin;
    }
}
