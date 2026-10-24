// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Diagnostics;
using ExtraPotions.Meadworks.Crucible;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using BepInEx.Configuration;
using ExtraPotions.Almanac.Attributes;
using ExtraPotions.Almanac.Display;
using ExtraPotions.Almanac.Editors;
using ExtraPotions.Almanac.Index;
using ExtraPotions.Almanac.Runtime.Discovery;
using ExtraPotions.Almanac.Runtime.Editing;
using CorePlugin=ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Almanac.Runtime.Changes
{
    public enum RestartClass { Live, Maybe, Restart }
    public sealed class ChangeRecord
    {
        internal ChangeRecord(ConfigEntryBase entry,string before,string after,string route,RestartClass restart)
        {Entry=new WeakReference<ConfigEntryBase>(entry);Path=entry.ConfigFile.ConfigFilePath;Section=entry.Definition.Section;Key=entry.Definition.Key;BeforeSession=before;LastValue=after;Route=route;Restart=restart;Time=DateTime.UtcNow;}
        internal WeakReference<ConfigEntryBase> Entry { get; }
        public string Path { get; }
        public string Section { get; }
        public string Key { get; }
        public string BeforeSession { get; }
        public string LastValue { get; internal set; }
        public DateTime Time { get; internal set; }
        public string Route { get; internal set; }
        public RestartClass Restart { get; internal set; }
        public bool Changed=>!string.Equals(BeforeSession,LastValue,StringComparison.Ordinal);
    }
    public sealed class ChangeSkip
    {
        internal ChangeSkip(ConfigEntryBase entry,string reason){Entry=entry;Reason=reason;}
        public ConfigEntryBase Entry { get; }
        public string Reason { get; }
    }
    public sealed class ResetItem
    {
        internal ResetItem(ConfigEntryBase entry,string before,string value,string text){Entry=entry;Before=before;Value=value;Text=text;}
        public ConfigEntryBase Entry { get; }
        public string Before { get; }
        public string Value { get; }
        internal string Text { get; }
    }
    public sealed class ResetPreview
    {
        internal ResetPreview(ChangeJournal owner,IReadOnlyList<ResetItem> items,IReadOnlyList<ChangeSkip> skipped){Owner=owner;Items=items;Skipped=skipped;}
        internal ChangeJournal Owner { get; }
        public IReadOnlyList<ResetItem> Items { get; }
        public IReadOnlyList<ChangeSkip> Skipped { get; }
    }
    public sealed class ChangeActionResult
    {
        internal readonly List<ChangeSkip> skips=new List<ChangeSkip>();
        public int Accepted { get; internal set; }
        public int Pending { get; internal set; }
        public int Failed { get; internal set; }
        public IReadOnlyList<ChangeSkip> Skipped=>skips;
    }
    /// <summary>Session observation plus reset/undo through the existing live native writer. No remote value is saved.</summary>
    public sealed class ChangeJournal:IDisposable
    {
        readonly EntryWriter writer;
        readonly MeadworksLog? log;
        readonly ConfigDiscovery discovery;
        readonly Func<ConfigEntryBase,NativeWriteFacts> resolve;
        readonly AlmanacPreferences preferences;
        readonly string path;
        readonly int thread=Thread.CurrentThread.ManagedThreadId;
        readonly Dictionary<string,ChangeRecord> records=new Dictionary<string,ChangeRecord>(StringComparer.Ordinal);
        readonly List<PendingWrite> pending=new List<PendingWrite>();
        sealed class PendingWrite
        {
            internal WeakReference<ConfigEntryBase> Entry;
            internal string Before,After;
            internal Func<bool> Confirm;
            internal long World,Generation;
            internal object Epoch;
            internal PendingWrite(ConfigEntryBase entry,string before,string after,Func<bool> confirm)
            {Entry=new WeakReference<ConfigEntryBase>(entry);Before=before;After=after;Confirm=confirm;World=CorePlugin.Session.WorldId;Generation=CorePlugin.Session.Generation;Epoch=CorePlugin.Session.SessionId;}
        }
        bool disposed;
        public ChangeJournal(EntryWriter writer,ConfigDiscovery discovery,Func<ConfigEntryBase,NativeWriteFacts> resolver,string configdir,AlmanacPreferences prefs,MeadworksLog? logger=null)
        {
            log=logger;
            this.writer=writer??throw new ArgumentNullException(nameof(writer));this.discovery=discovery??throw new ArgumentNullException(nameof(discovery));
            resolve=resolver??throw new ArgumentNullException(nameof(resolver));preferences=prefs??throw new ArgumentNullException(nameof(prefs));
            path=System.IO.Path.Combine(System.IO.Path.GetFullPath(configdir),"Meadworks","almanac","journal.log");writer.Written+=Written;
        }
        public event Action? Changed;
        /// <summary>Records a confirmed writer change or a UI change after the owner's actual matching receipt.</summary>
        public void RecordConfirmed(ConfirmedEntryWrite change)=>Written(change);
        public string? Warning { get; private set; }
        public IReadOnlyList<ChangeRecord> Records=>records.Values.OrderByDescending(row=>row.Time).ToArray();
        public int Count=>records.Values.Count(row=>row.Changed);
        public int RestartCount=>records.Values.Count(row=>row.Changed&&row.Restart==RestartClass.Restart);
        public int MaybeCount=>records.Values.Count(row=>row.Changed&&row.Restart==RestartClass.Maybe);
        public int PendingCount=>pending.Count;
        /// <summary>Only an actual owner's later matching receipt can record a submitted remote reset/undo.</summary>
        public void Tick()
        {
            Check();bool removed=false;
            foreach(var item in pending.ToArray())
            {
                var session=CorePlugin.Session;
                if(!item.Entry.TryGetTarget(out var entry)||!Current(entry)||session.WorldId!=item.World||session.Generation!=item.Generation||!Equals(session.SessionId,item.Epoch))
                {pending.Remove(item);removed=true;continue;}
                try
                {
                    if(!item.Confirm()||new NativeEntry(entry).RawSerialized()!=item.After)continue;
                    pending.Remove(item);removed=true;Written(new ConfirmedEntryWrite(entry,item.Before,item.After,"OwnerAmend"));
                }
                catch{pending.Remove(item);removed=true;} // Unknown owner confirmation is never acceptance.
            }
            if(removed)Notify();
        }
        void Written(ConfirmedEntryWrite change)
        {
            Check();string key=Id(change.Entry);var restart=Classify(change.Entry);
            if(!records.TryGetValue(key,out var row)||!row.Entry.TryGetTarget(out var held)||!ReferenceEquals(held,change.Entry))
            {if(records.Count>=4096)records.Remove(records.OrderBy(pair=>pair.Value.Time).First().Key);row=new ChangeRecord(change.Entry,change.Before,change.After,change.Route,restart);records[key]=row;}
            else{row.LastValue=change.After;row.Time=DateTime.UtcNow;row.Route=change.Route;row.Restart=restart;}
            try{Append(row,change.Before);}catch(Exception error){Warning="The setting changed, but journal writing failed: "+error.GetBaseException().Message;}
            Notify();
        }
        public RestartClass Classify(ConfigEntryBase entry)
        {
            var native=new NativeEntry(entry);
            if(native.Attributes.RequiresRestart||discovery.Index.Find(entry.ConfigFile.ConfigFilePath)?.Kind==FileKind.CoreConfig)return RestartClass.Restart;
            string description=native.Attributes.Description??entry.Description.Description??"";
            if(SettingNotes.Classify(description,(preferences.RestartPhrases.Value??"").Split(new[]{',',';','\n'},StringSplitOptions.RemoveEmptyEntries))==ChangeTiming.Restart)return RestartClass.Restart;
            return Subscriber(entry)||Subscriber(entry.ConfigFile)?RestartClass.Live:RestartClass.Maybe;
        }
        static bool Subscriber(object target)
        {
            for(var type=target.GetType();type!=null;type=type.BaseType)
            {
                var field=type.GetField("SettingChanged",BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.DeclaredOnly);
                if(field==null)continue;
                try{return (field.GetValue(target) as Delegate)?.GetInvocationList().Any(callback=>callback.Method.DeclaringType?.Assembly!=typeof(ConfigFile).Assembly&&callback.Method.DeclaringType?.Assembly!=typeof(ChangeJournal).Assembly)==true;}catch{return false;}
            }
            return false;
        }
        public ResetPreview PreviewReset(IEnumerable<ConfigEntryBase> entries)
        {
            Check();var items=new List<ResetItem>();var skipped=new List<ChangeSkip>();
            foreach(var entry in Selection(entries))
            {
                try
                {
                    var native=new NativeEntry(entry);string? reason=Editable(entry,native,true);
                    if(reason!=null){skipped.Add(new ChangeSkip(entry,reason));continue;}
                    object? value=Presentation.ResetTarget(native.Attributes,entry.DefaultValue);string text=Text(native,value);
                    var parsed=native.Parse(text);if(!parsed.Valid){skipped.Add(new ChangeSkip(entry,parsed.Error??"Default is invalid."));continue;}
                    string before=native.RawSerialized();if(before!=parsed.Serialized)items.Add(new ResetItem(entry,before,parsed.Serialized,text));
                }
                catch(Exception error){skipped.Add(new ChangeSkip(entry,error.GetBaseException().Message));}
            }
            return new ResetPreview(this,items.ToArray(),skipped.ToArray());
        }
        public ChangeActionResult ApplyReset(ResetPreview preview)
        {
            Check();if(preview==null||!ReferenceEquals(preview.Owner,this))throw new ArgumentException("Reset preview belongs to another journal.",nameof(preview));
            var result=new ChangeActionResult();result.skips.AddRange(preview.Skipped);
            // Validate the complete preview before the first write; each actual assignment still re-resolves current locks.
            foreach(var item in preview.Items)
            {
                if(!Current(item.Entry)||new NativeEntry(item.Entry).RawSerialized()!=item.Before)throw new InvalidOperationException("An entry changed; preview reset again.");
                var fresh=PreviewReset(new[]{item.Entry});if(fresh.Items.Count!=1||fresh.Items[0].Value!=item.Value||fresh.Items[0].Text!=item.Text)throw new InvalidOperationException("A default, converter or lock changed; preview reset again.");
            }
            foreach(var item in preview.Items)
            {
                if(!Current(item.Entry)||new NativeEntry(item.Entry).RawSerialized()!=item.Before)
                {result.skips.Add(new ChangeSkip(item.Entry,"The entry changed during reset; preview again."));continue;}
                Apply(item.Entry,item.Text,result);
            }
            return result;
        }
        public ChangeActionResult Undo()=>Undo(records.Values.Where(row=>row.Changed).Select(row=>row.Entry.TryGetTarget(out var entry)?entry:null).Where(entry=>entry!=null).Cast<ConfigEntryBase>());
        public ChangeActionResult Undo(IEnumerable<ConfigEntryBase> entries)
        {
            Check();var result=new ChangeActionResult();
            foreach(var entry in Selection(entries))
            {
                if(!records.TryGetValue(Id(entry),out var row)||!row.Changed)continue;
                try
                {
                    var native=new NativeEntry(entry);string? reason=Editable(entry,native,false);
                    if(reason==null&&(!row.Entry.TryGetTarget(out var held)||!ReferenceEquals(held,entry)||native.RawSerialized()!=row.LastValue))reason="The entry changed outside this session edit; undo was skipped.";
                    if(reason!=null){result.skips.Add(new ChangeSkip(entry,reason));continue;}
                    Apply(entry,Text(native,TomlTypeConverter.ConvertToValue(row.BeforeSession,entry.SettingType)),result);
                }
                catch(Exception error){result.Failed++;result.skips.Add(new ChangeSkip(entry,error.GetBaseException().Message));}
            }
            return result;
        }
        void Apply(ConfigEntryBase entry,string text,ChangeActionResult result)
        {
            if(pending.Count>=4096){result.Failed++;result.skips.Add(new ChangeSkip(entry,"Too many unconfirmed session edits."));return;}
            string before=new NativeEntry(entry).RawSerialized();var outcome=writer.TryWrite(entry,text);
            if(outcome.PendingAuthority)
            {
                result.Pending++;
                if(outcome.IsConfirmed!=null&&outcome.Serialized!=null){pending.Add(new PendingWrite(entry,before,outcome.Serialized,outcome.IsConfirmed));Notify();}
            }
            else if(outcome.Accepted)result.Accepted++;
            else{result.Failed++;result.skips.Add(new ChangeSkip(entry,outcome.Error??outcome.Decision?.Reason??"The write was refused."));}
        }
        string? Editable(ConfigEntryBase entry,NativeEntry native,bool reset)
        {
            if(!Current(entry))return "The native entry is no longer registered.";
            var facts=resolve(entry);var a=native.Attributes;
            if(!facts.Known||!facts.Editable)return facts.Reason??"The entry is locked.";
            if(native.Tags.Errors.Count!=0||a.ReadOnly==true||a.CharterReadOnly||a.GenericReadOnly)return "The mod marks this entry read-only.";
            if((facts.RequiresOwnerWrite||facts.ConnectedAsClient&&facts.EntrySynced)&&facts.OwnerWrite==null)return "No verified owner write path.";
            return reset&&!Presentation.ShowsResetButton(a)?"The mod hides the reset button.":null;
        }
        bool Current(ConfigEntryBase entry)=>discovery.Entries(entry.ConfigFile.ConfigFilePath).Any(current=>ReferenceEquals(current,entry));
        static string Text(NativeEntry native,object? value)=>EditorSelector.Codec(native.Shape,native.Attributes)==CodecChoice.ModDelegates
            ?(string?)((Delegate)native.Attributes.ObjToStr!).DynamicInvoke(value)??"":TomlTypeConverter.ConvertToString(value,native.Entry.SettingType);
        static ConfigEntryBase[] Selection(IEnumerable<ConfigEntryBase> entries)
        {if(entries==null)throw new ArgumentNullException(nameof(entries));var selected=entries.Distinct().Take(4097).ToArray();if(selected.Length>4096||selected.Any(entry=>entry==null))throw new ArgumentException("Select at most 4096 actual entries.",nameof(entries));return selected;}
        static string Id(ConfigEntryBase entry)=>System.IO.Path.GetFullPath(entry.ConfigFile.ConfigFilePath)+"\0"+entry.Definition.Section+"\0"+entry.Definition.Key;
        void Append(ChangeRecord row,string before)
        {
            var timing=new StallTiming(log);
            long flushTicks=0,readTicks=0,writeTicks=0;
            int retained=0,appended=0;
            bool complete=false;
            try
            {
            int max=Math.Max(64,Math.Min(8192,preferences.JournalSizeKb.Value))*1024;
            string values=Sensitive(row.Section+"."+row.Key)?"[redacted]\t[redacted]":Escape(before)+"\t"+Escape(row.LastValue);
            string line=row.Time.ToString("O",CultureInfo.InvariantCulture)+"\t"+Escape(row.Path)+"\t"+Escape(row.Section)+"\t"+Escape(row.Key)+"\t"+Escape(row.Route)+"\t"+values+"\n";
            byte[] tail=Encoding.UTF8.GetBytes(line);if(tail.Length>max)tail=Encoding.UTF8.GetBytes(row.Time.ToString("O",CultureInfo.InvariantCulture)+"\t[entry too large]\n");
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
            appended=tail.Length;
            long readStart=timing.Enabled?Stopwatch.GetTimestamp():0;
            bool truncated=false;byte[] existing=Array.Empty<byte>();if(File.Exists(path))using(var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
            {int count=(int)Math.Min(input.Length,max-tail.Length);truncated=input.Length>count;existing=new byte[count];input.Position=input.Length-count;int read=0;while(read<count){int got=input.Read(existing,read,count-read);if(got==0)break;read+=got;}if(read!=count)Array.Resize(ref existing,read);}
            if(timing.Enabled)readTicks=Stopwatch.GetTimestamp()-readStart;
            int start=0;if(truncated&&existing.Length!=0){while(start<existing.Length&&existing[start]!='\n')start++;if(start<existing.Length)start++;}
            retained=existing.Length-start;
            long writeStart=timing.Enabled?Stopwatch.GetTimestamp():0;
            using(var output=new FileStream(path,FileMode.Create,FileAccess.Write,FileShare.Read))
            {
                output.Write(existing,start,existing.Length-start);output.Write(tail,0,tail.Length);
                long flushStart=timing.Enabled?Stopwatch.GetTimestamp():0;
                if(timing.Enabled)writeTicks=flushStart-writeStart;
                try{output.Flush(true);}
                finally{if(timing.Enabled)flushTicks=Stopwatch.GetTimestamp()-flushStart;}
            }
            complete=true;
            }
            finally
            {
                if(timing.Enabled)timing.Finish().Complete("Almanac.journal", "complete="+complete+" retainedBytes="+retained+" appendedBytes="+appended
                    +" readMs="+StallTiming.Ms(readTicks)+" writeMs="+StallTiming.Ms(writeTicks)+" flushMs="+StallTiming.Ms(flushTicks));
            }
        }
        static bool Sensitive(string key){string name=new string(key.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());return new[]{"password","passwd","token","secret","apikey","credential","privatekey"}.Any(name.Contains);}
        static string Escape(string text)=>text.Replace("\\","\\\\").Replace("\r","\\r").Replace("\n","\\n").Replace("\t","\\t").Replace("\0","\\0");
        void Notify(){if(Changed!=null)foreach(Action listener in Changed.GetInvocationList())try{listener();}catch{}}
        void Check(){if(disposed)throw new ObjectDisposedException(nameof(ChangeJournal));if(Thread.CurrentThread.ManagedThreadId!=thread)throw new InvalidOperationException("Session changes must remain on their owning thread.");}
        public void Dispose(){if(disposed)return;writer.Written-=Written;disposed=true;records.Clear();pending.Clear();Changed=null;}
    }
}
