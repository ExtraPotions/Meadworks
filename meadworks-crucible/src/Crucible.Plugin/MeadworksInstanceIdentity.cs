using System;
namespace ExtraPotions.Meadworks.Crucible {
    /// <summary>Thin ZDO adapter for <see cref="InstanceIdentity"/>. Tokens persist in the world save with the ZDO.</summary>
    public static class MeadworksInstanceIdentity {
        sealed class ZdoStore:IInstanceIdentityStore {
            readonly ZDO zdo;
            public ZdoStore(ZDO zdo){this.zdo=zdo;}
            public bool CanWrite=>zdo.IsOwner();
            public string Read(string key)=>zdo.GetString(key,"");
            public void Write(string key,string value)=>zdo.Set(key,value);
        }
        public static IInstanceIdentityStore Store(ZDO zdo)=>new ZdoStore(zdo??throw new ArgumentNullException(nameof(zdo)));
        public static InstanceToken? Read(ZDO zdo,string key)=>InstanceIdentity.Read(Store(zdo),key);
        public static InstanceToken? Read(ZNetView view,string key)=>view!=null&&view.IsValid()?Read(view.GetZDO(),key):null;
        /// <summary>Returns or assigns the token; only the ZDO owner assigns.</summary>
        public static InstanceIdentityResult Ensure(ZDO zdo,string key,bool repairInvalid=false)=>InstanceIdentity.Ensure(Store(zdo),key,null,repairInvalid);
        public static InstanceIdentityResult Ensure(ZNetView view,string key,bool repairInvalid=false){
            if(view==null||!view.IsValid())throw new InvalidOperationException("Instance identity requires a valid ZNetView.");
            return Ensure(view.GetZDO(),key,repairInvalid);
        }
    }
}
