using System;
using System.Collections.Generic;
namespace ExtraPotions.Meadworks.Crucible {
    /// <summary>
    /// Answers the product-facing authority query without rebuilding it per call. <see cref="AuthorityFacts"/> and the
    /// <see cref="AuthorityDecision"/> for each scope are kept per product and reused while the inputs they were built
    /// from are unchanged. Nothing is invalidated by hooks: every call reads the live inputs (session role, authority
    /// ready, admin, server presence, the product's registered peer class, the server's state for the product) and
    /// compares them with the ones the entry was built from, so a changed input can never return an old answer, and
    /// a changed registry (unregister, re-register with another policy) is seen on the next call. Main thread only,
    /// like the registry and the session tracker it reads.
    /// </summary>
    public sealed class AuthorityMemo {
        // Unknown product ids fail closed and are cached too; a caller passing arbitrary ids cannot grow this without bound.
        const int MaxEntries=512;
        const int ScopeCount=3;
        readonly ProductRegistry registry;
        readonly Dictionary<string,Entry> entries=new Dictionary<string,Entry>(StringComparer.Ordinal);

        sealed class Entry {
            public SessionRole Role;public bool Ready;public bool Admin;public ServerCrucible Server;public PeerClass Peer;public ServerProduct ServerProduct;
            public AuthorityFacts Facts=null!;
            // [scope * 2 + (serverLocked ? 1 : 0)]
            public readonly AuthorityDecision?[] Decisions=new AuthorityDecision?[ScopeCount*2];
        }

        public AuthorityMemo(ProductRegistry registry){this.registry=registry??throw new ArgumentNullException(nameof(registry));}

        /// <summary>Whether this memo answers for the given registry (the plugin replaces its registry only on a restart).</summary>
        public bool IsFor(ProductRegistry other)=>ReferenceEquals(registry,other);

        /// <summary>Peer class from the registered metadata; unknown products fail closed as required-on-all-peers.</summary>
        public PeerClass PeerClassOf(string productId)=>registry.TryGetMetadata(productId??"",out var product)?PeerClasses.Of(product):PeerClass.RequiredOnAllPeers;

        public AuthorityFacts Facts(SessionSnapshot session,ServerCrucible server,string productId,ServerProduct serverProduct)=>Resolve(session,server,productId,serverProduct).Facts;

        public bool ProductActive(SessionSnapshot session,ServerCrucible server,string productId,ServerProduct serverProduct,out ConfigBlockReason reason)=>
            AuthorityGate.ProductActive(Resolve(session,server,productId,serverProduct).Facts,out reason);

        public AuthorityDecision Decide(SessionSnapshot session,ServerCrucible server,string productId,ServerProduct serverProduct,ConfigScope scope,bool serverLocked=true){
            var entry=Resolve(session,server,productId,serverProduct);
            int index=(int)scope;
            // An undefined scope takes the uncached path so it still fails with the gate's own exception.
            if((uint)index>=ScopeCount)return AuthorityGate.Decide(scope,entry.Facts,serverLocked);
            index=index*2+(serverLocked?1:0);
            return entry.Decisions[index]??(entry.Decisions[index]=AuthorityGate.Decide(scope,entry.Facts,serverLocked));
        }

        Entry Resolve(SessionSnapshot session,ServerCrucible server,string productId,ServerProduct serverProduct){
            if(session==null)throw new ArgumentNullException(nameof(session));
            var id=productId??"";
            var peer=PeerClassOf(id);
            if(entries.TryGetValue(id,out var entry)){
                if(entry.Role==session.Role&&entry.Ready==session.AuthorityReady&&entry.Admin==session.IsAdmin&&entry.Server==server&&entry.Peer==peer&&entry.ServerProduct==serverProduct)return entry;
            }else{
                if(entries.Count>=MaxEntries)entries.Clear();
                entry=new Entry();
            }
            // Built first so invalid input throws before the entry is touched.
            var facts=AuthorityFacts.From(session,server,peer,serverProduct);
            entry.Role=session.Role;entry.Ready=session.AuthorityReady;entry.Admin=session.IsAdmin;entry.Server=server;entry.Peer=peer;entry.ServerProduct=serverProduct;
            entry.Facts=facts;Array.Clear(entry.Decisions,0,entry.Decisions.Length);
            entries[id]=entry;
            return entry;
        }
    }
}
