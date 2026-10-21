using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
namespace Burrow.SharedTransfers {
    internal static class OwnerDecisionCodec {
        internal const int MaximumMigration=65536,MaximumDecoded=8388608,MaximumEntries=4096;
        internal sealed class Entry {internal byte[] Metadata,Before,After;}
        internal sealed class MetadataValue {internal TransferRequest Request;internal TransferReceipt Receipt;}
        // Session is a transport epoch, while Root owns the durable decision.
        internal static bool SameRequest(TransferRequest a,TransferRequest b)=>a.Root==b.Root&&a.Actor==b.Actor&&a.World==b.World&&a.Chest==b.Chest&&a.Kind==b.Kind&&a.Body.SequenceEqual(b.Body)&&a.Expected.SequenceEqual(b.Expected);
        internal static string Source(TransferRequest request)=>"shared:"+TransferWire.Hash(request.World.ToString(System.Globalization.CultureInfo.InvariantCulture)+"\n"+request.Actor+"\n"+request.Root);
        internal static string Fingerprint(TransferRequest request){var frozen=TransferWire.Decode(TransferWire.Encode(request));frozen.Session="durable";return TransferWire.Hash(TransferWire.Encode(frozen));}
        internal static byte[] Metadata(TransferRequest request,TransferReceipt receipt){
            if(!receipt.Matches(request)||receipt.Disposition==TransferDisposition.Recovery)throw new InvalidDataException("Mismatched owner receipt.");
            return Write(TransferWire.Maximum,w=>{w.Write(1);Blob(w,TransferWire.Encode(request));Blob(w,TransferWire.Encode(receipt));});
        }
        internal static MetadataValue ReadMetadata(byte[] bytes)=>Read(bytes,TransferWire.Maximum,r=>{
            if(r.ReadInt32()!=1)throw new InvalidDataException("Owner metadata version.");
            var value=new MetadataValue{Request=TransferWire.Decode(Blob(r,TransferWire.Maximum)),Receipt=TransferWire.Receipt(Blob(r,TransferWire.Maximum))};
            if(!value.Receipt.Matches(value.Request)||value.Receipt.Disposition==TransferDisposition.Recovery)throw new InvalidDataException("Mismatched owner receipt.");return value;
        });
        internal static byte[] Migration(IEnumerable<Entry> entries){
            var list=entries.ToList();if(list.Count>MaximumEntries)throw new InvalidDataException("Owner migration capacity.");
            if(list.Select(e=>Source(ReadMetadata(e.Metadata).Request)).Distinct(StringComparer.Ordinal).Count()!=list.Count)throw new InvalidDataException("Duplicate owner migration identity.");
            var body=Write(MaximumDecoded,w=>{w.Write(1);w.Write(list.Count);foreach(var entry in list){Blob(w,entry.Metadata);Blob(w,entry.Before);Blob(w,entry.After);}});
            using(var memory=new MemoryStream()){
                using(var writer=new BinaryWriter(memory,Encoding.UTF8,true)){writer.Write(2);writer.Write(body.Length);}
                using(var zip=new DeflateStream(memory,CompressionLevel.Optimal,true))zip.Write(body,0,body.Length);
                using(var sha=SHA256.Create()){byte[] digest=sha.ComputeHash(memory.ToArray());memory.Write(digest,0,digest.Length);}
                if(memory.Length>MaximumMigration)throw new InvalidDataException("Compressed owner migration bound.");return memory.ToArray();
            }
        }
        internal static List<Entry> ReadMigration(byte[] bytes){
            if(bytes==null||bytes.Length<40||bytes.Length>MaximumMigration)throw new InvalidDataException("Owner migration bound.");
            using(var sha=SHA256.Create())if(!sha.ComputeHash(bytes.Take(bytes.Length-32).ToArray()).SequenceEqual(bytes.Skip(bytes.Length-32)))throw new InvalidDataException("Owner migration checksum.");
            byte[] body;
            using(var wire=new MemoryStream(bytes,false))using(var reader=new BinaryReader(wire,Encoding.UTF8)){
                int version=reader.ReadInt32();
                if(version==1)body=bytes.Take(bytes.Length-32).ToArray();
                else if(version==2){
                    int expected=reader.ReadInt32();if(expected<8||expected>MaximumDecoded)throw new InvalidDataException("Decoded owner migration bound.");
                    using(var packed=new MemoryStream(bytes,8,bytes.Length-40,false))using(var zip=new DeflateStream(packed,CompressionMode.Decompress))using(var decoded=new MemoryStream()){
                        byte[] chunk=new byte[8192];int count;
                        while((count=zip.Read(chunk,0,Math.Min(chunk.Length,expected-(int)decoded.Length+1)))>0){if(decoded.Length+count>expected)throw new InvalidDataException("Decoded owner migration exceeds declared bound.");decoded.Write(chunk,0,count);}
                        if(decoded.Length!=expected)throw new InvalidDataException("Decoded owner migration length.");body=decoded.ToArray();
                    }
                }else throw new InvalidDataException("Owner migration version.");
            }
            var list=Read(body,MaximumDecoded,r=>{if(r.ReadInt32()!=1)throw new InvalidDataException("Owner migration version.");int count=r.ReadInt32();if(count<0||count>MaximumEntries)throw new InvalidDataException("Owner migration count.");var values=new List<Entry>();for(int i=0;i<count;i++)values.Add(new Entry{Metadata=Blob(r,TransferWire.Maximum),Before=Blob(r,MaximumDecoded),After=Blob(r,MaximumDecoded)});return values;});
            if(list.Select(e=>Source(ReadMetadata(e.Metadata).Request)).Distinct(StringComparer.Ordinal).Count()!=list.Count)throw new InvalidDataException("Duplicate owner migration identity.");return list;
        }
        static byte[] Write(int bound,Action<BinaryWriter> write){using(var memory=new MemoryStream()){using(var writer=new BinaryWriter(memory,Encoding.UTF8,true))write(writer);if(memory.Length>bound)throw new InvalidDataException("Owner decision exceeds bound.");return memory.ToArray();}}
        static T Read<T>(byte[] bytes,int bound,Func<BinaryReader,T> read){if(bytes==null||bytes.Length>bound)throw new InvalidDataException("Owner decision exceeds bound.");try{using(var memory=new MemoryStream(bytes,false))using(var reader=new BinaryReader(memory,Encoding.UTF8)){var result=read(reader);if(memory.Position!=memory.Length)throw new InvalidDataException("Owner decision trailing bytes.");return result;}}catch(EndOfStreamException error){throw new InvalidDataException("Truncated owner decision.",error);}}
        static void Blob(BinaryWriter writer,byte[] bytes){if(bytes==null||bytes.Length>MaximumDecoded||writer.BaseStream.Position+4L+bytes.Length>MaximumDecoded)throw new InvalidDataException("Owner image exceeds bound.");writer.Write(bytes.Length);writer.Write(bytes);}
        static byte[] Blob(BinaryReader reader,int bound){int size=reader.ReadInt32();if(size<0||size>bound||size>reader.BaseStream.Length-reader.BaseStream.Position)throw new InvalidDataException("Owner image exceeds bound.");return reader.ReadBytes(size);}
    }
}
