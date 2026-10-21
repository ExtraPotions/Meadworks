using System;
using System.Collections.Generic;
using System.Linq;

namespace ExtraPotions.Meadworks.Crucible {
    public sealed class NegotiatedCapability {
        public string PeerId{get;} public string ProductId{get;} public Version Protocol{get;}
        internal NegotiatedCapability(string peer,string product,Version version){PeerId=peer;ProductId=product;Protocol=version;}
    }
    public sealed class PeerCapabilities {
        string epoch="";long generation;bool ready;
        readonly Dictionary<(string,string),NegotiatedCapability> peers=new Dictionary<(string,string),NegotiatedCapability>();
        internal void UpdateSession(SessionSnapshot session){if(session.SessionId!=epoch||session.Generation!=generation)peers.Clear();epoch=session.SessionId;generation=session.Generation;ready=session.AuthorityReady;}
        public void RecordVerified(SessionSnapshot session,string peerId,string productId,Version protocol) {
            if(session==null)throw new ArgumentNullException(nameof(session));
            if(!ready||session.SessionId!=epoch||session.Generation!=generation||!session.AuthorityReady)throw new InvalidOperationException("Peer negotiation belongs to an unavailable or old session.");
            if(string.IsNullOrWhiteSpace(peerId)||peerId.Length>128)throw new ArgumentException("Invalid peer identity.");
            Validation.Id(productId);Validation.Version(protocol);
            var key=(peerId,productId);if(!peers.ContainsKey(key)&&peers.Count>=4096)throw new InvalidOperationException("Peer capability capacity reached.");
            peers[key]=new NegotiatedCapability(peerId,productId,protocol);
        }
        public bool Resolve(string peerId,string productId,Version protocol){Validation.Version(protocol);return ready&&peers.TryGetValue((peerId,productId),out var entry)&&entry.Protocol.Major==protocol.Major&&entry.Protocol>=protocol;}
        public IReadOnlyList<NegotiatedCapability> Snapshot()=>Array.AsReadOnly(peers.Values.OrderBy(p=>p.PeerId,StringComparer.Ordinal).ThenBy(p=>p.ProductId,StringComparer.Ordinal).ToArray());
        public void RemovePeer(string peerId){foreach(var key in peers.Keys.Where(k=>k.Item1==peerId).ToArray())peers.Remove(key);}
    }
}
