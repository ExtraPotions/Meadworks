using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using ExtraPotions.Meadworks.Contracts;
using Newtonsoft.Json;

namespace ExtraPotions.Wildroot {
    public enum PlacementDecisionState { None,Commit,Abort }
    public enum PlacementPresence { Unavailable,Present,ProvenMissing,Removed,Ambiguous }
    public sealed class PlacementCellRecord {
        public string RequestId{get;set;}="";
        public string RecoveryData{get;set;}="";
        public string NativeId{get;set;}="";
        public List<ResourceCost> Costs{get;set;}=new List<ResourceCost>();
        public bool Created{get;set;}
        public bool Settled{get;set;}
        public bool Retired{get;set;}
    }
    public sealed class PlacementJournalRecord {
        public int Schema{get;set;}=1;
        public string ProviderId{get;set;}="";
        public string ActorId{get;set;}="";
        public long WorldId{get;set;}
        public string SessionId{get;set;}="";
        public string RequestId{get;set;}="";
        public List<ResourceCost> Costs{get;set;}=new List<ResourceCost>();
        public List<PlacementCellRecord> Cells{get;set;}=new List<PlacementCellRecord>();
        public PlacementDecisionState Decision{get;set;}
        public AsyncReservationState Payment{get;set;}=AsyncReservationState.Pending;
        public bool Cancelled{get;set;}
        public bool Completed{get;set;}
        public PlacementResultKind AbortKind{get;set;}=PlacementResultKind.Cancelled;
        [JsonIgnore]public string Key=>Hash(ProviderId+"\n"+ActorId+"\n"+WorldId+"\n"+SessionId+"\n"+RequestId);
        [JsonIgnore]public AsyncResourceContext Context=>new AsyncResourceContext(ActorId,WorldId,SessionId,RequestId);
        internal static string Hash(string value){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-","").ToLowerInvariant();}
        public void Validate(){
            if(Schema!=1||string.IsNullOrWhiteSpace(ProviderId)||ProviderId.Length>512||Cells==null||Cells.Count==0||Cells.Count>10000||Costs==null||Costs.Any(c=>c==null)||!Enum.IsDefined(typeof(PlacementDecisionState),Decision)||!Enum.IsDefined(typeof(AsyncReservationState),Payment))throw new InvalidDataException("Invalid placement journal.");
            _=Context;
            if(Cells.Any(c=>c==null||string.IsNullOrWhiteSpace(c.RequestId)||c.RequestId.Length>128||c.RecoveryData==null||c.RecoveryData.Length>16384||c.NativeId==null||c.Costs==null||c.Costs.Any(cost=>cost==null))||Cells.Select(c=>c.RequestId).Distinct(StringComparer.Ordinal).Count()!=Cells.Count)throw new InvalidDataException("Invalid placement cells.");
        }
    }
    public interface IPlacementJournal {void Save(PlacementJournalRecord record);IReadOnlyList<PlacementJournalRecord> Load();}
    public interface IDurablePlacementHandle:IConfirmedPlacementHandle {string NativeId{get;}}
    public interface IDurablePlacementBackend:IPlacementBackend {string RecoveryData{get;}PlacementProbe Probe(PlacementCellRecord cell);}
    public sealed class PlacementProbe {
        public PlacementPresence Presence{get;}public IPlacementHandle? Handle{get;}
        public PlacementProbe(PlacementPresence presence,IPlacementHandle? handle=null){Presence=presence;Handle=handle;}
    }
    /// <summary>Atomic replacement and fsync precede a network/native side effect.
    /// Corrupt records fail closed; an older backup is never current lifecycle proof.</summary>
    public sealed class FilePlacementJournal:IPlacementJournal {
        readonly string directory;
        public FilePlacementJournal(string directory){this.directory=Path.GetFullPath(directory??throw new ArgumentNullException(nameof(directory)));}
        sealed class Envelope {public string Payload{get;set;}="";public string SHA256{get;set;}="";}
        public void Save(PlacementJournalRecord record){
            record.Validate();Directory.CreateDirectory(directory);string path=Path.Combine(directory,record.Key+".json"),pending=path+".pending";
            string payload=JsonConvert.SerializeObject(record);var bytes=Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new Envelope{Payload=payload,SHA256=PlacementJournalRecord.Hash(payload)}));
            using(var stream=new FileStream(pending,FileMode.Create,FileAccess.Write,FileShare.None)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
            if(File.Exists(path))File.Replace(pending,path,null);else File.Move(pending,path);
        }
        public IReadOnlyList<PlacementJournalRecord> Load(){
            if(!Directory.Exists(directory))return Array.Empty<PlacementJournalRecord>();var records=new List<PlacementJournalRecord>();
            foreach(var path in Directory.EnumerateFiles(directory,"*.json").OrderBy(p=>p,StringComparer.Ordinal))records.Add(Read(path));
            return records.AsReadOnly();
        }
        static PlacementJournalRecord Read(string path){
            var envelope=JsonConvert.DeserializeObject<Envelope>(File.ReadAllText(path))??throw new InvalidDataException("Empty placement journal.");
            if(envelope.SHA256!=PlacementJournalRecord.Hash(envelope.Payload))throw new InvalidDataException("Placement journal checksum mismatch.");
            var record=JsonConvert.DeserializeObject<PlacementJournalRecord>(envelope.Payload)??throw new InvalidDataException("Empty placement record.");record.Validate();
            if(Path.GetFileNameWithoutExtension(path)!=record.Key)throw new InvalidDataException("Placement journal identity mismatch.");
            return record;
        }
        /// <summary>Folder that keeps records recovery cannot use. They are moved there, never deleted.</summary>
        public string RetiredDirectory=>Path.Combine(directory,"retired");
        /// <summary>Like <see cref="Load"/>, but a record that cannot be read is moved to <see cref="RetiredDirectory"/>
        /// and named in <paramref name="retired"/> instead of hiding every other record.</summary>
        public IReadOnlyList<PlacementJournalRecord> LoadRecoverable(out IReadOnlyList<string> retired){
            var moved=new List<string>();retired=moved;
            if(!Directory.Exists(directory))return Array.Empty<PlacementJournalRecord>();var records=new List<PlacementJournalRecord>();
            foreach(var path in Directory.EnumerateFiles(directory,"*.json").OrderBy(p=>p,StringComparer.Ordinal)){
                try{records.Add(Read(path));}
                catch(Exception error)when(error is InvalidDataException||error is JsonException||error is IOException){if(RetireFile(path))moved.Add(Path.GetFileName(path));}
            }
            return records.AsReadOnly();
        }
        /// <summary>Moves one record that recovery cannot use out of the active journal. True when it moved.</summary>
        public bool Retire(PlacementJournalRecord record)=>record!=null&&RetireFile(Path.Combine(directory,record.Key+".json"));
        bool RetireFile(string path)=>MoveFile(path,RetiredDirectory);
        static bool MoveFile(string path,string folder){
            try{
                if(!File.Exists(path))return false;Directory.CreateDirectory(folder);
                string target=Path.Combine(folder,Path.GetFileName(path));
                if(File.Exists(target))target=Path.Combine(folder,Path.GetFileNameWithoutExtension(path)+"."+DateTime.UtcNow.Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture)+".json");
                File.Move(path,target);return true;
            }catch(Exception error)when(error is IOException||error is UnauthorizedAccessException){return false;}
        }
        /// <summary>Folder that keeps finished records. A Completed record has a terminal outcome, so recovery never reads it again.</summary>
        public string ArchiveDirectory=>Path.Combine(directory,"archive");
        /// <summary>Written once the pre-archive backup of the legacy journal exists.</summary>
        public string MigrationMarker=>Path.Combine(directory,"archive-migration-1.done");
        public string BackupRoot=>Path.Combine(directory,"backups");
        /// <summary>Like <see cref="LoadRecoverable"/>, but returns only unfinished records. Completed records are moved to
        /// <see cref="ArchiveDirectory"/> (never deleted). The first run copies every record to a timestamped folder under
        /// <see cref="BackupRoot"/> and writes <see cref="MigrationMarker"/>; if that backup fails, nothing is archived this time.</summary>
        public IReadOnlyList<PlacementJournalRecord> LoadActive(out IReadOnlyList<string> retired,out IReadOnlyList<string> archived,out string? backup){
            var moved=new List<string>();archived=moved;backup=null;
            if(!Directory.Exists(directory)){retired=Array.Empty<string>();return Array.Empty<PlacementJournalRecord>();}
            bool migrated=File.Exists(MigrationMarker);
            if(!migrated){
                try{
                    var files=Directory.GetFiles(directory,"*.json");
                    if(files.Length>0){
                        string stamp=DateTime.UtcNow.ToString("yyyyMMdd-HHmmss",System.Globalization.CultureInfo.InvariantCulture),target=Path.Combine(BackupRoot,stamp);
                        for(int n=1;Directory.Exists(target);n++)target=Path.Combine(BackupRoot,stamp+"-"+n.ToString(System.Globalization.CultureInfo.InvariantCulture));
                        Directory.CreateDirectory(target);foreach(var file in files)File.Copy(file,Path.Combine(target,Path.GetFileName(file)),false);backup=target;
                    }
                    File.WriteAllText(MigrationMarker,DateTime.UtcNow.ToString("o",System.Globalization.CultureInfo.InvariantCulture)+"\n"+files.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)+" record(s) backed up"+(backup==null?"":" to "+backup)+"\n");migrated=true;
                }catch(Exception error)when(error is IOException||error is UnauthorizedAccessException){backup=null;}
            }
            var active=new List<PlacementJournalRecord>();
            foreach(var record in LoadRecoverable(out retired)){
                if(!record.Completed){active.Add(record);continue;}
                if(migrated&&MoveFile(Path.Combine(directory,record.Key+".json"),ArchiveDirectory))moved.Add(record.Key+".json");
            }
            return active.AsReadOnly();
        }
    }
}
