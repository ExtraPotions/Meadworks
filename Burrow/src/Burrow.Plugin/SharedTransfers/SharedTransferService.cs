using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BepInEx;
using Burrow.Resources;
using ExtraPotions.Burrow;
using ExtraPotions.Meadworks.Crucible;
using CruciblePlugin=ExtraPotions.Meadworks.Crucible.Plugin;

namespace Burrow.SharedTransfers {
    internal sealed class SharedTransferService:IDisposable {
        internal readonly SharedTransferOwner Owner=new SharedTransferOwner();
        internal readonly SharedTransferClient Client=new SharedTransferClient();
        readonly IDisposable operation,command;
        readonly Dictionary<string,Task<OperationResult<TransferReceipt>>> pending=new Dictionary<string,Task<OperationResult<TransferReceipt>>>(StringComparer.Ordinal);
        InventoryDecisionJournal intents,receipts;long world;bool disposed,fault;
        internal SharedTransferService(ProductRegistration registration){
            command=CruciblePlugin.Services.PeerCommands.Register(registration,new OperationDescriptor(TransferWire.Command,TransferWire.Version),new OwnerTransferCodec(),(context,request)=>Owner.Handle(context,request));
            operation=CruciblePlugin.Services.Operations.Register(registration,new OperationDescriptor(TransferWire.Operation,TransferWire.Version),new TransferCodec(),Handle);
        }
        internal static ZDO FindChest(string id)=>NativeChestIdentity.Find(id,false,true);
        bool Open(long requestedWorld){
            if(intents!=null&&world==requestedWorld)return !fault;
            Close();world=requestedWorld;
            try{string folder=Path.Combine(Paths.BepInExRootPath,"Burrow.shared-authority",TransferWire.Hash(world.ToString(CultureInfo.InvariantCulture)));intents=new InventoryDecisionJournal(Path.Combine(folder,"intents.bin"));receipts=new InventoryDecisionJournal(Path.Combine(folder,"receipts.bin"));return true;}
            catch{fault=true;return false;}
        }
        OperationExecution<TransferReceipt> Handle(AuthorityContext context,TransferRequest request){
            if(disposed||context.ActorId!=request.Actor||context.Session.WorldId!=request.World||!Guid.TryParseExact(request.Root,"N",out _))return OperationExecution<TransferReceipt>.Rejected("TransferIdentity");
            if(!Open(request.World))return OperationExecution<TransferReceipt>.RecoveryRequired();
            string key=TransferWire.Hash(request.Root),fingerprint=TransferWire.Hash(TransferWire.Encode(request));var old=intents.Find(key);
            if(old!=null&&old.Fingerprint!=fingerprint)return OperationExecution<TransferReceipt>.Rejected("IdentityConflict");
            var saved=receipts.Find(key);
            if(saved!=null){var receipt=TransferWire.Receipt(saved.Before);if(!receipt.Matches(request))return OperationExecution<TransferReceipt>.RecoveryRequired();if(receipt.Disposition==TransferDisposition.Denied)return OperationExecution<TransferReceipt>.Success(receipt);}
            if(old==null){
                if(!intents.CanAdmit||!receipts.CanAdmit||pending.Count>=32)return OperationExecution<TransferReceipt>.Rejected("TransferCapacity");
                try{byte[] bytes=TransferWire.Encode(request);old=intents.Prepare(key,key,fingerprint,0,bytes,bytes);}catch{return OperationExecution<TransferReceipt>.RecoveryRequired();}
            }
            var chest=NativeChestIdentity.Find(request.Chest,old.State!=InventoryDecision.Prepared,true);
            if(chest==null||chest.GetOwner()==0){
                // A missing source can be refused only before any owner was asked.
                // Once forwarded, its disconnected owner may already have saved it.
                if(old.State!=InventoryDecision.Prepared)return OperationExecution<TransferReceipt>.RecoveryRequired();
                return SaveReceipt(key,request,TransferReceipt.Denied(request,Shared.ChestRequests.Denied));
            }
            try{if(old.State==InventoryDecision.Prepared)intents.Transition(old,InventoryDecision.Decided);context.MarkDecisionMade();}
            catch{return OperationExecution<TransferReceipt>.RecoveryRequired();}
            if(!pending.TryGetValue(key,out var task)){
                string sender=context.IsLocal?ZNet.GetUID().ToString(CultureInfo.InvariantCulture):context.SenderId;
                string owner=chest.GetOwner().ToString(CultureInfo.InvariantCulture);
                string correlation=TransferWire.Hash(request.Root+"|"+context.Session.SessionId+"|"+sender+"|"+owner+"|"+context.Identity.RequestId);
                task=CruciblePlugin.PeerCommandClient.SendAsync<OwnerCommand,TransferReceipt>(owner,TransferWire.Command,correlation,new OwnerCommand{Request=request,SenderPeer=sender},CancellationToken.None);pending[key]=task;
            }
            return Poll(key,request,task);
        }
        OperationExecution<TransferReceipt> Poll(string key,TransferRequest request,Task<OperationResult<TransferReceipt>> task){
            if(disposed||intents==null||world!=request.World)return OperationExecution<TransferReceipt>.RecoveryRequired();
            if(!task.IsCompleted)return OperationExecution<TransferReceipt>.Pending(()=>Poll(key,request,task));
            pending.Remove(key);
            if(task.IsFaulted||task.IsCanceled)return OperationExecution<TransferReceipt>.RecoveryRequired();
            var result=task.Result;
            if(result.State!=OperationState.Succeeded||result.Reply==null||!result.Reply.Matches(request)||result.Reply.Disposition==TransferDisposition.Recovery)return OperationExecution<TransferReceipt>.RecoveryRequired();
            return SaveReceipt(key,request,result.Reply);
        }
        OperationExecution<TransferReceipt> SaveReceipt(string key,TransferRequest request,TransferReceipt receipt){
            try{
                var saved=receipts.Find(key);byte[] bytes=TransferWire.Encode(receipt);
                if(saved==null){saved=receipts.Prepare(key,key,TransferWire.Hash(bytes),0,bytes,bytes);receipts.Transition(saved,InventoryDecision.Decided);receipts.Transition(saved,InventoryDecision.Committed);}
                else if(!saved.Before.SequenceEqual(bytes))return OperationExecution<TransferReceipt>.RecoveryRequired();
                var intent=intents.Find(key);
                if(intent.State==InventoryDecision.Prepared)intents.Transition(intent,InventoryDecision.Aborted);
                else if(intent.State==InventoryDecision.Decided)intents.Transition(intent,InventoryDecision.Committed);
                return OperationExecution<TransferReceipt>.Success(receipt);
            }catch{return OperationExecution<TransferReceipt>.RecoveryRequired();}
        }
        internal bool IsLocked(Inventory inventory)=>Client.IsLocked(inventory)||Owner.IsLocked(inventory);
        internal void Tick(){if(disposed)return;Owner.Tick();Client.Tick();if(intents!=null&&(!CruciblePlugin.Session.IsAuthority||CruciblePlugin.Session.WorldId!=world))Close();}
        void Close(){intents?.Dispose();receipts?.Dispose();intents=null;receipts=null;pending.Clear();fault=false;world=0;}
        public void Dispose(){if(disposed)return;disposed=true;operation.Dispose();command.Dispose();Client.Dispose();Owner.Dispose();Close();}
    }
}
