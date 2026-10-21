using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
namespace ExtraPotions.Meadworks.Crucible {
    internal static class PeerProtocol {
        internal static readonly System.Version Version=new System.Version(1,0);
        internal static byte[] Capabilities(ProductRegistry registry){
            using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream,Encoding.UTF8,true)){
                var products=registry.Snapshot();if(products.Count>256)throw new InvalidOperationException("Too many products.");writer.Write(products.Count);
                foreach(var product in products){writer.Write(product.Id);writer.Write(1);writer.Write(0);}writer.Flush();return stream.ToArray();
            }
        }
        /// <summary>This peer's products as the C04 handshake sees them.</summary>
        internal static IReadOnlyList<PeerProduct> LocalProducts(ProductRegistry registry){var list=new List<PeerProduct>();foreach(var product in registry.Snapshot())list.Add(PeerProduct.From(product));return list;}
        /// <summary>v2 capability payload (product major.minor and peer class); see <see cref="CapabilityCodec"/>.</summary>
        internal static byte[] CapabilitiesV2(ProductRegistry registry)=>CapabilityCodec.Encode(LocalProducts(registry));
        internal static IReadOnlyList<(string,System.Version)> ReadCapabilities(byte[] bytes){
            if(bytes.Length>49152)throw new InvalidDataException();var result=new List<(string,System.Version)>();var ids=new HashSet<string>();
            using(var stream=new MemoryStream(bytes,false))using(var reader=new BinaryReader(stream,Encoding.UTF8)){
                var count=reader.ReadInt32();if(count<0||count>256)throw new InvalidDataException();
                for(int i=0;i<count;i++){var id=reader.ReadString();if(id.Length>128||!ids.Add(id))throw new InvalidDataException();var version=new System.Version(reader.ReadInt32(),reader.ReadInt32());if(version!=Version)throw new InvalidDataException("Protocol mismatch.");result.Add((id,version));}
                if(stream.Position!=stream.Length)throw new InvalidDataException();
            }return result;
        }
        internal static byte[] Reply(OperationResult result){using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream,Encoding.UTF8,true)){writer.Write((int)result.State);writer.Write(result.Code);var bytes=result.Reply;if(bytes.Length>16384)throw new InvalidDataException();writer.Write(bytes.Length);writer.Write(bytes);writer.Flush();return stream.ToArray();}}
        internal static OperationResult ReadReply(ProtocolFrame frame,string actor){using(var stream=new MemoryStream(frame.Payload,false))using(var reader=new BinaryReader(stream,Encoding.UTF8)){
            var state=(OperationState)reader.ReadInt32();if(!Enum.IsDefined(typeof(OperationState),state))throw new InvalidDataException();var code=reader.ReadString();if(code.Length>64||code.IndexOfAny(new[]{'\0','\r','\n'})>=0)throw new InvalidDataException();var length=reader.ReadInt32();if(length<0||length>16384||length!=stream.Length-stream.Position)throw new InvalidDataException();
            return new OperationResult(new RequestIdentity(actor,frame.WorldId,frame.Epoch,frame.RequestId),state,code,reader.ReadBytes(length));}}
    }
}
