// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Globalization;
using System.Text;

namespace ExtraPotions.Meadworks.Crucible {
    public enum WorldSettingSource { World, Config, Default }
    public enum WorldSettingWriteStatus { Refused, Unavailable, Invalid, Queued, Applied }

    /// <summary>CS4 values in native world global keys. Lowercase hex survives the game's ToLower without data loss.</summary>
    public static class WorldSettingStorage {
        const string Version="cs4v1:";
        public const int MaximumValueBytes=4096;
        static readonly UTF8Encoding Utf8=new UTF8Encoding(false,true);
        public static void RequireKey(string key){
            if(key==null||key.Length<6||key.Length>160||!key.StartsWith("mw.",StringComparison.Ordinal)
                ||key.EndsWith(".",StringComparison.Ordinal)||key.IndexOf("..",StringComparison.Ordinal)>=0)
                throw new ArgumentException("Expected mw.<product>.<setting> in lowercase ASCII.",nameof(key));
            if(key.IndexOf('.',3)<4)throw new ArgumentException("A world key needs product and setting segments.",nameof(key));
            foreach(char c in key)if(!(c>='a'&&c<='z')&&!(c>='0'&&c<='9')&&c!='.'&&c!='_'&&c!='-')
                throw new ArgumentException("World keys use lowercase ASCII letters, digits, '.', '_' or '-'.",nameof(key));
        }
        public static bool Reserved(string key)=>key!=null&&key.StartsWith("mw.",StringComparison.OrdinalIgnoreCase);
        public static string Encode(long worldId,string value){
            if(worldId==0)throw new ArgumentException("No active world.",nameof(worldId));
            if(value==null)throw new ArgumentNullException(nameof(value));
            if(value.Length>MaximumValueBytes||Utf8.GetByteCount(value)>MaximumValueBytes)
                throw new ArgumentException("World setting value exceeds the storage bound.",nameof(value));
            byte[] bytes=Utf8.GetBytes(value);
            var hex=new StringBuilder(bytes.Length*2);
            foreach(byte b in bytes)hex.Append(b.ToString("x2",CultureInfo.InvariantCulture));
            return Version+unchecked((ulong)worldId).ToString("x16",CultureInfo.InvariantCulture)+":"+hex;
        }
        /// <summary>Reads this world's versioned value, or an existing plain global-key value. Unknown versions stay untouched.</summary>
        public static bool TryDecode(long worldId,string stored,out string value,bool requireVersion=false){
            value="";
            if(worldId==0||stored==null)return false;
            if(!stored.StartsWith("cs4v",StringComparison.Ordinal)){
                if(requireVersion)return false;
                try{if(stored.Length>MaximumValueBytes||Utf8.GetByteCount(stored)>MaximumValueBytes)return false;}catch(EncoderFallbackException){return false;}
                value=stored;return true;
            }
            if(!stored.StartsWith(Version,StringComparison.Ordinal)||stored.Length<Version.Length+17)return false;
            int start=Version.Length;
            if(stored[start+16]!=':'||!ulong.TryParse(stored.Substring(start,16),NumberStyles.AllowHexSpecifier,CultureInfo.InvariantCulture,out var uid)
                ||uid!=unchecked((ulong)worldId))return false;
            int length=stored.Length-start-17;
            if(length>MaximumValueBytes*2||length%2!=0)return false;
            string hex=stored.Substring(start+17);
            var bytes=new byte[hex.Length/2];
            for(int i=0;i<bytes.Length;i++){
                if(!byte.TryParse(hex.Substring(i*2,2),NumberStyles.AllowHexSpecifier,CultureInfo.InvariantCulture,out bytes[i]))return false;
            }
            try{value=Utf8.GetString(bytes);return true;}catch(DecoderFallbackException){return false;}
        }
        /// <summary>World edits always require an active ready world and server authority, even for optional products.</summary>
        public static AuthorityDecision Decide(AuthorityFacts facts,long worldId,bool storageReady){
            if(facts==null)throw new ArgumentNullException(nameof(facts));
            if(worldId==0||!storageReady||facts.Role==SessionRole.Menu||!facts.AuthorityReady)
                return AuthorityGate.Decide(ConfigScope.ServerAuthoritative,new AuthorityFacts(SessionRole.RemoteClient,false,false,ServerCrucible.Unknown,PeerClass.RequiredOnAllPeers));
            if(facts.Role==SessionRole.RemoteClient){
                if(!AuthorityGate.ProductActive(facts,out _))return AuthorityGate.Decide(ConfigScope.ServerAuthoritative,facts,true);
                if(facts.Server==ServerCrucible.Unknown)
                    return AuthorityGate.Decide(ConfigScope.ServerAuthoritative,new AuthorityFacts(SessionRole.RemoteClient,false,false,ServerCrucible.Unknown,PeerClass.RequiredOnAllPeers));
                facts=new AuthorityFacts(facts.Role,facts.AuthorityReady,facts.IsAdmin,facts.Server,PeerClass.RequiredOnAllPeers,facts.ServerProduct);
            }
            return AuthorityGate.Decide(ConfigScope.ServerAuthoritative,facts,true);
        }
    }
}
