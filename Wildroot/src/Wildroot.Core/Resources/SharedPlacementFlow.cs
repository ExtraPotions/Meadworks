namespace ExtraPotions.Wildroot {
    /// <summary>
    /// How shared-material planting meets the player's cultivator. Cells wait on the material provider a few at a time,
    /// so a large grid never overruns the provider's admission queue. A cell that needs recovery stops holding the
    /// player's grid and settles in the background, so the cultivator is never held by unfinished bookkeeping.
    /// </summary>
    public static class SharedPlacementFlow {
        /// <summary>Burrow admits 24 queued reservations; staying well under it leaves room for status and settlement calls.</summary>
        public const int MaxInFlight=8;
        public static bool CanStart(int inFlight)=>inFlight<MaxInFlight;
        /// <summary>True while the cell still counts toward the player's grid in progress.</summary>
        public static bool HoldsBatch(AsyncPlacementJob job)=>job.Result==null&&!job.NeedsRecovery;
        /// <summary>A grid blocks new cultivator placements only while it is still running for the player.</summary>
        public static bool BlocksPlanting(int pending,bool cancelled)=>pending>0&&!cancelled;
    }
}
