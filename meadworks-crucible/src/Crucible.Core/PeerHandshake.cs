using System;
namespace ExtraPotions.Meadworks.Crucible {
    public sealed class PeerHandshake {
        public string Challenge{get;}public long WorldId{get;private set;}public string Epoch{get;private set;}="";
        public PeerHandshake(string challenge){RequestIdentity.IdentityText(challenge);Challenge=challenge;}
        public bool Accept(bool authenticatedServer,string challenge,long world,string epoch){
            if(!authenticatedServer||challenge!=Challenge||world==0||string.IsNullOrWhiteSpace(epoch)||epoch.Length>128||epoch.IndexOfAny(new[]{'\0','\r','\n'})>=0)return false;
            if(Epoch.Length>0&&(WorldId!=world||Epoch!=epoch))return false;
            WorldId=world;Epoch=epoch;return true;
        }
    }
}
