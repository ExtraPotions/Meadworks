using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace ExtraPotions.Burrow {
    public enum InventoryDecision { Rejected,Prepared,Decided,Committed,Aborted,RecoveryRequired }
    public sealed class InventoryDecisionRecord {
        public string Id{get;}public string Source{get;}public string Fingerprint{get;}public double Deadline{get;}
        public InventoryDecision State{get;internal set;}
        readonly byte[] before,after,metadata;
        public byte[] Before=>(byte[])before.Clone();public byte[] After=>(byte[])after.Clone();
        public byte[] Metadata=>(byte[])metadata.Clone();
        internal InventoryDecisionRecord(string id,string source,string fingerprint,double deadline,byte[] before,byte[] after,InventoryDecision state,byte[]? metadata=null){Id=id;Source=source;Fingerprint=fingerprint;Deadline=deadline;this.before=(byte[])before.Clone();this.after=(byte[])after.Clone();this.metadata=metadata==null?Array.Empty<byte>():(byte[])metadata.Clone();State=state;}
    }
    /// <summary>Owned local write-ahead journal. A complete corrupt record fails closed;
    /// an incomplete final frame is cut back to the last flushed frame before admission.</summary>
    public sealed class InventoryDecisionJournal:IDisposable {
        const int Magic=0x42575231,MaxFrame=4194304;const long MaxFile=268435456;
        readonly Dictionary<string,InventoryDecisionRecord> records=new Dictionary<string,InventoryDecisionRecord>(StringComparer.Ordinal);
        readonly FileStream stream;readonly int capacity;bool disposed,faulted;
        public InventoryDecisionJournal(string path,int capacity=4096){
            if(capacity<1||capacity>4096)throw new ArgumentOutOfRangeException(nameof(capacity));this.capacity=capacity;
            var directory=Path.GetDirectoryName(Path.GetFullPath(path));Directory.CreateDirectory(directory!);
            stream=new FileStream(path,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.Read);
            try{Read();}catch{stream.Dispose();throw;}
        }
        public InventoryDecisionRecord? Find(string id)=>records.TryGetValue(id,out var record)?record:null;
        public IReadOnlyList<InventoryDecisionRecord> Snapshot()=>Array.AsReadOnly(records.Values.ToArray());
        public bool CanAdmit=>!disposed&&!faulted&&records.Count<capacity&&stream.Length<MaxFile-MaxFrame;
        public InventoryDecisionRecord Prepare(string id,string source,string fingerprint,double deadline,byte[] before,byte[] after,byte[]? metadata=null){
            Text(id);Text(source);Text(fingerprint);
            if(!CanAdmit||records.ContainsKey(id)||double.IsNaN(deadline)||double.IsInfinity(deadline)||before==null||after==null||before.Length>MaxFrame/2-16384||after.Length>MaxFrame/2-16384||(metadata?.Length??0)>16384)throw new InvalidOperationException("Journal admission denied.");
            var record=new InventoryDecisionRecord(id,source,fingerprint,deadline,before,after,InventoryDecision.Prepared,metadata);Append(record);records.Add(id,record);return record;
        }
        public void Transition(InventoryDecisionRecord record,InventoryDecision next){
            if(disposed||faulted||!records.TryGetValue(record.Id,out var existing)||!ReferenceEquals(record,existing))throw new InvalidOperationException("Inactive journal record.");
            if(!Allowed(record.State,next))throw new InvalidOperationException("Invalid durable inventory transition.");
            if(next==record.State)return;
            var changed=new InventoryDecisionRecord(record.Id,record.Source,record.Fingerprint,record.Deadline,record.Before,record.After,next,record.Metadata);
            Append(changed);record.State=next;
        }
        static bool Allowed(InventoryDecision old,InventoryDecision next)=>old==next||old==InventoryDecision.Prepared&&(next==InventoryDecision.Decided||next==InventoryDecision.Aborted)||old==InventoryDecision.Decided&&next==InventoryDecision.Committed;
        static void Text(string value){if(string.IsNullOrWhiteSpace(value)||value.Length>512||value.IndexOfAny(new[]{'\0','\r','\n'})>=0)throw new InvalidDataException("Invalid journal identity.");}
        void Append(InventoryDecisionRecord record){
            try{
                using(var bytes=new MemoryStream()){
                    using(var writer=new BinaryWriter(bytes,Encoding.UTF8,true)){writer.Write(2);writer.Write(record.Id);writer.Write(record.Source);writer.Write(record.Fingerprint);writer.Write(record.Deadline);writer.Write((int)record.State);WriteImage(writer,record.Before);WriteImage(writer,record.After);WriteImage(writer,record.Metadata);}
                    byte[] body=bytes.ToArray();if(body.Length>MaxFrame||stream.Length+body.Length+40>MaxFile)throw new IOException("Journal storage capacity reached.");
                    using(var writer=new BinaryWriter(stream,Encoding.UTF8,true)){writer.Write(Magic);writer.Write(body.Length);writer.Write(body);using(var sha=SHA256.Create())writer.Write(sha.ComputeHash(body));writer.Flush();}
                    stream.Flush(true);
                }
            }catch{faulted=true;throw;}
        }
        static void WriteImage(BinaryWriter writer,byte[] image){writer.Write(image.Length);writer.Write(image);}
        static byte[] ReadImage(BinaryReader reader){int length=reader.ReadInt32();if(length<0||length>MaxFrame/2-1024||length>reader.BaseStream.Length-reader.BaseStream.Position)throw new InvalidDataException("Invalid inventory image.");return reader.ReadBytes(length);}
        void Read(){
            if(stream.Length>MaxFile)throw new InvalidDataException("Journal too large.");long complete=0;
            using(var reader=new BinaryReader(stream,Encoding.UTF8,true)){
                while(stream.Position<stream.Length){
                    if(stream.Length-stream.Position<8){Truncate(complete);break;}
                    if(reader.ReadInt32()!=Magic)throw new InvalidDataException("Journal frame signature mismatch.");
                    int length=reader.ReadInt32();if(length<0||length>MaxFrame)throw new InvalidDataException("Journal frame exceeds bounds.");
                    if(stream.Length-stream.Position<length+32L){Truncate(complete);break;}
                    byte[] body=reader.ReadBytes(length),digest=reader.ReadBytes(32);
                    using(var sha=SHA256.Create())if(!digest.SequenceEqual(sha.ComputeHash(body)))throw new InvalidDataException("Journal frame checksum mismatch.");
                    InventoryDecisionRecord record;
                    try{using(var bytes=new MemoryStream(body))using(var entry=new BinaryReader(bytes,Encoding.UTF8)){
                        int version=entry.ReadInt32();if(version!=1&&version!=2)throw new InvalidDataException("Unknown journal version.");
                        string id=entry.ReadString(),source=entry.ReadString(),fingerprint=entry.ReadString();Text(id);Text(source);Text(fingerprint);
                        double deadline=entry.ReadDouble();if(double.IsNaN(deadline)||double.IsInfinity(deadline))throw new InvalidDataException("Invalid deadline.");
                        var state=(InventoryDecision)entry.ReadInt32();if(state!=InventoryDecision.Prepared&&state!=InventoryDecision.Decided&&state!=InventoryDecision.Committed&&state!=InventoryDecision.Aborted)throw new InvalidDataException("Invalid decision state.");
                        var before=ReadImage(entry);var after=ReadImage(entry);var metadata=version==2?ReadImage(entry):Array.Empty<byte>();if(metadata.Length>16384)throw new InvalidDataException("Journal metadata exceeds bounds.");record=new InventoryDecisionRecord(id,source,fingerprint,deadline,before,after,state,metadata);if(bytes.Position!=bytes.Length)throw new InvalidDataException("Journal record has trailing bytes.");
                    }}catch(EndOfStreamException error){throw new InvalidDataException("Invalid complete journal frame.",error);}
                    if(records.TryGetValue(record.Id,out var previous)){
                        if(record.Source!=previous.Source||record.Fingerprint!=previous.Fingerprint||record.Deadline!=previous.Deadline||!record.Before.SequenceEqual(previous.Before)||!record.After.SequenceEqual(previous.After)||!record.Metadata.SequenceEqual(previous.Metadata)||!Allowed(previous.State,record.State))throw new InvalidDataException("Conflicting journal history.");
                    }else if(records.Count>=capacity||record.State!=InventoryDecision.Prepared)throw new InvalidDataException("Invalid journal admission history.");
                    records[record.Id]=record;complete=stream.Position;
                }
            }
            stream.Position=stream.Length;
        }
        void Truncate(long complete){stream.SetLength(complete);stream.Position=complete;stream.Flush(true);}
        public void Dispose(){if(disposed)return;disposed=true;stream.Dispose();}
    }
}
