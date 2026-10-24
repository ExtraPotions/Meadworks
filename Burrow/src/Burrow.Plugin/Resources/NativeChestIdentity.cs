using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ExtraPotions.Burrow;
using Burrow.Core;

namespace Burrow.Resources {
    /// <summary>Only native owners write identity. Lookup never claims ownership.</summary>
    internal static class NativeChestIdentity {
        internal const string Key="burrow.chest-identity.v1";
        static readonly Dictionary<string,List<ZDO>> index=new Dictionary<string,List<ZDO>>(StringComparer.Ordinal);
        static long indexedWorld;static double rebuildAt;
        // Clients receive world identity from the authenticated Core handshake;
        // native m_world is absent during menu/disconnect transitions.
        internal static long World=>ZNet.instance&&ExtraPotions.Meadworks.Crucible.Plugin.IsReady&&ExtraPotions.Meadworks.Crucible.Plugin.Session.AuthorityReady?ExtraPotions.Meadworks.Crucible.Plugin.Session.WorldId:0;
        static ChestIdentity Read(ZDO zdo){var bytes=zdo.GetByteArray(Key);return bytes==null||bytes.Length==0?null:ChestIdentity.Read(bytes,World);}
        internal static string Address(ZDO zdo)=>Read(zdo)?.Token??zdo.m_uid.ToString();
        internal static bool Matches(ZDO zdo,string id)=>Read(zdo)?.Matches(id)==true;
        internal static string Ensure(ZDO zdo,IEnumerable<string> retainedAliases=null){
            if(World==0||zdo==null||zdo.GetOwner()!=ZNet.GetUID())throw new InvalidOperationException("Only the native chest owner can persist identity.");
            var value=Read(zdo)??ChestIdentity.Create(World,zdo.m_uid.ToString());value=value.Remember(zdo.m_uid.ToString());
            if(retainedAliases!=null)foreach(var alias in retainedAliases)if(!value.Matches(alias))value=value.Remember(alias);
            byte[] bytes=value.Encode(),current=zdo.GetByteArray(Key);
            if(current==null||!current.SequenceEqual(bytes)){
                zdo.Set(Key,bytes);if(!zdo.GetByteArray(Key).SequenceEqual(bytes))throw new IOException("Native chest identity did not persist.");rebuildAt=0;
            }
            return value.Token;
        }
        static void Add(string key,ZDO zdo){if(!index.TryGetValue(key,out var values))index[key]=values=new List<ZDO>();if(!values.Contains(zdo))values.Add(zdo);}
        static void Rebuild(){
            index.Clear();indexedWorld=World;rebuildAt=UnityEngine.Time.realtimeSinceStartupAsDouble+0.5;
            foreach(var zdo in ZDOMan.instance.m_objectsByID.Values){
                Add(zdo.m_uid.ToString(),zdo);
                try{var identity=Read(zdo);if(identity==null)continue;Add(identity.Token,zdo);foreach(var alias in identity.Aliases)Add(alias,zdo);}catch(InvalidDataException){/* Malformed identity cannot become a runtime fallback. */}
            }
        }
        internal static ZDO Find(string id,bool requirePersistent=true,bool refresh=false){
            if(World==0||ZDOMan.instance==null||string.IsNullOrEmpty(id))return null;
            if(refresh||indexedWorld!=World||UnityEngine.Time.realtimeSinceStartupAsDouble>=rebuildAt)Rebuild();
            if(!index.TryGetValue(id,out var candidates))return null;
            // Revalidate cached candidates against live identity; ambiguity never
            // chooses a chest by position, inventory content or iteration order.
            try{return ChestIdentity.Resolve(candidates.Where(z=>ReferenceEquals(ZDOMan.instance.GetZDO(z.m_uid),z)),id,z=>z.m_uid.ToString(),Read,requirePersistent);}catch(InvalidDataException){return null;}
        }
        internal static Container Container(string id,bool requirePersistent=true,bool refresh=false){
            var zdo=Find(id,requirePersistent,refresh);return zdo==null?null:ContainerScan.All().FirstOrDefault(c=>c&&c.m_nview&&c.m_nview.IsValid()&&ReferenceEquals(c.m_nview.GetZDO(),zdo));
        }
    }
}
