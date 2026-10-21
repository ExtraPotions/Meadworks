// Magpie - ExtraPotions Meadworks loot handling.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
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

        /// <summary>Phase 2 never landed: target stack minus the moved amount, then clear the record (no duplicate).</summary>
        RollBackTarget,

        /// <summary>The merge completed: clear the record.</summary>
        Clear,
    }

    /// <summary>
    /// Reconcile decision table for an open <c>magpie.merge_in</c> record (design 3.3). Because phase 2 is only sent after
    /// phase 1, "source removed while the target is unchanged" cannot occur, so the only repair is the rollback.
    /// The rollback clears the record, so it applies at most once.
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
            if (!areaReady || now - intent.WorldTime < ReconcileAfterSeconds)
                return ReconcileAction.Wait;
            return source == SourceObservation.FoundWithoutNonce ? ReconcileAction.RollBackTarget : ReconcileAction.Clear;
        }

        public static float SearchRadius(MergeSettings settings) => settings.Radius + SearchMargin;
    }
}
