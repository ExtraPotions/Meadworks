using System;
using System.IO;
using System.Text;
using ExtraPotions.Meadworks.Crucible;
using UnityEngine;

namespace ExtraPotions.Wildroot.Runtime {
 internal sealed class NativeHarvestRequest {
  internal string Root="",Actor="",Session="";internal long World;internal ZDOID Target;internal int Bonus;
  internal readonly float RequesterRadius;internal readonly Vector3 PrimaryOrigin;
  internal NativeHarvestRequest(float requesterRadius,Vector3 primaryOrigin){RequesterRadius=requesterRadius;PrimaryOrigin=primaryOrigin;}
 }
 internal sealed class NativeHarvestCommand {internal NativeHarvestRequest Request=null!;internal string SenderPeer="";}
 internal sealed class NativeHarvestDrop {internal ZDOID Id;internal int Amount;}
 internal sealed class NativeHarvestReceipt {
  internal NativeHarvestRequest Request=null!;internal bool Harvested;internal string Reason="";internal NativeHarvestDrop[] Drops=Array.Empty<NativeHarvestDrop>();
  internal bool Matches(NativeHarvestRequest value)=>Request.Root==value.Root&&Request.Actor==value.Actor&&Request.Session==value.Session&&Request.World==value.World&&Request.Target==value.Target&&Request.Bonus==value.Bonus&&Request.RequesterRadius.Equals(value.RequesterRadius)&&Request.PrimaryOrigin.Equals(value.PrimaryOrigin);
 }
 internal static class NativeHarvestWire {
  internal const string Operation="meadworks.wildroot.harvest",Command="meadworks.wildroot.harvest.owner";
  // Match C01's internal PeerProtocol.Version: authority requests use outer 1.0; the payload rejects old shapes independently.
  internal static readonly System.Version OperationVersion=new System.Version(1,0);
  // Owner commands carry their descriptor protocol through the peer-command transport.
  internal static readonly System.Version OwnerVersion=new System.Version(2,0);
  const int PayloadVersion=2;
  internal static byte[] Encode(NativeHarvestRequest value)=>Write(w=>Request(w,value));
  internal static NativeHarvestRequest RequestValue(byte[] bytes)=>Read(bytes,Request);
  internal static byte[] Encode(NativeHarvestCommand value)=>Write(w=>{Text(w,value.SenderPeer);Request(w,value.Request);});
  internal static NativeHarvestCommand CommandValue(byte[] bytes)=>Read(bytes,r=>new NativeHarvestCommand{SenderPeer=Text(r),Request=Request(r)});
  internal static byte[] Encode(NativeHarvestReceipt value)=>Write(w=>{Request(w,value.Request);w.Write(value.Harvested);Text(w,value.Reason);if(value.Drops.Length>128)throw new InvalidDataException("Harvest drop bound.");w.Write(value.Drops.Length);foreach(var drop in value.Drops){Id(w,drop.Id);if(drop.Amount<1)throw new InvalidDataException("Harvest stack.");w.Write(drop.Amount);}});
  internal static NativeHarvestReceipt ReceiptValue(byte[] bytes)=>Read(bytes,r=>{var value=new NativeHarvestReceipt{Request=Request(r),Harvested=r.ReadBoolean(),Reason=Text(r)};int count=r.ReadInt32();if(count<0||count>128)throw new InvalidDataException("Harvest drop bound.");value.Drops=new NativeHarvestDrop[count];for(int i=0;i<count;i++){var drop=new NativeHarvestDrop{Id=Id(r),Amount=r.ReadInt32()};if(drop.Amount<1)throw new InvalidDataException("Harvest stack.");for(int j=0;j<i;j++)if(value.Drops[j].Id==drop.Id)throw new InvalidDataException("Duplicate harvest drop.");value.Drops[i]=drop;}if(!value.Harvested&&count!=0)throw new InvalidDataException("Denied harvest drops.");return value;});
  static void Request(BinaryWriter w,NativeHarvestRequest value){ValidateRequest(value);w.Write(PayloadVersion);Text(w,value.Root);Text(w,value.Actor);Text(w,value.Session);w.Write(value.World);Id(w,value.Target);w.Write(value.Bonus);w.Write(value.RequesterRadius);w.Write(value.PrimaryOrigin.x);w.Write(value.PrimaryOrigin.y);w.Write(value.PrimaryOrigin.z);}
  static NativeHarvestRequest Request(BinaryReader r){
   if(r.ReadInt32()!=PayloadVersion)throw new InvalidDataException("Harvest version.");
   var root=Text(r);var actor=Text(r);var session=Text(r);long world=r.ReadInt64();var target=Id(r);int bonus=r.ReadInt32();float radius=r.ReadSingle();var origin=new Vector3(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());
   var value=new NativeHarvestRequest(radius,origin){Root=root,Actor=actor,Session=session,World=world,Target=target,Bonus=bonus};ValidateRequest(value);return value;
  }
  static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
  static void ValidateRequest(NativeHarvestRequest value){if(!Guid.TryParseExact(value.Root,"N",out _)||value.World==0||value.Bonus<0||value.Bonus>10000)throw new InvalidDataException("Harvest identity.");if(!Finite(value.RequesterRadius)||value.RequesterRadius<0||!Finite(value.PrimaryOrigin.x)||!Finite(value.PrimaryOrigin.y)||!Finite(value.PrimaryOrigin.z))throw new InvalidDataException("Harvest geometry.");}
  static void Id(BinaryWriter w,ZDOID id){if(id==ZDOID.None)throw new InvalidDataException("Harvest target.");w.Write(id.UserID);w.Write(id.ID);}
  static ZDOID Id(BinaryReader r){var id=new ZDOID(r.ReadInt64(),r.ReadUInt32());if(id==ZDOID.None)throw new InvalidDataException("Harvest target.");return id;}
  static void Text(BinaryWriter w,string value){Validate(value);w.Write(value);}
  static string Text(BinaryReader r){var value=r.ReadString();Validate(value);return value;}
  static void Validate(string value){if(string.IsNullOrWhiteSpace(value)||value.Length>128||value.IndexOfAny(new[]{'\r','\n','\0'})>=0)throw new InvalidDataException("Harvest text.");}
  static byte[] Write(Action<BinaryWriter> write){using(var stream=new MemoryStream()){using(var writer=new BinaryWriter(stream,Encoding.UTF8,true))write(writer);if(stream.Length>16384)throw new InvalidDataException("Harvest packet bound.");return stream.ToArray();}}
  static T Read<T>(byte[] bytes,Func<BinaryReader,T> read){if(bytes==null||bytes.Length>16384)throw new InvalidDataException("Harvest packet bound.");using(var stream=new MemoryStream(bytes,false))using(var reader=new BinaryReader(stream,Encoding.UTF8)){var value=read(reader);if(stream.Position!=stream.Length)throw new InvalidDataException("Harvest trailing bytes.");return value;}}
 }
 internal sealed class NativeHarvestCodec:IOperationCodec<NativeHarvestRequest,NativeHarvestReceipt>{public byte[] EncodeRequest(NativeHarvestRequest value)=>NativeHarvestWire.Encode(value);public NativeHarvestRequest DecodeRequest(byte[] bytes)=>NativeHarvestWire.RequestValue(bytes);public byte[] EncodeReply(NativeHarvestReceipt value)=>NativeHarvestWire.Encode(value);public NativeHarvestReceipt DecodeReply(byte[] bytes)=>NativeHarvestWire.ReceiptValue(bytes);}
 internal sealed class NativeHarvestCommandCodec:IOperationCodec<NativeHarvestCommand,NativeHarvestReceipt>{public byte[] EncodeRequest(NativeHarvestCommand value)=>NativeHarvestWire.Encode(value);public NativeHarvestCommand DecodeRequest(byte[] bytes)=>NativeHarvestWire.CommandValue(bytes);public byte[] EncodeReply(NativeHarvestReceipt value)=>NativeHarvestWire.Encode(value);public NativeHarvestReceipt DecodeReply(byte[] bytes)=>NativeHarvestWire.ReceiptValue(bytes);}
}
