using System;

namespace ExtraPotions.Meadworks.Crucible {
    public enum SessionRole { Menu, SinglePlayer, Host, RemoteClient, DedicatedServer }
    public sealed class SessionFacts {
        static readonly char[] LineBreaks={'\r','\n'};
        public bool WorldReady{get;} public bool NetworkReady{get;} public bool IsServer{get;} public bool IsDedicated{get;}
        public int RemotePeerCount{get;} public long WorldId{get;} public bool ConfigurationReady{get;} public bool IsAdmin{get;}
        public string AuthoritativeSessionId{get;} public bool Hosted{get;}
        public SessionFacts(bool worldReady,bool networkReady,bool isServer,bool isDedicated,int remotePeerCount,long worldId,bool configurationReady,bool isAdmin,string? authoritativeSessionId=null,bool hosted=false) {
            if(remotePeerCount<0)throw new ArgumentOutOfRangeException(nameof(remotePeerCount));
            if(isDedicated&&!isServer)throw new ArgumentException("A dedicated authority must be a server.");
            if(authoritativeSessionId!=null&&(authoritativeSessionId.Length>128||authoritativeSessionId.IndexOfAny(LineBreaks)>=0))throw new ArgumentException("Invalid session identity.");
            WorldReady=worldReady;NetworkReady=networkReady;IsServer=isServer;IsDedicated=isDedicated;RemotePeerCount=remotePeerCount;
            WorldId=worldId;ConfigurationReady=configurationReady;IsAdmin=isAdmin;AuthoritativeSessionId=authoritativeSessionId??"";Hosted=hosted;
        }
    }
    public sealed class SessionSnapshot {
        public SessionRole Role{get;} public long WorldId{get;} public string SessionId{get;} public long Generation{get;}
        public bool AuthorityReady{get;} public bool IsAdmin{get;} public bool IsAuthority=>Role==SessionRole.SinglePlayer||Role==SessionRole.Host||Role==SessionRole.DedicatedServer;
        internal SessionSnapshot(SessionRole role,long world,string session,long generation,bool ready,bool admin){Role=role;WorldId=world;SessionId=session;Generation=generation;AuthorityReady=ready;IsAdmin=admin;}
    }
}
