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
        byte[] before,after;readonly byte[] metadata;
        public byte[] Before=>(byte[])before.Clone();public byte[] After=>(byte[])after.Clone();
        public byte[] Metadata=>(byte[])metadata.Clone();
        internal InventoryDecisionRecord(string id,string source,string fingerprint,double deadline,byte[] before,byte[] after,InventoryDecision state,byte[]? metadata=null){Id=id;Source=source;Fingerprint=fingerprint;Deadline=deadline;this.before=(byte[])before.Clone();this.after=(byte[])after.Clone();this.metadata=metadata==null?Array.Empty<byte>():(byte[])metadata.Clone();State=state;}
        internal void Rebase(byte[] before,byte[] after){this.before=(byte[])before.Clone();this.after=(byte[])after.Clone();}
    }
    /// <summary>Owned local write-ahead journal. A complete corrupt record fails closed;
    /// an incomplete final frame is cut back to the last flushed frame before admission.</summary>
    public sealed class InventoryDecisionJournal:IDisposable {
        const int Magic=0x42575231,MaxFrame=4194304;const long MaxFile=268435456;
        readonly Dictionary<string,InventoryDecisionRecord> records=new Dictionary<string,InventoryDecisionRecord>(StringComparer.Ordinal);
        // Finished records moved out by ArchiveSettled: their identity and final state stay answerable (a replayed or
        // recovering request still hears Committed or Aborted), but they no longer count against capacity.
        readonly Dictionary<string,(string Fingerprint,InventoryDecision State)> settled=new Dictionary<string,(string,InventoryDecision)>(StringComparer.Ordinal);
        readonly List<(string Id,long Start,long Length)> frames=new List<(string,long,long)>();
        readonly FileStream stream;readonly int capacity;bool disposed,faulted;
        public InventoryDecisionJournal(string path,int capacity=4096){
            if(capacity<1||capacity>4096)throw new ArgumentOutOfRangeException(nameof(capacity));this.capacity=capacity;
            var directory=Path.GetDirectoryName(Path.GetFullPath(path));Directory.CreateDirectory(directory!);
            stream=new FileStream(path,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.Read);
            try{Read();ReadSettled(path+SettledSuffix);}catch{stream.Dispose();throw;}
        }
        public InventoryDecisionRecord? Find(string id)=>records.TryGetValue(id,out var record)?record:null;
        /// <summary>An archived finished record's fingerprint and final state (Committed or Aborted).</summary>
        public bool TryGetSettled(string id,out string fingerprint,out InventoryDecision state){
            if(!records.ContainsKey(id)&&settled.TryGetValue(id,out var value)){fingerprint=value.Fingerprint;state=value.State;return true;}
            fingerprint="";state=InventoryDecision.Rejected;return false;
        }
        public IEnumerable<string> SettledIds=>settled.Keys;
        public IReadOnlyList<InventoryDecisionRecord> Snapshot()=>Array.AsReadOnly(records.Values.ToArray());
        public bool CanAdmit=>!disposed&&!faulted&&records.Count<capacity&&stream.Length<MaxFile-MaxFrame;
        public InventoryDecisionRecord Prepare(string id,string source,string fingerprint,double deadline,byte[] before,byte[] after,byte[]? metadata=null){
            Text(id);Text(source);Text(fingerprint);
            if(!CanAdmit||records.ContainsKey(id)||settled.ContainsKey(id)||double.IsNaN(deadline)||double.IsInfinity(deadline)||before==null||after==null||before.Length>MaxFrame/2-16384||after.Length>MaxFrame/2-16384||(metadata?.Length??0)>16384)throw new InvalidOperationException("Journal admission denied.");
            var record=new InventoryDecisionRecord(id,source,fingerprint,deadline,before,after,InventoryDecision.Prepared,metadata);Append(record);records.Add(id,record);return record;
        }
        public void Transition(InventoryDecisionRecord record,InventoryDecision next){
            if(disposed||faulted||!records.TryGetValue(record.Id,out var existing)||!ReferenceEquals(record,existing))throw new InvalidOperationException("Inactive journal record.");
            if(!Allowed(record.State,next))throw new InvalidOperationException("Invalid durable inventory transition.");
            if(next==record.State)return;
            var changed=new InventoryDecisionRecord(record.Id,record.Source,record.Fingerprint,record.Deadline,record.Before,record.After,next,record.Metadata);
            Append(changed);record.State=next;
        }
        /// <summary>The decision of a hold whose payment was moved onto the live source (<see cref="IInventoryImageRebase"/>):
        /// the Decided frame carries the rebased images, which replace the reserved ones. Only a Prepared hold can be rebased.</summary>
        public void Decide(InventoryDecisionRecord record,byte[] before,byte[] after){
            if(disposed||faulted||!records.TryGetValue(record.Id,out var existing)||!ReferenceEquals(record,existing))throw new InvalidOperationException("Inactive journal record.");
            if(record.State!=InventoryDecision.Prepared||before==null||after==null||before.Length>MaxFrame/2-16384||after.Length>MaxFrame/2-16384)throw new InvalidOperationException("Invalid durable inventory transition.");
            var changed=new InventoryDecisionRecord(record.Id,record.Source,record.Fingerprint,record.Deadline,before,after,InventoryDecision.Decided,record.Metadata);
            Append(changed);record.Rebase(before,after);record.State=InventoryDecision.Decided;
        }
        // Decided → Aborted is written only for a chest salvage whose removal never landed (DurableInventoryParticipant).
        static bool Allowed(InventoryDecision old,InventoryDecision next)=>old==next||old==InventoryDecision.Prepared&&(next==InventoryDecision.Decided||next==InventoryDecision.Aborted)||old==InventoryDecision.Decided&&(next==InventoryDecision.Committed||next==InventoryDecision.Aborted);
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
                        bool rebased=previous.State==InventoryDecision.Prepared&&record.State==InventoryDecision.Decided;
                        if(record.Source!=previous.Source||record.Fingerprint!=previous.Fingerprint||record.Deadline!=previous.Deadline||!rebased&&(!record.Before.SequenceEqual(previous.Before)||!record.After.SequenceEqual(previous.After))||!record.Metadata.SequenceEqual(previous.Metadata)||!Allowed(previous.State,record.State))throw new InvalidDataException("Conflicting journal history.");
                    }else if(records.Count>=capacity||record.State!=InventoryDecision.Prepared)throw new InvalidDataException("Invalid journal admission history.");
                    records[record.Id]=record;frames.Add((record.Id,complete,stream.Position-complete));complete=stream.Position;
                }
            }
            stream.Position=stream.Length;
        }
        public const string SettledSuffix=".settled",ArchiveMarkerSuffix=".archive-1.done",DurableSuffix=".durable";
        static string Field(string value)=>Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
        static string Unfield(string value)=>Encoding.UTF8.GetString(Convert.FromBase64String(value));
        /// <summary>One line per archived record. A final line without its newline (an interrupted append) is ignored; the
        /// record is still in the journal then, and the next archive writes the line again. Any other malformed line fails closed.</summary>
        void ReadSettled(string path){
            if(!File.Exists(path))return;
            string text=File.ReadAllText(path,Encoding.UTF8);int end=text.LastIndexOf('\n');if(end<0)return;
            foreach(var raw in text.Substring(0,end).Split('\n')){
                if(raw.Length==0)continue;
                var parts=raw.Split('\t');
                if(parts.Length!=4||parts[0]!="S1")throw new InvalidDataException("Invalid settled record index.");
                string id,fingerprint;try{id=Unfield(parts[1]);fingerprint=Unfield(parts[2]);}catch(FormatException error){throw new InvalidDataException("Invalid settled record index.",error);}
                Text(id);Text(fingerprint);
                if(!int.TryParse(parts[3],System.Globalization.NumberStyles.None,System.Globalization.CultureInfo.InvariantCulture,out int state)||state!=(int)InventoryDecision.Committed&&state!=(int)InventoryDecision.Aborted)throw new InvalidDataException("Invalid settled record state.");
                settled[id]=(fingerprint,(InventoryDecision)state);
            }
        }
        /// <summary>Ids of Committed records whose receipts were seen again in a later game run (the D1 lines of
        /// <c>&lt;journal&gt;.durable</c>). A final line without its newline is ignored; another malformed line fails closed.</summary>
        public static HashSet<string> ReadDurable(string path){
            var ids=new HashSet<string>(StringComparer.Ordinal);var file=path+DurableSuffix;
            if(!File.Exists(file))return ids;
            string text=File.ReadAllText(file,Encoding.UTF8);int end=text.LastIndexOf('\n');if(end<0)return ids;
            foreach(var raw in text.Substring(0,end).Split('\n')){
                if(raw.Length==0)continue;
                var parts=raw.Split('\t');
                if(parts.Length!=2||parts[0]!="D1")throw new InvalidDataException("Invalid durable receipt list.");
                string id;try{id=Unfield(parts[1]);}catch(FormatException error){throw new InvalidDataException("Invalid durable receipt list.",error);}
                Text(id);ids.Add(id);
            }
            return ids;
        }
        /// <summary>Appends ids to <c>&lt;journal&gt;.durable</c> and flushes; an interrupted earlier fragment is cut first.</summary>
        public static void AppendDurable(string path,IEnumerable<string> ids){
            var lines=new StringBuilder();foreach(var id in ids){Text(id);lines.Append("D1\t").Append(Field(id)).Append('\n');}
            if(lines.Length==0)return;
            CutTornLine(path+DurableSuffix);
            using(var file=new FileStream(path+DurableSuffix,FileMode.Append,FileAccess.Write,FileShare.Read)){var bytes=Encoding.UTF8.GetBytes(lines.ToString());file.Write(bytes,0,bytes.Length);file.Flush(true);}
        }
        static void CutTornLine(string file){
            if(!File.Exists(file))return;
            var old=File.ReadAllBytes(file);
            if(old.Length>0&&old[old.Length-1]!=(byte)'\n'){int keep=Array.LastIndexOf(old,(byte)'\n')+1;using(var cut=new FileStream(file,FileMode.Open,FileAccess.Write,FileShare.None)){cut.SetLength(keep);cut.Flush(true);}}
        }
        /// <summary>
        /// Moves finished records out of the journal at <paramref name="path"/>, which must not be open. Aborted records changed
        /// nothing and always move. A Committed record moves only when <paramref name="durable"/> says its receipt was confirmed
        /// after a reload: until then a lost world save can still roll its chest back, and recovery must be able to apply the
        /// payment again from this journal.
        /// The first time for a journal its whole file is copied to backups/&lt;UTC time&gt;/ and a marker is written; nothing
        /// moves unless that copy is complete and identical. The finished records' exact frames go to archive/&lt;name&gt;.&lt;ticks&gt;.bin
        /// and their identity and final state to the settled index, both flushed, before the journal is replaced by one holding
        /// only the unfinished records' frames, byte for byte. Nothing is ever deleted. Returns how many records moved.
        /// </summary>
        public static int ArchiveSettled(string path,Func<DateTime>? clock=null,Func<string,bool>? durable=null){
            var now=(clock??(()=>DateTime.UtcNow))();
            if(!File.Exists(path))return 0;
            List<(string Id,long Start,long Length)> frames;
            var finished=new Dictionary<string,(string Fingerprint,InventoryDecision State)>(StringComparer.Ordinal);
            // Opening validates every frame and cuts an interrupted final frame, exactly as a normal open does.
            using(var journal=new InventoryDecisionJournal(path)){
                frames=new List<(string,long,long)>(journal.frames);
                foreach(var record in journal.records.Values)
                    if(record.State==InventoryDecision.Aborted||record.State==InventoryDecision.Committed&&durable!=null&&durable(record.Id))finished[record.Id]=(record.Fingerprint,record.State);
            }
            if(finished.Count==0)return 0;
            var full=Path.GetFullPath(path);var directory=Path.GetDirectoryName(full)!;var name=Path.GetFileName(full);
            byte[] bytes=File.ReadAllBytes(full);
            string stamp=now.Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if(!File.Exists(full+ArchiveMarkerSuffix)){
                var backups=Path.Combine(directory,"backups",now.ToString("yyyyMMdd-HHmmss",System.Globalization.CultureInfo.InvariantCulture));Directory.CreateDirectory(backups);
                var copy=Path.Combine(backups,name);if(File.Exists(copy))copy+="."+stamp;
                using(var file=new FileStream(copy,FileMode.CreateNew,FileAccess.Write,FileShare.None)){file.Write(bytes,0,bytes.Length);file.Flush(true);}
                if(!File.ReadAllBytes(copy).SequenceEqual(bytes))throw new IOException("Journal backup does not match; nothing archived.");
                File.WriteAllText(full+ArchiveMarkerSuffix,copy+"\n",Encoding.UTF8);
            }
            byte[] Frames(bool unfinished){using(var memory=new MemoryStream()){foreach(var frame in frames)if(finished.ContainsKey(frame.Id)!=unfinished)memory.Write(bytes,(int)frame.Start,(int)frame.Length);return memory.ToArray();}}
            var archive=Path.Combine(directory,"archive");Directory.CreateDirectory(archive);
            var moved=Path.Combine(archive,name+"."+stamp+".bin");
            for(int n=1;File.Exists(moved);n++)moved=Path.Combine(archive,name+"."+stamp+"-"+n.ToString(System.Globalization.CultureInfo.InvariantCulture)+".bin");
            var archived=Frames(false);
            using(var file=new FileStream(moved,FileMode.CreateNew,FileAccess.Write,FileShare.None)){file.Write(archived,0,archived.Length);file.Flush(true);}
            var lines=new StringBuilder();
            var indexPath=full+SettledSuffix;
            // An interrupted earlier append may have left a final line without its newline. That fragment was never read (its
            // record is still in the journal and is written again here), so it is cut before appending.
            if(File.Exists(indexPath)){
                var old=File.ReadAllBytes(indexPath);
                if(old.Length>0&&old[old.Length-1]!=(byte)'\n'){int keep=Array.LastIndexOf(old,(byte)'\n')+1;using(var cut=new FileStream(indexPath,FileMode.Open,FileAccess.Write,FileShare.None)){cut.SetLength(keep);cut.Flush(true);}}
            }
            foreach(var pair in finished)lines.Append("S1\t").Append(Field(pair.Key)).Append('\t').Append(Field(pair.Value.Fingerprint)).Append('\t').Append(((int)pair.Value.State).ToString(System.Globalization.CultureInfo.InvariantCulture)).Append('\n');
            using(var index=new FileStream(indexPath,FileMode.Append,FileAccess.Write,FileShare.Read)){var text=Encoding.UTF8.GetBytes(lines.ToString());index.Write(text,0,text.Length);index.Flush(true);}
            var kept=Frames(true);var temporary=full+".compact";
            using(var file=new FileStream(temporary,FileMode.Create,FileAccess.Write,FileShare.None)){file.Write(kept,0,kept.Length);file.Flush(true);}
            File.Replace(temporary,full,null);
            return finished.Count;
        }
        void Truncate(long complete){stream.SetLength(complete);stream.Position=complete;stream.Flush(true);}
        public void Dispose(){if(disposed)return;disposed=true;stream.Dispose();}
    }
}
