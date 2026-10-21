using System;
using System.IO;
using System.Text;
using ExtraPotions.Meadworks.Crucible;
using Wayfarer.Core;
namespace Wayfarer.CrucibleIntegration {
    internal static class SessionIntegration {
        internal const string Authorize="meadworks.wayfarer.teleport.authorize",Complete="meadworks.wayfarer.teleport.complete";
        static readonly System.Diagnostics.Stopwatch clock=System.Diagnostics.Stopwatch.StartNew();internal static double Now=>clock.Elapsed.TotalSeconds;
        internal static byte[] Write(Action<BinaryWriter> write){using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream,new UTF8Encoding(false,true),true)){write(writer);writer.Flush();if(stream.Length>16384)throw new InvalidDataException();return stream.ToArray();}}
        internal static T Read<T>(byte[] bytes,Func<BinaryReader,T> read){if(bytes.Length>16384)throw new InvalidDataException();using(var stream=new MemoryStream(bytes,false))using(var reader=new BinaryReader(stream,new UTF8Encoding(false,true))){var value=read(reader);if(stream.Position!=stream.Length)throw new InvalidDataException();return value;}}
        internal static string Text(BinaryReader reader){var value=reader.ReadString();if(string.IsNullOrWhiteSpace(value)||value.Length>128||value.IndexOfAny(new[]{'\0','\r','\n'})>=0)throw new InvalidDataException();return value;}
    }
    internal sealed class TeleportCodec:IOperationCodec<TeleportRequest,TeleportGrant> {
        public byte[] EncodeRequest(TeleportRequest r)=>SessionIntegration.Write(w=>{w.Write(r.RequestId);w.Write((int)r.Category);w.Write(r.DestinationId);w.Write(r.X);w.Write(r.Y);w.Write(r.Z);w.Write(r.IsLastKnown);});
        public TeleportRequest DecodeRequest(byte[] bytes)=>SessionIntegration.Read(bytes,r=>new TeleportRequest(SessionIntegration.Text(r),(Category)r.ReadInt32(),SessionIntegration.Text(r),r.ReadSingle(),r.ReadSingle(),r.ReadSingle(),r.ReadBoolean()));
        // Send a remaining duration: process-local monotonic timestamps are not
        // comparable between server/client. Never extend a grant past ten seconds.
        public byte[] EncodeReply(TeleportGrant g)=>SessionIntegration.Write(w=>{w.Write(g.ActorId);w.Write(g.WorldId);w.Write(g.SessionId);w.Write(g.RequestId);w.Write(g.GrantId);w.Write((int)g.Category);w.Write(g.DestinationId);w.Write(g.X);w.Write(g.Y);w.Write(g.Z);w.Write(Math.Max(0,Math.Min(10,g.ExpiresAt-SessionIntegration.Now)));});
        public TeleportGrant DecodeReply(byte[] bytes)=>SessionIntegration.Read(bytes,r=>{var actor=SessionIntegration.Text(r);var world=r.ReadInt64();var session=SessionIntegration.Text(r);var request=SessionIntegration.Text(r);var grant=SessionIntegration.Text(r);var category=(Category)r.ReadInt32();var destination=SessionIntegration.Text(r);var x=r.ReadSingle();var y=r.ReadSingle();var z=r.ReadSingle();var duration=r.ReadDouble();if(double.IsNaN(duration)||double.IsInfinity(duration)||duration<0||duration>10)throw new InvalidDataException();return new TeleportGrant(actor,world,session,request,grant,category,destination,x,y,z,SessionIntegration.Now+duration);});
    }
    internal sealed class CompletionCodec:IOperationCodec<TeleportCompletion,bool> {
        public byte[] EncodeRequest(TeleportCompletion c)=>SessionIntegration.Write(w=>{w.Write(c.RequestId);w.Write(c.GrantId);w.Write(c.Accepted);});
        public TeleportCompletion DecodeRequest(byte[] bytes)=>SessionIntegration.Read(bytes,r=>new TeleportCompletion(SessionIntegration.Text(r),SessionIntegration.Text(r),r.ReadBoolean()));
        public byte[] EncodeReply(bool value)=>new[]{value?(byte)1:(byte)0};
        public bool DecodeReply(byte[] bytes){if(bytes.Length!=1||bytes[0]>1)throw new InvalidDataException();return bytes[0]==1;}
    }
}
