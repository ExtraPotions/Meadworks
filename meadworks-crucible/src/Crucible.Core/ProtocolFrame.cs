using System;
using System.IO;
using System.Text;
namespace ExtraPotions.Meadworks.Crucible {
    public enum FrameKind { Probe=1,Hello=2,Capabilities=3,Request=4,Status=5,Cancel=6,Reply=7 }
    // Identity here is correlation only. The receiving native connection supplies
    // the actor and admin facts; these fields cannot confer authority.
    public sealed class ProtocolFrame {
        readonly byte[] payload;
        public FrameKind Kind{get;}public long WorldId{get;}public string Epoch{get;}public string RequestId{get;}public string OperationId{get;}public byte[] Payload=>(byte[])payload.Clone();
        public ProtocolFrame(FrameKind kind,long world,string epoch,string request,string operation,byte[] payload){
            if(!Enum.IsDefined(typeof(FrameKind),kind))throw new ArgumentException("Unknown frame.");
            TextBound(epoch);TextBound(request);TextBound(operation);if(payload==null||payload.Length>49152)throw new ArgumentException("Frame payload exceeds bounds.");
            Kind=kind;WorldId=world;Epoch=epoch;RequestId=request;OperationId=operation;this.payload=(byte[])payload.Clone();
        }
        static void TextBound(string value){if(value==null||value.Length>128||value.IndexOfAny(new[]{'\0','\r','\n'})>=0)throw new ArgumentException("Invalid wire text.");}
        static readonly UTF8Encoding Utf8=new UTF8Encoding(false,true);
        static void WriteText(BinaryWriter writer,string value){var bytes=Utf8.GetBytes(value);writer.Write(bytes.Length);writer.Write(bytes);}
        static string ReadText(BinaryReader reader){var length=reader.ReadInt32();if(length<0||length>512||length>reader.BaseStream.Length-reader.BaseStream.Position)throw new InvalidDataException("Invalid text length.");var text=Utf8.GetString(reader.ReadBytes(length));TextBound(text);return text;}
        public byte[] Encode(){using(var stream=new MemoryStream()){using(var writer=new BinaryWriter(stream,Utf8,true)){writer.Write(0x43525543);writer.Write(1);writer.Write(0);writer.Write((int)Kind);writer.Write(WorldId);WriteText(writer,Epoch);WriteText(writer,RequestId);WriteText(writer,OperationId);writer.Write(payload.Length);writer.Write(payload);}return stream.ToArray();}}
        public static ProtocolFrame Decode(byte[] bytes){
            if(bytes==null||bytes.Length>65536)throw new ArgumentException("Frame exceeds bounds.");
            using(var stream=new MemoryStream(bytes,false))using(var reader=new BinaryReader(stream,Utf8)){
                if(reader.ReadInt32()!=0x43525543||reader.ReadInt32()!=1||reader.ReadInt32()!=0)throw new InvalidDataException("Unsupported wire protocol.");
                var kind=(FrameKind)reader.ReadInt32();var world=reader.ReadInt64();var epoch=ReadText(reader);var request=ReadText(reader);var operation=ReadText(reader);var length=reader.ReadInt32();
                if(length<0||length>49152||length!=stream.Length-stream.Position)throw new InvalidDataException("Invalid payload length.");
                return new ProtocolFrame(kind,world,epoch,request,operation,reader.ReadBytes(length));
            }
        }
    }
}
