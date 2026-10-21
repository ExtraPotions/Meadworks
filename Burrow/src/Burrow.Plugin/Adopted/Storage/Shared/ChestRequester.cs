using System;
namespace Burrow.Shared {
    public static class ChestRequester {
        public static bool IsPending(Container container,Vector2i slot)=>Plugin.Instance?.Transfers?.Client.IsPending(container,slot)==true;
        public static void Send(Container container,string rpc,Vector2i slot,Action<ZPackage> writeBody,Action<ZPackage> granted,Action<string> refused,Vector2i? targetSlot=null){
            var client=Plugin.Instance?.Transfers?.Client;
            if(client==null){refused(ChestRequests.Timeout);return;}
            client.Send(container,rpc,slot,writeBody,granted,refused,targetSlot);
        }
        // Only typed Crucible receipts can settle transfers.
        public static void Receive(Container container,long sender,ZPackage package){}
        public static void CheckTimeouts(){}
    }
}
