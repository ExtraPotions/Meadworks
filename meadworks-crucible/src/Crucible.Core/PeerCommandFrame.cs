using System;
using System.IO;
using System.Text;

namespace ExtraPotions.Meadworks.Crucible {
    public enum PeerCommandFrameKind { Bind=1, Bound=2, Request=3, Status=4, Cancel=5, Reply=6 }
    // These fields bind a command to a native connection. They never establish
    // authority by themselves; the native adapter verifies the actual sender.
    public sealed class PeerCommandFrame {
        const int Magic=0x50434D44;
        static readonly UTF8Encoding Utf8=new UTF8Encoding(false,true);
        readonly byte[] payload;
        public PeerCommandFrameKind Kind{get;}
        public long WorldId{get;}
        public string Epoch{get;}
        public string TargetPeerId{get;}
        public string ConnectionId{get;}
        public string CorrelationId{get;}
        public string CommandId{get;}
        public Version Protocol{get;}
        public byte[] Payload=>(byte[])payload.Clone();
        public PeerCommandFrame(PeerCommandFrameKind kind,long world,string epoch,string target,string connection,string correlation,string command,Version protocol,byte[] payload){
            if(!Enum.IsDefined(typeof(PeerCommandFrameKind),kind))throw new ArgumentException("Unknown owner frame.");
            if(world==0)throw new ArgumentException("Missing owner world.");
            RequestIdentity.IdentityText(epoch);RequestIdentity.IdentityText(target);RequestIdentity.IdentityText(connection);
            TextBound(correlation);TextBound(command);Validation.Version(protocol);
            bool binding=kind==PeerCommandFrameKind.Bind||kind==PeerCommandFrameKind.Bound;
            if(binding){if(correlation.Length!=0||command.Length!=0)throw new ArgumentException("Unexpected binding application identity.");}
            else {RequestIdentity.IdentityText(correlation);new OperationDescriptor(command,protocol);}
            if(payload==null||payload.Length>(kind==PeerCommandFrameKind.Reply?49152:kind==PeerCommandFrameKind.Request?16384:0))throw new ArgumentException("Owner frame payload exceeds bounds.");
            Kind=kind;WorldId=world;Epoch=epoch;TargetPeerId=target;ConnectionId=connection;CorrelationId=correlation;CommandId=command;Protocol=protocol;this.payload=(byte[])payload.Clone();
        }
        static void TextBound(string value){if(value==null||value.Length>128||value.IndexOfAny(new[]{'\0','\r','\n'})>=0)throw new ArgumentException("Invalid owner wire text.");}
        static void WriteText(BinaryWriter writer,string value){var bytes=Utf8.GetBytes(value);writer.Write(bytes.Length);writer.Write(bytes);}
        static string ReadText(BinaryReader reader){var length=reader.ReadInt32();if(length<0||length>512||length>reader.BaseStream.Length-reader.BaseStream.Position)throw new InvalidDataException("Invalid owner text length.");var text=Utf8.GetString(reader.ReadBytes(length));TextBound(text);return text;}
        public byte[] Encode(){
            using(var stream=new MemoryStream()){using(var writer=new BinaryWriter(stream,Utf8,true)){
                writer.Write(Magic);writer.Write(1);writer.Write(0);writer.Write((int)Kind);writer.Write(WorldId);
                WriteText(writer,Epoch);WriteText(writer,TargetPeerId);WriteText(writer,ConnectionId);WriteText(writer,CorrelationId);WriteText(writer,CommandId);
                writer.Write(Protocol.Major);writer.Write(Protocol.Minor);writer.Write(payload.Length);writer.Write(payload);
            }return stream.ToArray();}
        }
        public static PeerCommandFrame Decode(byte[] bytes){
            if(bytes==null||bytes.Length>65536)throw new ArgumentException("Owner frame exceeds bounds.");
            using(var stream=new MemoryStream(bytes,false))using(var reader=new BinaryReader(stream,Utf8)){
                if(reader.ReadInt32()!=Magic||reader.ReadInt32()!=1||reader.ReadInt32()!=0)throw new InvalidDataException("Unsupported owner wire protocol.");
                var kind=(PeerCommandFrameKind)reader.ReadInt32();var world=reader.ReadInt64();
                var epoch=ReadText(reader);var target=ReadText(reader);var connection=ReadText(reader);var correlation=ReadText(reader);var command=ReadText(reader);
                var protocol=new Version(reader.ReadInt32(),reader.ReadInt32());var length=reader.ReadInt32();
                if(length<0||length>49152||length!=stream.Length-stream.Position)throw new InvalidDataException("Invalid owner payload length.");
                return new PeerCommandFrame(kind,world,epoch,target,connection,correlation,command,protocol,reader.ReadBytes(length));
            }
        }
        public static PeerCommandFrame Reply(PeerCommandFrame request,OperationResult result){
            var id=result.Identity;
            if(id.ActorId!=request.TargetPeerId||id.WorldId!=request.WorldId||id.SessionId!=request.Epoch||id.RequestId!=request.CorrelationId)throw new ArgumentException("Owner reply identity mismatch.");
            var bytes=result.Reply;if(bytes.Length>16384||!Enum.IsDefined(typeof(OperationState),result.State))throw new ArgumentException("Invalid owner reply.");
            TextBound(result.Code);if(string.IsNullOrWhiteSpace(result.Code)||result.Code.Length>64)throw new ArgumentException("Invalid owner result code.");
            using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream,Utf8,true)){
                writer.Write((int)result.State);WriteText(writer,result.Code);writer.Write(bytes.Length);writer.Write(bytes);writer.Flush();
                return new PeerCommandFrame(PeerCommandFrameKind.Reply,request.WorldId,request.Epoch,request.TargetPeerId,request.ConnectionId,request.CorrelationId,request.CommandId,request.Protocol,stream.ToArray());
            }
        }
        public OperationResult ReadReply(){
            if(Kind!=PeerCommandFrameKind.Reply)throw new InvalidDataException("Expected owner reply.");
            using(var stream=new MemoryStream(payload,false))using(var reader=new BinaryReader(stream,Utf8)){
                var state=(OperationState)reader.ReadInt32();if(!Enum.IsDefined(typeof(OperationState),state))throw new InvalidDataException("Invalid owner state.");
                var code=ReadText(reader);if(string.IsNullOrWhiteSpace(code)||code.Length>64)throw new InvalidDataException("Invalid owner code.");
                var length=reader.ReadInt32();if(length<0||length>16384||length!=stream.Length-stream.Position||state!=OperationState.Succeeded&&length!=0)throw new InvalidDataException("Invalid owner reply length.");
                return new OperationResult(new RequestIdentity(TargetPeerId,WorldId,Epoch,CorrelationId),state,code,reader.ReadBytes(length));
            }
        }
    }
}
