using System;

namespace ExtraPotions.Meadworks.Crucible {
    // Driven only by the native adapter on the main thread. Peer input alone is
    // never a SessionFacts source; remote epochs must come from the current server.
    public sealed class SessionTracker {
        public SessionSnapshot Current{get;private set;}=new SessionSnapshot(SessionRole.Menu,0,"",0,false,false);
        public PeerCapabilities Peers{get;}=new PeerCapabilities();
        public SessionSnapshot Update(SessionFacts facts) {
            if(facts==null)throw new ArgumentNullException(nameof(facts));
            var role=!facts.WorldReady||!facts.NetworkReady||facts.WorldId==0?SessionRole.Menu:
                !facts.IsServer?SessionRole.RemoteClient:facts.IsDedicated?SessionRole.DedicatedServer:
                facts.Hosted||facts.RemotePeerCount>0?SessionRole.Host:SessionRole.SinglePlayer;
            bool authority=role==SessionRole.SinglePlayer||role==SessionRole.Host||role==SessionRole.DedicatedServer;
            bool changed=Current.WorldId!=(role==SessionRole.Menu?0:facts.WorldId)||Current.Role==SessionRole.Menu!=(role==SessionRole.Menu)||Current.IsAuthority!=authority;
            var epoch=role==SessionRole.Menu?"":authority?(changed?Guid.NewGuid().ToString("N"):Current.SessionId):facts.AuthoritativeSessionId;
            changed|=epoch!=Current.SessionId;
            var generation=changed?checked(Current.Generation+1):Current.Generation;
            Current=new SessionSnapshot(role,role==SessionRole.Menu?0:facts.WorldId,epoch,generation,role!=SessionRole.Menu&&facts.ConfigurationReady&&epoch.Length>0,role!=SessionRole.Menu&&facts.IsAdmin);
            Peers.UpdateSession(Current);return Current;
        }
        public void Reset(){Current=new SessionSnapshot(SessionRole.Menu,0,"",checked(Current.Generation+1),false,false);Peers.UpdateSession(Current);}
    }
}
