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
        /// <summary>The grid keeps going while a cultivator is in the right hand, whichever item instance it is.</summary>
        public static bool IsCultivator(string? toolPrefab)=>toolPrefab=="Cultivator";
        /// <summary>A whole-grid payment (Prevent Partial Planting) whose cell could not be created was returned in full; unless
        /// the player stopped the grid, the remaining cells are planted one by one and only the unplaceable ones are skipped.</summary>
        public static bool RetryCellsIndividually(PlacementResultKind result,bool gridCancelled)=>!gridCancelled&&result==PlacementResultKind.CreateFailed;
        /// <summary>A shared-material cell can be canceled by a condition that lasts a few frames: the game reports an area as not
        /// ready while an object near it is being created or removed (a stone picked mid-grid, a neighbor's staged piece).
        /// Its payment was returned, so a cell canceled while the player is still planting, whose spot is placeable again,
        /// is tried once more under a new request id. A spot that stays unplaceable is skipped and costs nothing.</summary>
        public static bool RetryCanceledCell(PlacementResultKind result,bool gridCancelled,bool stillPlanting,bool placeable,bool retried)=>
            result==PlacementResultKind.Cancelled&&!gridCancelled&&stillPlanting&&placeable&&!retried;
        public static string RetryRequestId(string cell)=>cell+":retry";
        /// <summary>How long a retried cell waits for its area to become ready before it is tried (and skipped if still not).</summary>
        public const float SettleSeconds=5f;
        public static bool KeepSettling(float waited)=>waited>=0&&waited<SettleSeconds;
    }
}
