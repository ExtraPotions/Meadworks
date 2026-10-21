namespace ExtraPotions.Wildroot {
    /// <summary>Only positive lifecycle evidence distinguishes an older native save
    /// from later normal removal. An absent object or ledger is never that evidence.</summary>
    public static class PlacementReceiptPolicy {
        public static bool CanUse(string payment,string session)=>payment!="pending"||string.IsNullOrEmpty(session);
        public static PlacementPresence Evaluate(bool areaReady,bool permitted,int matches,bool owned,bool receiptMatches,string receiptState,string receiptNativeId,string expectedNativeId,bool removedProof){
            if(!areaReady||!permitted)return PlacementPresence.Unavailable;
            if(matches>1)return PlacementPresence.Ambiguous;
            if(removedProof||receiptMatches&&receiptState=="removed")return PlacementPresence.Removed;
            if(matches==1)return owned?PlacementPresence.Present:PlacementPresence.Unavailable;
            if(receiptMatches&&receiptState=="intent"&&string.IsNullOrEmpty(receiptNativeId)&&!string.IsNullOrEmpty(expectedNativeId))return PlacementPresence.ProvenMissing;
            return PlacementPresence.Ambiguous;
        }
    }
}
