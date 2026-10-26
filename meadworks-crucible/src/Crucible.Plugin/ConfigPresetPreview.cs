// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using BepInEx.Configuration;

namespace ExtraPotions.Meadworks.Crucible {
    public enum ConfigPresetFailure { Invalid, Stale, Locked, HostOnly, Write }

    public sealed class ConfigPresetException : InvalidOperationException {
        public ConfigPresetFailure Failure { get; }
        internal ConfigPresetException(ConfigPresetFailure failure,string detail) : base(detail){Failure=failure;}
    }

    public sealed class ConfigPresetRow {
        public ConfigEntryBase Entry { get; }
        public string Before { get; }
        public string After { get; }
        internal object Value { get; }
        internal object Previous { get; }
        internal ConfigPresetRow(ConfigEntryBase entry,object value){
            Entry=entry;Value=value;Previous=entry.BoxedValue;
            Before=TomlTypeConverter.ConvertToString(Previous,entry.SettingType);
            After=TomlTypeConverter.ConvertToString(value,entry.SettingType);
        }
    }

    public sealed class ConfigPresetResult {
        public bool Saved { get; }
        public bool Verified { get; }
        public bool Synchronized { get; }
        public Exception? CompletionError { get; }
        internal ConfigPresetResult(bool saved,bool verified,bool synchronized,Exception? error=null){Saved=saved;Verified=verified;Synchronized=synchronized;CompletionError=error;}
    }

    /// <summary>An explicit one-file, one-owner operation. Previewing never writes; applying never expands remote host values.</summary>
    public sealed class ConfigPresetPreview {
        [ThreadStatic] static bool applying;
        readonly ConfigFile file;
        readonly ConfigPresetRow[] rows;
        readonly long generation;
        readonly SessionRole role;
        readonly int thread;
        bool consumed;
        public string ProductId { get; }
        public ConfigScope Scope { get; }
        public IReadOnlyList<ConfigPresetRow> Rows { get; }

        ConfigPresetPreview(string product,ConfigScope scope,ConfigFile file,ConfigPresetRow[] rows){
            ProductId=product;Scope=scope;this.file=file;this.rows=rows;
            Rows=Array.AsReadOnly(rows);generation=Plugin.Session.Generation;role=Plugin.Session.Role;thread=Thread.CurrentThread.ManagedThreadId;
        }

        public static ConfigPresetPreview Capture(string productId,ConfigScope scope,ConfigFile file,IReadOnlyDictionary<ConfigDefinition,object> values){
            if(applying||!Plugin.IsReady||file==null||string.IsNullOrWhiteSpace(productId)||values==null||values.Count==0||values.Count>32
                ||scope!=ConfigScope.Local&&scope!=ConfigScope.ServerAuthoritative)
                throw Fault(ConfigPresetFailure.Invalid,"A preset needs one active owner, one scope and 1–32 exact values.");
            var rows=new List<ConfigPresetRow>();
            foreach(var value in values){
                if(value.Key==null||!file.ContainsKey(value.Key))throw Fault(ConfigPresetFailure.Invalid,"Unknown preset key.");
                var entry=file[value.Key];
                ValidateValue(entry,value.Value);
                rows.Add(new ConfigPresetRow(entry,value.Value));
            }
            var preview=new ConfigPresetPreview(productId,scope,file,rows.ToArray());preview.Validate(true);return preview;
        }

        static ConfigPresetException Fault(ConfigPresetFailure failure,string detail)=>new ConfigPresetException(failure,detail);
        static void ValidateValue(ConfigEntryBase entry,object value){
            var type=entry.SettingType;
            if(entry.GetType()!=typeof(ConfigEntry<>).MakeGenericType(type)||value==null||value.GetType()!=type||!(type.IsEnum||type==typeof(bool)||type==typeof(int)||type==typeof(float)||type==typeof(double)||type==typeof(string))
                ||type.IsEnum&&!Enum.IsDefined(type,value)
                ||value is float f&&(float.IsNaN(f)||float.IsInfinity(f))
                ||value is double d&&(double.IsNaN(d)||double.IsInfinity(d))
                ||entry.Description.AcceptableValues?.IsValid(value)==false)
                throw Fault(ConfigPresetFailure.Invalid,"Preset value has the wrong type or violates the current constraint.");
            string serialized=TomlTypeConverter.ConvertToString(value,type);
            object reconstructed=TomlTypeConverter.ConvertToValue(serialized,type);
            if(!Equals(value,reconstructed))throw Fault(ConfigPresetFailure.Invalid,"Preset value does not round-trip through the configuration serializer.");
        }

        void Validate(bool original){
            if(consumed||Thread.CurrentThread.ManagedThreadId!=thread||!Plugin.IsReady
                ||Plugin.Session.Generation!=generation||Plugin.Session.Role!=role)
                throw Fault(ConfigPresetFailure.Stale,"Preset preview no longer belongs to this session.");
            if(Scope==ConfigScope.ServerAuthoritative&&(!Plugin.Session.IsAuthority||!Plugin.Session.AuthorityReady))
                throw Fault(ConfigPresetFailure.HostOnly,"Host presets must execute on their authoritative host.");
            foreach(var row in rows){
                var entry=row.Entry;
                if(!ReferenceEquals(entry.ConfigFile,file)||!file.ContainsKey(entry.Definition)||!ReferenceEquals(file[entry.Definition],entry))
                    throw Fault(ConfigPresetFailure.Stale,"A previewed entry was replaced.");
                if(!MeadworksConfig.TryGetMetadata(entry,out var metadata)||metadata.ProductId!=ProductId||metadata.Scope!=Scope||metadata.Visibility!=ConfigVisibility.Shown)
                    throw Fault(ConfigPresetFailure.Invalid,"Preset entry owner, visibility or scope changed.");
                var decision=metadata.Decision;
                if(!decision.Allowed||decision.RequiresServerAmendment||decision.Source!=ConfigValueSource.Own)
                    throw Fault(ConfigPresetFailure.Locked,"Preset entry is locked or carries a server overlay.");
                ValidateValue(entry,row.Value);
                string actual=TomlTypeConverter.ConvertToString(entry.BoxedValue,entry.SettingType);
                if(actual!=(original?row.Before:row.After))throw Fault(ConfigPresetFailure.Stale,"A previewed value changed.");
            }
        }

        public ConfigPresetResult Apply(){
            if(applying)throw Fault(ConfigPresetFailure.Invalid,"A preset operation is already applying.");
            Validate(true);bool saved=false,verified=false;bool autoSave=file.SaveOnConfigSet;
            applying=true;
            try{
                var result=MeadworksConfig.AmendBatch(ProductId,rows.Select(row=>row.Entry).ToArray(),()=>{
                    Validate(true);
                    file.SaveOnConfigSet=false;
                    try{
                        foreach(var row in rows)row.Entry.BoxedValue=row.Value;
                        Validate(false);
                        MeadworksConfig.PersistPresetEntries(rows.Select(row=>row.Entry).ToArray());
                        saved=true;verified=true;
                    }catch(PresetPublicationException){saved=true;throw;}
                    catch(Exception error){
                        var failures=new List<Exception>{error};
                        for(int index=rows.Length-1;index>=0;index--)try{rows[index].Entry.BoxedValue=rows[index].Previous;}catch(Exception restore){failures.Add(restore);}
                        if(failures.Count>1)throw new AggregateException("Preset failed and one or more RAM values could not be restored.",failures);
                        throw;
                    }finally{file.SaveOnConfigSet=autoSave;}
                });
                if(!result.Applied)throw Fault(ConfigPresetFailure.Locked,"Preset batch was refused before its write.");
                return new ConfigPresetResult(saved,verified,true);
            }catch(Exception error) when(saved){
                // A transport failure after durable publication cannot truthfully be reported as an unapplied edit.
                return new ConfigPresetResult(true,verified,false,error);
            }finally{applying=false;consumed=true;file.SaveOnConfigSet=autoSave;}
        }
    }
}
