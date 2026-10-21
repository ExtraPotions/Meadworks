using System;
namespace ExtraPotions.Wildroot {
    /// <summary>World identity decisions shared by every native adapter. The native
    /// identity read dereferences the loaded world, so it runs only when the network
    /// instance is present and a world is loaded; menu, connect and disconnect resolve
    /// to 0. Identity 0 means "no world" and never matches anything, itself included.</summary>
    public static class WorldIdentity {
        public static long Resolve(bool networkReady,bool worldLoaded,Func<long> readUid){
            if(readUid==null)throw new ArgumentNullException(nameof(readUid));
            return networkReady&&worldLoaded?readUid():0;
        }
        public static bool IsCurrent(long current,long captured)=>current!=0&&current==captured;
    }
}
