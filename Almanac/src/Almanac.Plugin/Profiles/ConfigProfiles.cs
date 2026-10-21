// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using BepInEx.Configuration;
using ExtraPotions.Almanac.Attributes;
using ExtraPotions.Almanac.Editors;
using ExtraPotions.Almanac.Index;
using ExtraPotions.Almanac.Runtime.Discovery;
using ExtraPotions.Almanac.Runtime.Editing;
using ExtraPotions.Almanac.Writing;
using ExtraPotions.Meadworks.Crucible;

namespace ExtraPotions.Almanac.Runtime.Profiles
{
    public sealed class ProfileSelection
    {
        public Func<IndexedFile,bool>? File { get; set; }
        public Func<IndexedFile,ConfigEntryBase,bool>? Entry { get; set; }
        public bool IncludeKeybinds { get; set; }=true;
        public bool IncludeAdvanced { get; set; }=true;
        public bool OnlyChangedFromDefault { get; set; }
    }
    public sealed class ProfileValue
    {
        internal ProfileValue(string plugin,string version,string file,string section,string key,string type,string value)
        {Plugin=plugin;PluginVersion=version;File=file;Section=section;Key=key;Type=type;Value=value;}
        public string Plugin { get; }
        public string PluginVersion { get; }
        // Relative logical identity only; this is matched against discovery, never used as a write path.
        public string File { get; }
        public string Section { get; }
        public string Key { get; }
        public string Type { get; }
        public string Value { get; }
    }
    public sealed class ConfigProfile
    {
        internal ConfigProfile(string name,string version,string build,IEnumerable<ProfileValue> values,bool defaults=false)
        {Name=name;AlmanacVersion=version;GameBuild=build;Values=Array.AsReadOnly(values.ToArray());IsDefaults=defaults;}
        public string Name { get; }
        public string AlmanacVersion { get; }
        public string GameBuild { get; }
        public IReadOnlyList<ProfileValue> Values { get; }
        public bool IsDefaults { get; }
    }
    public enum ProfileOutcome { Apply, Same, Locked, MissingPlugin, UnknownKey, TypeChanged, InvalidValue }
    public sealed class ProfilePreviewRow
    {
        internal ProfilePreviewRow(ProfileValue value,ProfileOutcome outcome,string? reason=null,ConfigEntryBase? entry=null,
            string? current=null,string? input=null,string? target=null,bool restart=false,bool clamped=false)
        {Value=value;Outcome=outcome;Reason=reason;Entry=entry;Current=current;Input=input;Target=target;RequiresRestart=restart;WasClamped=clamped;}
        public ProfileValue Value { get; }
        public ProfileOutcome Outcome { get; }
        public string? Reason { get; }
        public string? Current { get; }
        public string? Target { get; }
        public bool RequiresRestart { get; }
        public bool WasClamped { get; }
        internal ConfigEntryBase? Entry { get; }
        internal string? Input { get; }
    }
    public sealed class ProfilePreview
    {
        internal ProfilePreview(ConfigProfiles owner,ConfigProfile profile,IEnumerable<ProfilePreviewRow> rows)
        {Owner=owner;Profile=profile;Rows=Array.AsReadOnly(rows.ToArray());}
        internal ConfigProfiles Owner { get; }
        public ConfigProfile Profile { get; }
        public IReadOnlyList<ProfilePreviewRow> Rows { get; }
    }
    public sealed class ProfileApplyResult
    {
        internal ProfileApplyResult(bool success,int applied,int skipped,IEnumerable<string> backups,string? error=null,IEnumerable<string>? rollbackErrors=null)
        {Success=success;Applied=applied;Skipped=skipped;Backups=Array.AsReadOnly(backups.ToArray());Error=error;RollbackErrors=Array.AsReadOnly((rollbackErrors??Array.Empty<string>()).ToArray());}
        public bool Success { get; }
        public int Applied { get; }
        public int Skipped { get; }
        public IReadOnlyList<string> Backups { get; }
        public string? Error { get; }
        public IReadOnlyList<string> RollbackErrors { get; }
    }

    /// <summary>A10: local profile storage, live preview, and one guarded undo group. No config text edits.</summary>
    public sealed class ConfigProfiles
    {
        const int MaxBytes=1024*1024,MaxEntries=8192,MaxString=65536;
        readonly ConfigDiscovery discovery;
        readonly EntryWriter writer;
        readonly Func<ConfigEntryBase,NativeWriteFacts> resolve;
        readonly Func<ConfigEntryBase,bool>? hidden;
        readonly Func<int> backupCount;
        readonly Action<string>? warning;
        readonly string configRoot,version,build;
        readonly int thread=Thread.CurrentThread.ManagedThreadId;
        readonly ProfileFiles files=new ProfileFiles();
        List<ProfilePreviewRow>? undo;
        bool applying;
        public event Action<ConfirmedEntryWrite>? Written;
        public ConfigProfiles(ConfigDiscovery discovery,Func<ConfigEntryBase,NativeWriteFacts> resolve,
            string configDirectory,string almanacVersion,string gameBuild,Func<int>? backupCount=null,Func<ConfigEntryBase,bool>? hidden=null,Action<string>? warning=null)
        {
            this.discovery=discovery??throw new ArgumentNullException(nameof(discovery));
            this.resolve=resolve??throw new ArgumentNullException(nameof(resolve));
            // EntryWriter resolves again after parsing. Block remote profile amendments there too,
            // even if a mod's codec changed the role/lock after our preflight.
            writer=new EntryWriter(entry=>
            {
                var facts=this.resolve(entry)??new NativeWriteFacts();
                if(!EntryWritePolicy.MayCaptureForProfile(facts.ConnectedAsClient,facts.EntrySynced))
                    return new NativeWriteFacts{Known=facts.Known,Editable=false,Reason="Profiles never push server-provided values.",
                        ConnectedAsClient=facts.ConnectedAsClient,FileHasSyncedEntries=facts.FileHasSyncedEntries,EntrySynced=facts.EntrySynced};
                return facts;
            },warning);
            writer.Written+=change=>Written?.Invoke(change);
            configRoot=Path.GetFullPath(configDirectory);version=almanacVersion??"";build=gameBuild??"";
            this.backupCount=backupCount??(()=>10);this.hidden=hidden;this.warning=warning;
            ProfilesDirectory=Path.Combine(configRoot,"Meadworks","almanac","profiles");
            BackupsDirectory=Path.Combine(configRoot,"Meadworks","almanac","backups");
        }
        public string ProfilesDirectory { get; }
        public string BackupsDirectory { get; }
        public bool CanUndo=>undo!=null&&undo.Count!=0;
        public ConfigProfile Capture(string name,ProfileSelection? selection=null,bool defaults=false)
        {
            ThreadCheck();if(!defaults)NameCheck(name);selection??=new ProfileSelection();var values=new List<ProfileValue>();
            foreach(var file in discovery.Index.Files.ToArray())
            {
                if(!file.EntriesEditable||file.Owner==null&&file.Kind!=FileKind.CoreConfig||selection.File?.Invoke(file)==false)continue;
                foreach(var entry in discovery.Entries(file.Path).ToArray())
                {
                    if(!Live(entry)||hidden?.Invoke(entry)==true||selection.Entry?.Invoke(file,entry)==false)continue;
                    var facts=resolve(entry)??new NativeWriteFacts();
                    // In particular, never snapshot the displayed server override instead of the player's own value.
                    if(!facts.Known||!EntryWritePolicy.MayCaptureForProfile(facts.ConnectedAsClient,facts.EntrySynced))continue;
                    var native=new NativeEntry(entry);var attrs=native.Attributes;
                    if(native.Tags.Errors.Count!=0||attrs.Browsable==false||!selection.IncludeAdvanced&&(file.AllEntriesAdvanced||attrs.IsAdvanced==true))continue;
                    string type=TypeName(entry);
                    if(!selection.IncludeKeybinds&&(type=="UnityEngine.KeyCode"||type=="BepInEx.Configuration.KeyboardShortcut"))continue;
                    string current=native.RawSerialized(),reset=TomlTypeConverter.ConvertToString(Presentation.ResetTarget(attrs,entry.DefaultValue),entry.SettingType);
                    if(selection.OnlyChangedFromDefault&&string.Equals(current,reset,StringComparison.Ordinal))continue;
                    values.Add(new ProfileValue(file.Owner?.Guid??ConfigIndex.CoreGroupKey,file.Owner?.Version??"",FileKey(file.Path),entry.Definition.Section,entry.Definition.Key,type,defaults?reset:current));
                    if(values.Count>MaxEntries)throw new InvalidOperationException("A profile has too many entries.");
                }
            }
            var profile=new ConfigProfile(defaults?"Defaults":name,version,build,values,defaults);
            Encode(profile);return profile;
        }
        public ProfilePreview Preview(ConfigProfile profile)
        {ThreadCheck();if(profile==null)throw new ArgumentNullException(nameof(profile));return new ProfilePreview(this,profile,profile.Values.Select(PreviewOne));}
        ProfilePreviewRow PreviewOne(ProfileValue value)
        {
            var owned=discovery.Index.Files.Where(f=>f.EntriesEditable&&string.Equals(f.Owner?.Guid??(f.Kind==FileKind.CoreConfig?ConfigIndex.CoreGroupKey:""),value.Plugin,StringComparison.Ordinal)).ToArray();
            if(owned.Length==0)return new ProfilePreviewRow(value,ProfileOutcome.MissingPlugin,"Plugin is not indexed.");
            var matches=owned.Where(f=>string.Equals(FileKey(f.Path),value.File,StringComparison.OrdinalIgnoreCase))
                .SelectMany(f=>discovery.Entries(f.Path)).Where(e=>e.Definition.Section==value.Section&&e.Definition.Key==value.Key).ToArray();
            if(matches.Length!=1)return new ProfilePreviewRow(value,ProfileOutcome.UnknownKey,matches.Length==0?"No live matching entry.":"Entry identity is ambiguous.");
            var entry=matches[0];
            try
            {
                if(TypeName(entry)!=value.Type)return new ProfilePreviewRow(value,ProfileOutcome.TypeChanged,"The setting type changed.");
                var native=new NativeEntry(entry);var indexed=discovery.Index.Find(entry.ConfigFile.ConfigFilePath);
                bool restart=native.Attributes.RequiresRestart||indexed?.AllEntriesRequireRestart==true;
                string? locked=LockReason(entry,native);
                // Concealed values must not leak into profile preview, even when a profile supplies their key.
                if(hidden?.Invoke(entry)==true||native.Attributes.Browsable==false)return new ProfilePreviewRow(value,ProfileOutcome.Locked,"The setting is hidden.");
                string current=native.RawSerialized();
                if(locked!=null)return new ProfilePreviewRow(value,ProfileOutcome.Locked,locked,current:current,restart:restart);
                string input=WriterText(native,value.Value);var parsed=native.Parse(input);
                if(!parsed.Valid)return new ProfilePreviewRow(value,ProfileOutcome.InvalidValue,parsed.Error,current:current,restart:restart);
                return new ProfilePreviewRow(value,string.Equals(current,parsed.Serialized,StringComparison.Ordinal)?ProfileOutcome.Same:ProfileOutcome.Apply,
                    entry:entry,current:current,input:input,target:parsed.Serialized,restart:restart,clamped:parsed.WasClamped);
            }
            catch(Exception error){return new ProfilePreviewRow(value,ProfileOutcome.InvalidValue,error.GetBaseException().Message);}
        }
        string? LockReason(ConfigEntryBase entry,NativeEntry native)
        {
            if(!Live(entry))return "The native config file was replaced.";
            var facts=resolve(entry)??new NativeWriteFacts();var attrs=native.Attributes;
            if(!facts.Known)return "Sync state unknown.";
            if(!EntryWritePolicy.MayCaptureForProfile(facts.ConnectedAsClient,facts.EntrySynced))return "Profiles never push server-provided values.";
            if(native.Tags.Errors.Count!=0||attrs.ReadOnly==true||attrs.CharterReadOnly||attrs.GenericReadOnly)return "The mod marks this setting read-only.";
            if(!facts.Editable)return facts.Reason??"Locked.";
            if(facts.RequiresOwnerWrite&&facts.OwnerWrite==null)return "No verified owner write path.";
            return null;
        }
        // Profiles always store native TOML, including when an editor normally uses a mod's display codec.
        static string WriterText(NativeEntry native,string toml)
        {
            object? value=TomlTypeConverter.ConvertToValue(toml,native.Entry.SettingType);
            return EditorSelector.Codec(native.Shape,native.Attributes)==CodecChoice.ModDelegates
                ?(string?)((Delegate)native.Attributes.ObjToStr!).DynamicInvoke(value)??""
                :toml;
        }
        public ProfileApplyResult Apply(ProfilePreview preview,Func<ProfilePreviewRow,bool>? selected=null)
        {
            ThreadCheck();if(preview==null||!ReferenceEquals(preview.Owner,this))throw new ArgumentException("Preview belongs to another profile controller.",nameof(preview));
            if(applying)return Failure("A profile operation is already in progress.");
            var requested=preview.Rows.Where(r=>r.Outcome==ProfileOutcome.Apply&&(selected==null||selected(r))).ToList();
            return ApplyRows(requested,preview.Rows.Count-requested.Count,false);
        }
        public ProfileApplyResult Undo()
        {
            ThreadCheck();if(applying)return Failure("A profile operation is already in progress.");
            if(undo==null)return Failure("No profile apply is available to undo.");
            var requested=new List<ProfilePreviewRow>();
            foreach(var row in undo.AsEnumerable().Reverse())
            {
                var original=new ProfileValue(row.Value.Plugin,row.Value.PluginVersion,row.Value.File,row.Value.Section,row.Value.Key,row.Value.Type,row.Current!);
                var restored=PreviewOne(original);
                if(restored.Outcome!=ProfileOutcome.Apply||!ReferenceEquals(restored.Entry,row.Entry)||restored.Current!=row.Target)
                    return Failure("Undo refused: an entry changed, was replaced, or is now locked.");
                requested.Add(restored);
            }
            return ApplyRows(requested,0,true);
        }
        ProfileApplyResult ApplyRows(List<ProfilePreviewRow> requested,int skipped,bool isUndo)
        {
            var planned=new List<ProfilePreviewRow>();var applied=new List<ProfilePreviewRow>();var backups=new List<string>();
            applying=true;
            try
            {
                // Re-resolve every selected row before any mutation. Newly locked rows are explicitly skipped;
                // stale identities/values/converters abort the plan. Undo never partially skips a locked row.
                foreach(var old in requested)
                {
                    var fresh=PreviewOne(old.Value);
                    if(fresh.Outcome==ProfileOutcome.Locked&&!isUndo){skipped++;continue;}
                    if(fresh.Outcome!=ProfileOutcome.Apply||!ReferenceEquals(fresh.Entry,old.Entry)||fresh.Current!=old.Current||fresh.Target!=old.Target)
                        return new ProfileApplyResult(false,0,skipped,backups,"Profile preview changed; review it again.");
                    planned.Add(fresh);
                }
                foreach(var path in planned.Select(r=>r.Entry!.ConfigFile.ConfigFilePath).Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    if(!File.Exists(path))continue; // A bound, not-yet-saved ConfigFile has no original bytes to back up.
                    string backup=BackupPath(path);files.Copy(path,backup);backups.Add(backup);Prune(path);
                }
                foreach(var row in planned)
                {
                    // A prior mod handler may have changed a later entry or its permissions.
                    var fresh=PreviewOne(row.Value);
                    if(fresh.Outcome!=ProfileOutcome.Apply||!ReferenceEquals(fresh.Entry,row.Entry)||fresh.Current!=row.Current||fresh.Target!=row.Target)
                        throw new InvalidOperationException("A planned entry changed during profile apply.");
                    var result=writer.TryWrite(row.Entry!,fresh.Input!);
                    if(!result.Accepted||result.PendingAuthority)throw new InvalidOperationException(result.Error??"The mod refused a profile value.");
                    applied.Add(row);
                }
                // Also verify earlier values after every handler has run; do not accept cross-entry changes.
                foreach(var row in applied)
                    if(!Live(row.Entry!)||new NativeEntry(row.Entry!).RawSerialized()!=row.Target)throw new InvalidOperationException("The mod changed an earlier profile value.");
                if(isUndo)undo=null;else if(applied.Count!=0)undo=applied.ToList();
                return new ProfileApplyResult(true,applied.Count,skipped,backups);
            }
            catch(Exception error)
            {
                var failures=new List<string>();
                foreach(var row in applied.AsEnumerable().Reverse())
                {
                    try
                    {
                        var native=new NativeEntry(row.Entry!);string? reason=LockReason(row.Entry!,native);
                        // Respect concurrent/native handler changes: never overwrite a third value to undo ours.
                        if(reason!=null||native.RawSerialized()!=row.Target)throw new InvalidOperationException(reason??"Current value differs from this profile's write.");
                        var result=writer.TryWrite(row.Entry!,WriterText(native,row.Current!));
                        if(!result.Accepted||result.PendingAuthority)throw new InvalidOperationException(result.Error??"Restoration was refused.");
                    }
                    catch(Exception restore){failures.Add(row.Value.Plugin+" ["+row.Value.Section+"] "+row.Value.Key+": "+restore.GetBaseException().Message);}
                }
                // A failed transaction invalidates the previous undo group; backups remain available.
                undo=null;return new ProfileApplyResult(false,applied.Count,skipped,backups,error.GetBaseException().Message,failures);
            }
            finally{applying=false;}
        }
        static ProfileApplyResult Failure(string error)=>new ProfileApplyResult(false,0,0,Array.Empty<string>(),error);
        bool Live(ConfigEntryBase entry)=>ReferenceEquals(discovery.LiveFile(entry.ConfigFile.ConfigFilePath),entry.ConfigFile)
            &&discovery.Entries(entry.ConfigFile.ConfigFilePath).Any(e=>ReferenceEquals(e,entry));
        static string TypeName(ConfigEntryBase entry)=>entry.SettingType.FullName??entry.SettingType.Name;
        string FileKey(string path)
        {
            string relative=Path.GetRelativePath(configRoot,Path.GetFullPath(path)).Replace('\\','/');
            return relative.StartsWith("../",StringComparison.Ordinal)||Path.IsPathRooted(relative)?"external/"+Path.GetFileName(path):relative;
        }
        public IReadOnlyList<string> List()
        {ThreadCheck();return Directory.Exists(ProfilesDirectory)?Directory.GetFiles(ProfilesDirectory,"*.json").Select(p=>Path.GetFileNameWithoutExtension(p)).Where(ValidName).OrderBy(n=>n,StringComparer.OrdinalIgnoreCase).Take(1024).ToArray():Array.Empty<string>();}
        public ConfigProfile Load(string name)
        {ThreadCheck();NameCheck(name);return Decode(files.ReadAllText(ProfilePath(name)),name);}
        public SafeWriteResult Save(ConfigProfile profile)
        {
            ThreadCheck();if(profile.IsDefaults)throw new InvalidOperationException("Defaults is computed and cannot be saved.");NameCheck(profile.Name);
            string path=ProfilePath(profile.Name);var result=SafeFileWriter.Write(files,path,Encode(profile),BackupPath(path));
            Prune(path);return result;
        }
        public SafeWriteResult Import(string path,string name,bool overwrite=false)
        {
            ThreadCheck();NameCheck(name);if(File.Exists(ProfilePath(name))&&!overwrite)throw new IOException("A profile with this name already exists.");
            return Save(Decode(files.ReadAllText(Path.GetFullPath(path)),name));
        }
        public SafeWriteResult Export(ConfigProfile profile,string path,bool overwrite=false)
        {
            ThreadCheck();path=Path.GetFullPath(path);
            if(!string.Equals(Path.GetExtension(path),".json",StringComparison.OrdinalIgnoreCase))throw new ArgumentException("Export requires a .json path.",nameof(path));
            if(discovery.Index.Files.Any(f=>string.Equals(Path.GetFullPath(f.Path),path,StringComparison.OrdinalIgnoreCase)))throw new InvalidOperationException("A discovered mod file cannot be a profile export destination.");
            if(File.Exists(path)&&!overwrite)throw new IOException("The export destination already exists.");
            var result=SafeFileWriter.Write(files,path,Encode(profile),BackupPath(path));Prune(path);return result;
        }
        string ProfilePath(string name)=>Path.Combine(ProfilesDirectory,name+".json");
        static bool ValidName(string name)=>!string.IsNullOrWhiteSpace(name)&&name.Length<=64&&name.Trim()==name&&name!="."&&name!=".."
            &&!string.Equals(name,"Defaults",StringComparison.OrdinalIgnoreCase)&&name.IndexOfAny(Path.GetInvalidFileNameChars())<0
            &&name.IndexOf('/')<0&&name.IndexOf('\\')<0&&!name.EndsWith(".",StringComparison.Ordinal);
        static void NameCheck(string name){if(!ValidName(name))throw new ArgumentException("Choose a profile name of 1-64 characters without path separators; Defaults is reserved.",nameof(name));}
        void ThreadCheck(){if(Thread.CurrentThread.ManagedThreadId!=thread)throw new InvalidOperationException("Profiles must run on their owning thread.");}
        string BackupPath(string path)
        {
            string folder=BackupFolder(path);Directory.CreateDirectory(folder);DateTime utc=DateTime.UtcNow;
            // Retention can leave holes in this second's names. Always advance beyond the newest
            // matching backup so pruning never deletes the backup we have just created.
            var existing=BackupNaming.ToPrune(Path.GetFileName(path),Directory.GetFiles(folder).Select(p=>Path.GetFileName(p)),0);
            if(existing.Count>100)throw new IOException("Backup retention must succeed before more backups can be created.");
            string? newest=existing.LastOrDefault();
            string fileName=Path.GetFileName(path);int dot=fileName.LastIndexOf('.');int stampAt=(dot>0?dot:fileName.Length)+1;
            if(newest!=null&&newest.Length>=stampAt+15&&DateTime.TryParseExact(newest.Substring(stampAt,15),BackupNaming.StampFormat,CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal|DateTimeStyles.AdjustToUniversal,out var last)&&last>=utc.AddSeconds(-1))utc=last.AddSeconds(1);
            for(int i=0;i<100;i++){string candidate=Path.Combine(folder,BackupNaming.Name(path,utc.AddSeconds(i)));if(!File.Exists(candidate))return candidate;}
            throw new IOException("The bounded backup namespace is full for this timestamp.");
        }
        string BackupFolder(string path)
        {
            string identity;
            using(var hash=SHA256.Create())identity=string.Concat(hash.ComputeHash(Encoding.UTF8.GetBytes(Path.GetFullPath(path).ToUpperInvariant())).Select(b=>b.ToString("x2",CultureInfo.InvariantCulture)));
            return Path.Combine(BackupsDirectory,identity);
        }
        void Prune(string path)
        {
            // Backup failure is blocking; retention failure is not a reason to roll back a successful native edit.
            try
            {
                string folder=BackupFolder(path);if(!Directory.Exists(folder))return;
                foreach(string name in BackupNaming.ToPrune(Path.GetFileName(path),Directory.GetFiles(folder).Select(p=>Path.GetFileName(p)),Math.Max(1,Math.Min(100,backupCount()))))File.Delete(Path.Combine(folder,name));
            }
            catch(Exception error){try{warning?.Invoke("Profile backup retention failed: "+error.GetBaseException().Message);}catch{}}
        }
        static string Encode(ConfigProfile profile)
        {
            if(profile.Values.Count>MaxEntries)throw new FormatException("A profile has too many entries.");
            var text=new StringBuilder("{\"schema\":1,\"almanacVersion\":").Append(Quote(profile.AlmanacVersion)).Append(",\"gameBuild\":").Append(Quote(profile.GameBuild)).Append(",\"entries\":[");
            bool comma=false;var identities=new HashSet<string>(StringComparer.Ordinal);
            foreach(var value in profile.Values)
            {
                if(!identities.Add(Identity(value)))throw new FormatException("Duplicate profile entry identity.");
                if(comma)text.Append(',');comma=true;
                text.Append("{\"plugin\":").Append(Quote(value.Plugin)).Append(",\"pluginVersion\":").Append(Quote(value.PluginVersion)).Append(",\"file\":").Append(Quote(value.File))
                    .Append(",\"section\":").Append(Quote(value.Section)).Append(",\"key\":").Append(Quote(value.Key)).Append(",\"type\":").Append(Quote(value.Type)).Append(",\"value\":").Append(Quote(value.Value)).Append('}');
                if(text.Length>MaxBytes)throw new FormatException("Profile exceeds the strict JSON limit.");
            }
            text.Append("]}\n");string json=text.ToString();if(Encoding.UTF8.GetByteCount(json)>MaxBytes)throw new FormatException("Profile exceeds 1 MiB.");return json;
        }
        static string Quote(string value)
        {
            if(value.Length>MaxString)throw new FormatException("A profile string is too long.");var text=new StringBuilder("\"");
            foreach(char c in value)
                if(c=='"'||c=='\\')text.Append('\\').Append(c);
                else if(c<32||char.IsSurrogate(c))text.Append("\\u").Append(((int)c).ToString("x4",CultureInfo.InvariantCulture));
                else text.Append(c);
            return text.Append('"').ToString();
        }
        static ConfigProfile Decode(string json,string name)
        {
            if(Encoding.UTF8.GetByteCount(json)>MaxBytes)throw new FormatException("Profile exceeds 1 MiB.");
            // Use C09's strict, bounded reader. It is internal to Crucible.Core; no alternate JSON parser is introduced.
            var parse=typeof(ConfigConverter).Assembly.GetType("ExtraPotions.Meadworks.Crucible.MiniJson")?.GetMethod("Parse",BindingFlags.Public|BindingFlags.Static);
            if(parse==null)throw new InvalidOperationException("The Crucible strict JSON reader is unavailable.");
            object? decoded;
            try{decoded=parse.Invoke(null,new object[]{json});}catch(TargetInvocationException error){throw new FormatException(error.GetBaseException().Message,error);}
            var root=decoded as Dictionary<string,object?>??throw new FormatException("A profile is a JSON object.");
            if(!root.TryGetValue("schema",out var schema)||!(schema is double number)||number!=1)throw new FormatException("Unsupported profile schema.");
            if(!root.TryGetValue("entries",out var raw)||!(raw is List<object?> entries)||entries.Count>MaxEntries)throw new FormatException("Invalid profile entries.");
            var values=new List<ProfileValue>();var identities=new HashSet<string>(StringComparer.Ordinal);
            foreach(var item in entries)
            {
                var row=item as Dictionary<string,object?>??throw new FormatException("An entry must be a JSON object.");
                var value=new ProfileValue(Field(row,"plugin"),Field(row,"pluginVersion"),Field(row,"file"),Field(row,"section"),Field(row,"key"),Field(row,"type"),Field(row,"value"));
                if(value.Plugin.Length==0||value.File.Length==0||value.Key.Length==0||value.Type.Length==0)throw new FormatException("Entry identity is incomplete.");
                if(!identities.Add(Identity(value)))throw new FormatException("Duplicate profile entry identity.");values.Add(value);
            }
            return new ConfigProfile(name,Field(root,"almanacVersion"),Field(root,"gameBuild"),values);
        }
        static string Field(Dictionary<string,object?> row,string key)
        {if(!row.TryGetValue(key,out var value)||!(value is string text)||text.Length>MaxString)throw new FormatException("Invalid profile field: "+key);return text;}
        static string Identity(ProfileValue value)=>Quote(value.Plugin)+Quote(value.File.ToUpperInvariant())+Quote(value.Section)+Quote(value.Key);
        sealed class ProfileFiles:IFileOps
        {
            static readonly UTF8Encoding Utf8=new UTF8Encoding(false,true);
            public bool Exists(string path)=>File.Exists(path);
            public string ReadAllText(string path)
            {
                using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
                {
                    if(stream.Length>MaxBytes)throw new IOException("Profile exceeds 1 MiB.");
                    using(var reader=new StreamReader(stream,Utf8,true))return reader.ReadToEnd();
                }
            }
            public void WriteAllText(string path,string text)
            {
                if(Utf8.GetByteCount(text)>MaxBytes)throw new IOException("Profile exceeds 1 MiB.");
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);string temporary=Path.Combine(Path.GetDirectoryName(path)!,Guid.NewGuid().ToString("N")+".tmp");
                try
                {
                    using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None))
                    {byte[] bytes=Utf8.GetBytes(text);stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
                    Publish(temporary,path);
                }
                finally{if(File.Exists(temporary))File.Delete(temporary);}
            }
            public void Copy(string source,string destination)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);string temporary=Path.Combine(Path.GetDirectoryName(destination)!,Guid.NewGuid().ToString("N")+".tmp");
                try
                {
                    using(var input=new FileStream(source,FileMode.Open,FileAccess.Read,FileShare.Read))
                    {
                        if(input.Length>16L*MaxBytes)throw new IOException("A config backup exceeds 16 MiB.");
                        using(var output=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None))
                        {input.CopyTo(output);output.Flush(true);}
                    }
                    Publish(temporary,destination);
                }
                finally{if(File.Exists(temporary))File.Delete(temporary);}
            }
            static void Publish(string temporary,string path)
            {if(File.Exists(path))File.Replace(temporary,path,null);else File.Move(temporary,path);}
            public void Delete(string path)=>File.Delete(path);
        }
    }
}
