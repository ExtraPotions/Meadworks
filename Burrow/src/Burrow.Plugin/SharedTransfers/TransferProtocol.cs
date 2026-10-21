using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using ExtraPotions.Meadworks.Crucible;

namespace Burrow.SharedTransfers {
    internal enum TransferDisposition { Approved, Denied, Recovery }
    // Root is a durable product identity. Crucible supplies a new authenticated
    // transport identity on reconnect; it never replaces this transfer identity.
    internal sealed class TransferRequest {
        internal string Root,Actor,Session,Chest,Kind;internal long World;
        internal byte[] Body,Expected;
    }
    internal sealed class OwnerCommand {internal TransferRequest Request;internal string SenderPeer;}
    internal sealed class TransferReceipt {
        internal string Root,Actor,Chest,Kind,Reason;internal long World;
        internal TransferDisposition Disposition;internal byte[] Payload;
        internal static TransferReceipt Denied(TransferRequest request,string reason)=>new TransferReceipt{Root=request.Root,Actor=request.Actor,World=request.World,Chest=request.Chest,Kind=request.Kind,Disposition=TransferDisposition.Denied,Reason=reason,Payload=Array.Empty<byte>()};
        internal bool Matches(TransferRequest request)=>Root==request.Root&&Actor==request.Actor&&World==request.World&&Chest==request.Chest&&Kind==request.Kind;
    }
    internal static class TransferWire {
        internal const string Operation="meadworks.burrow.transfer",Command="meadworks.burrow.transfer.owner";
        internal const int Maximum=16384;
        internal static readonly System.Version Version=new System.Version(1,0);
        internal static string Hash(byte[] bytes){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}
        internal static string Hash(string text)=>Hash(Encoding.UTF8.GetBytes(text));
        internal static byte[] Encode(TransferRequest value)=>Write(w=>Request(w,value));
        internal static TransferRequest Decode(byte[] bytes)=>Read(bytes,Request);
        internal static byte[] Encode(TransferReceipt value)=>Write(w=>{w.Write(1);Text(w,value.Root);Text(w,value.Actor);w.Write(value.World);Text(w,value.Chest);Text(w,value.Kind);w.Write((int)value.Disposition);Text(w,value.Reason??"",true);Blob(w,value.Payload??Array.Empty<byte>());});
        internal static TransferReceipt Receipt(byte[] bytes)=>Read(bytes,r=>{if(r.ReadInt32()!=1)throw new InvalidDataException("Transfer receipt version.");var value=new TransferReceipt{Root=Text(r),Actor=Text(r),World=r.ReadInt64(),Chest=Text(r),Kind=Text(r),Disposition=(TransferDisposition)r.ReadInt32(),Reason=Text(r,true),Payload=Blob(r)};if(value.World==0||value.Disposition<TransferDisposition.Approved||value.Disposition>TransferDisposition.Recovery)throw new InvalidDataException("Transfer receipt identity.");return value;});
        internal static byte[] Encode(OwnerCommand value)=>Write(w=>{Text(w,value.SenderPeer);Blob(w,Encode(value.Request));});
        internal static OwnerCommand CommandValue(byte[] bytes)=>Read(bytes,r=>new OwnerCommand{SenderPeer=Text(r),Request=Decode(Blob(r))});
        static void Request(BinaryWriter w,TransferRequest value){w.Write(1);Text(w,value.Root);Text(w,value.Actor);w.Write(value.World);Text(w,value.Session);Text(w,value.Chest);Text(w,value.Kind);Blob(w,value.Body);Blob(w,value.Expected);}
        static TransferRequest Request(BinaryReader r){if(r.ReadInt32()!=1)throw new InvalidDataException("Transfer request version.");var value=new TransferRequest{Root=Text(r),Actor=Text(r),World=r.ReadInt64(),Session=Text(r),Chest=Text(r),Kind=Text(r),Body=Blob(r),Expected=Blob(r)};if(value.World==0||value.Expected.Length!=32||!new[]{Shared.ChestRequests.TakeRpc,Shared.ChestRequests.PutRpc,Shared.ChestRequests.MoveRpc,Shared.ChestRequests.TakeAllRpc,Shared.ChestRequests.StackAllRpc}.Contains(value.Kind))throw new InvalidDataException("Transfer request scope.");return value;}
        static byte[] Write(Action<BinaryWriter> action){using(var bytes=new MemoryStream()){using(var writer=new BinaryWriter(bytes,Encoding.UTF8,true))action(writer);if(bytes.Length>Maximum)throw new InvalidDataException("Transfer packet exceeds bound.");return bytes.ToArray();}}
        static T Read<T>(byte[] bytes,Func<BinaryReader,T> read){if(bytes==null||bytes.Length>Maximum)throw new InvalidDataException("Transfer packet exceeds bound.");using(var stream=new MemoryStream(bytes,false))using(var reader=new BinaryReader(stream,Encoding.UTF8)){var value=read(reader);if(stream.Position!=stream.Length)throw new InvalidDataException("Transfer packet trailing data.");return value;}}
        static void Text(BinaryWriter writer,string value,bool empty=false){Validate(value,empty);writer.Write(value);}
        static string Text(BinaryReader reader,bool empty=false){string value=reader.ReadString();Validate(value,empty);return value;}
        static void Validate(string value,bool empty){if(value==null||(!empty&&string.IsNullOrWhiteSpace(value))||value.Length>128||value.IndexOfAny(new[]{'\0','\r','\n'})>=0)throw new InvalidDataException("Transfer text identity.");}
        static void Blob(BinaryWriter writer,byte[] bytes){if(bytes==null||bytes.Length>Maximum)throw new InvalidDataException("Transfer body bound.");writer.Write(bytes.Length);writer.Write(bytes);}
        static byte[] Blob(BinaryReader reader){int length=reader.ReadInt32();if(length<0||length>Maximum||length>reader.BaseStream.Length-reader.BaseStream.Position)throw new InvalidDataException("Transfer body bound.");return reader.ReadBytes(length);}
    }
    internal sealed class TransferCodec:IOperationCodec<TransferRequest,TransferReceipt>{public byte[] EncodeRequest(TransferRequest request)=>TransferWire.Encode(request);public TransferRequest DecodeRequest(byte[] bytes)=>TransferWire.Decode(bytes);public byte[] EncodeReply(TransferReceipt receipt)=>TransferWire.Encode(receipt);public TransferReceipt DecodeReply(byte[] bytes)=>TransferWire.Receipt(bytes);}
    internal sealed class OwnerTransferCodec:IOperationCodec<OwnerCommand,TransferReceipt>{public byte[] EncodeRequest(OwnerCommand request)=>TransferWire.Encode(request);public OwnerCommand DecodeRequest(byte[] bytes)=>TransferWire.CommandValue(bytes);public byte[] EncodeReply(TransferReceipt receipt)=>TransferWire.Encode(receipt);public TransferReceipt DecodeReply(byte[] bytes)=>TransferWire.Receipt(bytes);}
}
