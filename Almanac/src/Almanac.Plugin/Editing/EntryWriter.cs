// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using ExtraPotions.Meadworks.Crucible;
using System.Collections.Generic;
using System.Threading;
using BepInEx.Configuration;
using BepInEx.Logging;
using ExtraPotions.Almanac.Writing;

namespace ExtraPotions.Almanac.Runtime.Editing
{
    /// <summary>Resolved live by the lock/sync adapter, immediately before a write. Unknown is locked by default.</summary>
    public sealed class NativeWriteFacts
    {
        public bool Known { get; set; }
        public bool Editable { get; set; }
        public string? Reason { get; set; }
        public bool ConnectedAsClient { get; set; }
        public bool FileHasSyncedEntries { get; set; }
        public bool EntrySynced { get; set; }
        public ConfigScope? DeclaredScope { get; set; }
        /// <summary>Managed Meadworks entries always use their registered Amend route, even for own local values.</summary>
        public bool RequiresOwnerWrite { get; set; }
        /// <summary>A verified owning-system route; true means admitted/submitted, not proof of server acceptance.</summary>
        public Func<object?,bool>? OwnerWrite { get; set; }
        public Func<object?,Func<bool>>? ConfirmWrite { get; set; }
    }
    public sealed class NativeWriteResult
    {
        internal NativeWriteResult(WriteDecision? decision,bool accepted,bool pending,bool rejected,string? error,string? serialized=null,bool clamped=false,Func<bool>? isConfirmed=null)
        {Decision=decision;Accepted=accepted;PendingAuthority=pending;Rejected=rejected;Error=error;Serialized=serialized;WasClamped=clamped;IsConfirmed=isConfirmed;}
        public WriteDecision? Decision { get; }
        public bool Accepted { get; }
        public bool PendingAuthority { get; }
        public bool Rejected { get; }
        public string? Error { get; }
        public string? Serialized { get; }
        public bool WasClamped { get; }
        /// <summary>Later actual owner receipt, bound to this write's native connection and world. Local assignment alone is insufficient.</summary>
        public Func<bool>? IsConfirmed { get; }
    }
    public sealed class ConfirmedEntryWrite
    {
        internal ConfirmedEntryWrite(ConfigEntryBase entry,string before,string after,string route)
        {Entry=entry;Before=before;After=after;Route=route;}
        public ConfigEntryBase Entry { get; }
        public string Before { get; }
        public string After { get; }
        public string Route { get; }
    }
    public sealed class EntryWriter
    {
        readonly Func<ConfigEntryBase,NativeWriteFacts> resolve;
        readonly Action<string>? warning;
        readonly HashSet<string> warned=new HashSet<string>(StringComparer.Ordinal);
        readonly int thread=Thread.CurrentThread.ManagedThreadId;
        /// <summary>Confirmed local changes only. A submitted remote amendment requires its real echo first.</summary>
        public event Action<ConfirmedEntryWrite>? Written;
        public EntryWriter(Func<ConfigEntryBase,NativeWriteFacts> resolve,Action<string>? warning=null)
        {this.resolve=resolve??throw new ArgumentNullException(nameof(resolve));this.warning=warning;}
        /// <summary>No lock cached from rendering is trusted. Does not call Save for files containing remote values.</summary>
        public NativeWriteResult TryWrite(ConfigEntryBase entry,string text)
        {
            if(entry==null)throw new ArgumentNullException(nameof(entry));
            if(Thread.CurrentThread.ManagedThreadId!=thread)return new NativeWriteResult(null,false,false,false,"Config editing must remain on its owning thread.");
            object? previous=null;string? before=null;NativeWriteFacts? facts=null;WriteDecision? decision=null;
            bool originalSave=entry.ConfigFile.SaveOnConfigSet,assigned=false;
            try
            {
                previous=entry.BoxedValue;
                var native=new NativeEntry(entry);before=native.RawSerialized();var parsed=native.Parse(text);
                facts=resolve(entry)??new NativeWriteFacts();
                bool ownerRoute=facts.RequiresOwnerWrite||(facts.ConnectedAsClient&&facts.EntrySynced);
                // A mod's converter or the resolver can change tags. Read its current lock at admission.
                var liveTags=NativeTags.Read(entry.Description.Tags);var attributes=liveTags.Attributes;
                var dependency=EntryDependencies.Evaluate(entry);
                if(!dependency.Visible||!dependency.Enabled)return new NativeWriteResult(null,false,false,false,dependency.Reason,before);
                bool tagsLocked=liveTags.Errors.Count!=0||attributes.ReadOnly==true||attributes.CharterReadOnly||attributes.GenericReadOnly;
                bool persistOwn=!facts.EntrySynced&&facts.Known&&(facts.DeclaredScope==ConfigScope.Local||!facts.ConnectedAsClient||!facts.FileHasSyncedEntries);
                bool suppressSave=persistOwn||facts.ConnectedAsClient&&(facts.FileHasSyncedEntries||facts.EntrySynced);
                var request=new WriteRequest{ValueValid=parsed.Valid,Error=parsed.Error,
                    Editable=facts.Known&&facts.Editable&&!tagsLocked&&(!ownerRoute||facts.OwnerWrite!=null),
                    LockReason=!facts.Known?"Sync state unknown":tagsLocked?LockReasonPolicy.ForReadOnlyTag(facts.EntrySynced,facts.Known,facts.Editable,facts.Reason,"The mod marks this setting read-only."):ownerRoute&&facts.OwnerWrite==null?"No verified owner write path.":facts.Reason,
                    SameAsCurrent=parsed.Valid&&string.Equals(before,parsed.Serialized,StringComparison.Ordinal),
                    ConnectedAsClient=facts.ConnectedAsClient,FileHasSyncedEntries=facts.FileHasSyncedEntries,EntrySynced=facts.EntrySynced,SaveOnConfigSet=originalSave&&!suppressSave,PersistOwnValue=persistOwn};
                decision=EntryWritePolicy.Decide(request);
                if(decision.Verdict!=WriteVerdict.Write)return new NativeWriteResult(decision,decision.Verdict==WriteVerdict.NoChange,false,false,decision.Reason,before,parsed.WasClamped);
                // Parse/delegate callbacks can change the current value; refuse a stale assignment.
                if(!string.Equals(native.RawSerialized(),before,StringComparison.Ordinal))return new NativeWriteResult(decision,false,false,true,"The entry changed while preparing this edit.");
                dependency=EntryDependencies.Evaluate(entry);
                if(!dependency.Visible||!dependency.Enabled)return new NativeWriteResult(decision,false,false,true,dependency.Reason,before);
                if(suppressSave)entry.ConfigFile.SaveOnConfigSet=false;
                using(var errors=new HandlerErrors())
                {
                    var confirmation=facts.ConnectedAsClient&&facts.EntrySynced?facts.ConfirmWrite?.Invoke(parsed.Value):null;
                    assigned=true;
                    if(ownerRoute)
                    {
                        if(!facts.OwnerWrite!(parsed.Value))throw new InvalidOperationException("The owning system refused the edit.");
                        if(errors.Error!=null)throw new InvalidOperationException("The mod rejected this value.",errors.Error);
                        if(facts.ConnectedAsClient&&facts.EntrySynced)return new NativeWriteResult(decision,false,true,false,null,parsed.Serialized,parsed.WasClamped,confirmation);
                    }
                    else entry.BoxedValue=parsed.Value;
                    if(errors.Error!=null)throw new InvalidOperationException("The mod rejected this value.",errors.Error);
                    if(!string.Equals(native.RawSerialized(),parsed.Serialized,StringComparison.Ordinal))throw new InvalidOperationException("The mod did not retain the requested serialized value.");
                }
                // Registered owner amendments already validate and publish their own value.
                bool ownerPersisted=ownerRoute&&MeadworksConfig.TryGetMetadata(entry,out _);
                if(persistOwn&&!ownerPersisted)MeadworksConfig.PersistOwnValue(entry);
                else if(!ownerRoute&&EntryWritePolicy.MaySaveFile(facts.ConnectedAsClient,facts.FileHasSyncedEntries))entry.ConfigFile.Save();
                Notify(new ConfirmedEntryWrite(entry,before!,parsed.Serialized,ownerRoute?"OwnerAmend":"NativeAssignment"));
                return new NativeWriteResult(decision,true,false,false,null,parsed.Serialized,parsed.WasClamped);
            }
            catch(Exception error)
            {
                string message=error is System.IO.IOException?error.Message:error.GetBaseException().Message;
                if(assigned&&before!=null&&EntryWritePolicy.AfterAssign(true,false)==HandlerOutcome.RestorePrevious)
                {
                    try
                    {
                        // Never impersonate an owner to undo an admitted remote request; its sync system
                        // owns correction/echo. Local assignment is restored once through the same route.
                        bool ownerRoute=facts!=null&&(facts.RequiresOwnerWrite||(facts.ConnectedAsClient&&facts.EntrySynced));
                        if(ownerRoute&&facts!.ConnectedAsClient&&facts.EntrySynced)throw new InvalidOperationException("Remote correction/echo belongs to the owning sync system; no rollback amendment was sent.");
                        if(ownerRoute){if(facts!.OwnerWrite==null||!facts.OwnerWrite(previous))throw new InvalidOperationException("Owner restoration was refused.");}
                        else entry.BoxedValue=previous;
                        if(!string.Equals(TomlTypeConverter.ConvertToString(entry.BoxedValue,entry.SettingType),before,StringComparison.Ordinal))throw new InvalidOperationException("Previous serialized value was not restored.");
                        if(originalSave&&facts!=null&&EntryWritePolicy.MaySaveFile(facts.ConnectedAsClient,facts.FileHasSyncedEntries))entry.ConfigFile.Save();
                    }
                    catch(Exception restore){message+=" Previous value restoration failed: "+restore.GetBaseException().Message;}
                }
                string key=entry.ConfigFile.ConfigFilePath+"|"+entry.Definition+"|"+message;
                if(warned.Add(key))warning?.Invoke(message);
                return new NativeWriteResult(decision,false,false,assigned,message);
            }
            finally{entry.ConfigFile.SaveOnConfigSet=originalSave;}
        }
        void Notify(ConfirmedEntryWrite change)
        {
            if(Written==null)return;
            foreach(Action<ConfirmedEntryWrite> listener in Written.GetInvocationList())
                try{listener(change);}catch{ } // Observation must never turn an accepted write into a refusal.
        }
        // BepInEx logs SettingChanged exceptions instead of propagating them. Observe only errors
        // on this assignment's thread; never remove, replay or replace the mod's handlers.
        sealed class HandlerErrors:ILogListener
        {
            readonly int owner=Thread.CurrentThread.ManagedThreadId;
            internal Exception? Error { get; private set; }
            internal HandlerErrors()=>Logger.Listeners.Add(this);
            public void LogEvent(object sender,LogEventArgs args)
            {if(Thread.CurrentThread.ManagedThreadId==owner&&(args.Level&LogLevel.Error)!=0&&args.Data is Exception error)Error??=error;}
            public void Dispose()=>Logger.Listeners.Remove(this);
        }
    }
}
