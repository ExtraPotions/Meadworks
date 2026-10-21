using System;

namespace ExtraPotions.Meadworks.Crucible {
    // These facts come from the native transport, never decoded payload fields.
    // IsLocal permits a verified host/single-player participant; a remote owner
    // requires its currently authenticated server connection and fresh binding.
    public sealed class PeerCommandContext {
        public SessionSnapshot Session{get;}public string AuthorityPeerId{get;}public string LocalPeerId{get;}public string ConnectionId{get;}
        public bool IsLocal{get;}public bool ServerConnectionVerified{get;}public RequestIdentity? Identity{get;}public bool DecisionMade{get;private set;}
        public PeerCommandContext(SessionSnapshot session,string authorityPeerId,string localPeerId,bool isLocal,bool serverConnectionVerified,string connectionId="local")
            :this(session,authorityPeerId,localPeerId,isLocal,serverConnectionVerified,connectionId,null){}
        PeerCommandContext(SessionSnapshot session,string authorityPeerId,string localPeerId,bool isLocal,bool verified,string connectionId,RequestIdentity? identity){
            Session=session??throw new ArgumentNullException(nameof(session));Text(authorityPeerId);Text(localPeerId);Text(connectionId);
            AuthorityPeerId=authorityPeerId;LocalPeerId=localPeerId;ConnectionId=connectionId;IsLocal=isLocal;ServerConnectionVerified=verified;Identity=identity;
        }
        static void Text(string text){if(string.IsNullOrWhiteSpace(text)||text.Length>128||text.IndexOfAny(new[]{'\0','\r','\n'})>=0)throw new ArgumentException("Invalid native command identity.");}
        internal PeerCommandContext ForRequest(RequestIdentity identity)=>new PeerCommandContext(Session,AuthorityPeerId,LocalPeerId,IsLocal,ServerConnectionVerified,ConnectionId,identity);
        public void MarkDecisionMade(){if(Identity==null)throw new InvalidOperationException("Command has not been admitted.");DecisionMade=true;}
    }
}
