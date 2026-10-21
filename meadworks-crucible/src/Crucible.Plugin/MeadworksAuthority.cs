using System;
using System.Collections.Generic;
namespace ExtraPotions.Meadworks.Crucible {
    /// <summary>
    /// The product-facing authority query (<see cref="AuthorityGate"/>) over the live session, the vanilla-server timer
    /// and the per-product server presence reported by the peer-class handshake. Main thread only.
    /// </summary>
    public static class MeadworksAuthority {
        static readonly Dictionary<string,ServerProduct> serverProducts=new Dictionary<string,ServerProduct>(StringComparer.Ordinal);
        static AuthorityMemo? memo;
        // Facts and decisions are reused until a live input changes (see AuthorityMemo); a new registry gets a new memo.
        static AuthorityMemo Memo{get{var services=Plugin.Services;var current=memo;if(current==null||!current.IsFor(services))memo=current=new AuthorityMemo(services);return current;}}

        /// <summary>Whether the connected server runs Crucible; Unknown in the menu and while waiting for the Hello.</summary>
        public static ServerCrucible Server=>Plugin.IsReady?ServerPresenceTimer.For(Plugin.Session.Role,Plugin.ServerPresence.State,Plugin.Session.AuthorityReady):ServerCrucible.Unknown;

        /// <summary>Peer class from the registered metadata; unknown products fail closed as required-on-all-peers.</summary>
        public static PeerClass PeerClassOf(string productId){
            if(!Plugin.IsReady)return PeerClass.RequiredOnAllPeers;
            return Memo.PeerClassOf(productId);
        }

        public static ServerProduct ServerProductOf(string productId)=>serverProducts.TryGetValue(productId??"",out var state)?state:ServerProduct.Unknown;

        public static AuthorityFacts Facts(string productId){
            if(!Plugin.IsReady)return new AuthorityFacts(SessionRole.RemoteClient,false,false,ServerCrucible.Unknown,PeerClass.RequiredOnAllPeers);
            return Memo.Facts(Plugin.Session,Server,productId,ServerProductOf(productId));
        }

        public static AuthorityDecision Decide(string productId,ConfigScope scope,bool serverLocked=true)=>Plugin.IsReady?Memo.Decide(Plugin.Session,Server,productId,ServerProductOf(productId),scope,serverLocked):AuthorityGate.Decide(scope,Facts(productId),serverLocked);

        public static bool ProductActive(string productId,out ConfigBlockReason reason)=>Plugin.IsReady?Memo.ProductActive(Plugin.Session,Server,productId,ServerProductOf(productId),out reason):AuthorityGate.ProductActive(Facts(productId),out reason);

        /// <summary>Set by the peer-class handshake for the current connection; cleared when it ends.</summary>
        internal static void SetServerProducts(IEnumerable<KeyValuePair<string,ServerProduct>> states){
            serverProducts.Clear();foreach(var state in states)serverProducts[state.Key]=state.Value;
        }
        internal static void ClearServerProducts()=>serverProducts.Clear();
    }
}
