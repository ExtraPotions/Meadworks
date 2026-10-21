using System;
using HarmonyLib;
namespace Burrow.Shared {
    public static class ChestRequests {
        public const string TakeRpc="Burrow_Take",PutRpc="Burrow_Put",MoveRpc="Burrow_Move",TakeAllRpc="Burrow_TakeAll",StackAllRpc="Burrow_StackAll",ReplyRpc="Burrow_Reply",TouchRpc="Burrow_Touch";
        public const string NotOwner="not owner",Denied="denied",ChestFull="chestfull",Nothing="nothing",Bad="bad",Timeout="timeout";
        public readonly struct Header {
            public readonly long Id,PlayerId;public readonly string PlayerName;
            public Header(long id,long playerId,string playerName){Id=id;PlayerId=playerId;PlayerName=playerName;}
        }
        public static ZPackage NewRequest(long id){var player=Player.m_localPlayer;var package=new ZPackage();package.Write(id);package.Write(player?player.GetPlayerID():0);package.Write(player?player.GetPlayerName():"");return package;}
        public static Header ReadHeader(ZPackage package)=>new Header(package.ReadLong(),package.ReadLong(),package.ReadString());
        public static bool Send(Container container,string rpc,long id,ZPackage package)=>false;
        public static void Reply(Container container,long peer,Header header,bool ok,string reason,ZPackage payload)=>SharedTransfers.SharedTransferOwner.CaptureReply(ok,reason,payload);
        internal static void Register(Container container){
            var view=container.m_nview;if(!view||view.m_functions==null||view.m_functions.ContainsKey(TouchRpc.GetStableHashCode()))return;
            view.Register<ZPackage>(TouchRpc,(sender,package)=>{try{Touches.Receive(container,sender,package);}catch(Exception error){Plugin.Log.LogDebug("Burrow touch ignored: "+error.GetType().Name);}});
        }
        [HarmonyPatch(typeof(Container),nameof(Container.Awake))]
        private static class RegisterPatch {[HarmonyPostfix]private static void Postfix(Container __instance){if(__instance.m_nview&&__instance.m_nview.GetZDO()!=null)Register(__instance);}}
    }
}
