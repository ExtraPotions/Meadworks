using System;

namespace ExtraPotions.Meadworks.Contracts {
    /// <summary>
    /// Product declaration <c>meadworks.admission.vanillasupport</c> version 1.0 for the optional server-only mode: each
    /// product that registers <c>MultiplayerPolicy.RequiredOnAllPeers</c> states whether its features work for clients
    /// without Meadworks. The shape is the BCL delegate <c>Func&lt;int&gt;</c> returning <see cref="ClientRequired"/>,
    /// <see cref="Mixed"/> or <see cref="ServerOnlyCapable"/>. Each product registers it on its own registration with
    /// <c>AddCapability&lt;Func&lt;int&gt;&gt;(Id, ContractVersion, ...)</c>; Crucible reads one declaration per owner.
    /// An absent, throwing or out-of-range declaration counts as <see cref="ClientRequired"/>. Optional, client-only and
    /// server-only policies need no declaration. Main thread only. Any change of meaning is a new major version.
    /// </summary>
    public static class VanillaSupportContract {
        public const string Id="meadworks.admission.vanillasupport";
        public static readonly Version ContractVersion=new Version(1,0);
        /// <summary>The product needs the mod on every client (custom prefabs, client-run mechanics or UI).</summary>
        public const int ClientRequired=0;
        /// <summary>Server-side features work for vanilla clients; installed clients also get UI and client extras.</summary>
        public const int Mixed=1;
        /// <summary>Every feature runs on the server and reaches vanilla clients.</summary>
        public const int ServerOnlyCapable=2;
        public static Type DelegateType=>typeof(Func<int>);

        public static bool Valid(int support)=>support>=ClientRequired&&support<=ServerOnlyCapable;

        /// <summary>Reads a declaration; absent, throwing or invalid gives false and <see cref="ClientRequired"/>.</summary>
        public static bool TryRead(Func<int>? provider,out int support){
            support=ClientRequired;
            if(provider==null)return false;
            int raw;
            try{raw=provider();}catch{return false;}
            if(!Valid(raw))return false;
            support=raw;
            return true;
        }
    }

    /// <summary>
    /// Server read capability <c>meadworks.peers.vanillacount</c> version 1.0, provided by Crucible: how many connected
    /// peers are not confirmed as running Crucible. That is vanilla peers (no answer within the 30 s join timeout) plus
    /// peers still pending (greeted, no answer yet) or unknown (not yet greeted), so a joining vanilla client counts at
    /// once. A Meadworks client also counts for the one round trip until its answer or first Crucible frame arrives;
    /// consumers that gate on "any" see a brief true while it joins. The shape is <c>Func&lt;int&gt;</c>;
    /// <see cref="Unknown"/> (-1) when not on the server or while the peer-class handshake is off, because Crucible
    /// cannot tell vanilla clients apart then. Consumers (Hex Ghost) read through <see cref="TryReadAny"/> and treat
    /// "no answer" conservatively. Main thread only.
    /// </summary>
    public static class VanillaPeersContract {
        public const string Id="meadworks.peers.vanillacount";
        public static readonly Version ContractVersion=new Version(1,0);
        public const int Unknown=-1;
        public static Type DelegateType=>typeof(Func<int>);

        /// <summary>True with the count when the provider knows it; absent, throwing, unknown or negative gives false.</summary>
        public static bool TryRead(Func<int>? provider,out int count){
            count=0;
            if(provider==null)return false;
            int raw;
            try{raw=provider();}catch{return false;}
            if(raw<0)return false;
            count=raw;
            return true;
        }

        /// <summary>True with whether any vanilla client is connected, when the provider knows.</summary>
        public static bool TryReadAny(Func<int>? provider,out bool any){
            var known=TryRead(provider,out var count);
            any=count>0;
            return known;
        }
    }

    /// <summary>
    /// Server read capability <c>meadworks.peers.kind</c> version 1.0, provided by Crucible: what Crucible knows about one
    /// connected peer by its ZNet uid. The shape is <c>Func&lt;long,int&gt;</c> returning <see cref="Unknown"/>,
    /// <see cref="Pending"/> (greeted, answer not yet due), <see cref="Crucible"/> or <see cref="Vanilla"/>. Unknown for
    /// unconnected peers, on clients and while the peer-class handshake is off. Main thread only.
    /// </summary>
    public static class PeerKindContract {
        public const string Id="meadworks.peers.kind";
        public static readonly Version ContractVersion=new Version(1,0);
        public const int Unknown=0,Pending=1,Crucible=2,Vanilla=3;
        public static Type DelegateType=>typeof(Func<long,int>);

        /// <summary>The peer's kind; an absent, throwing or out-of-range provider gives <see cref="Unknown"/>.</summary>
        public static int Read(Func<long,int>? provider,long peerId){
            if(provider==null)return Unknown;
            int raw;
            try{raw=provider(peerId);}catch{return Unknown;}
            return raw>=Unknown&&raw<=Vanilla?raw:Unknown;
        }
    }
}
