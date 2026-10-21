// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// ConfigDrawers 1.1.1 native bind conventions only; no manager implementation is copied.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using BepInEx;
using BepInEx.Configuration;
using ExtraPotions.Almanac.Runtime.Editing;
using ExtraPotions.Almanac.Writing;
using ExtraPotions.Meadworks.Crucible;

namespace ExtraPotions.Almanac.Runtime.Utilities
{
    public sealed class DrawerImportPreview
    {
        internal DrawerImportPreview(ConfigDrawersPreferences owner,string path,string hash,ConversionPreview preview,IReadOnlyDictionary<string,string> current)
        {Owner=owner;SourcePath=path;SourceSha256=hash;Preview=preview;Current=current;}
        internal ConfigDrawersPreferences Owner { get; }
        public string SourcePath { get; }
        public string SourceSha256 { get; }
        public ConversionPreview Preview { get; }
        public IReadOnlyDictionary<string,string> Current { get; }
    }
    public sealed class DrawerImportResult
    {
        internal DrawerImportResult(ConversionResult result,string? backup,string hash){Result=result;BackupPath=backup;SourceSha256=hash;}
        public ConversionResult Result { get; }
        public string? BackupPath { get; }
        public string SourceSha256 { get; }
    }
    /// <summary>C09 import into the actual five local Almanac preference entries supplied by the window integration.</summary>
    public sealed class ConfigDrawersPreferences
    {
        const string Source="ConfigDrawers",SourceGuid="vapok.bepinex.configdrawers";
        readonly ConfigFile target;
        readonly Dictionary<string,ConfigEntryBase> entries;
        readonly Func<ConfigEntryBase,NativeWriteFacts> resolve;
        readonly ConfigConverter converter;
        readonly EntryWriter writer;
        readonly int thread=Thread.CurrentThread.ManagedThreadId;
        public ConfigDrawersPreferences(ConfigEntryBase dock,ConfigEntryBase width,ConfigEntryBase opacity,ConfigEntryBase showAdvanced,ConfigEntryBase watchFiles,Func<ConfigEntryBase,NativeWriteFacts> resolve,EntryWriter? writer=null)
        {
            this.resolve=resolve??throw new ArgumentNullException(nameof(resolve));var all=new[]{dock,width,opacity,showAdvanced,watchFiles};
            this.writer=writer??new EntryWriter(resolve);
            if(all.Any(entry=>entry==null)||all.Any(entry=>!ReferenceEquals(entry.ConfigFile,dock.ConfigFile)))throw new ArgumentException("Preference targets must share the actual native config file.");
            target=dock.ConfigFile;entries=all.ToDictionary(Id,StringComparer.Ordinal);
            converter=new ConfigConverter(new ConversionMapping(1,Source,0,new[]{
                new ConversionRule(Source+".Interface.Default Dock Position",Id(dock),"DockSide","Left,Right,Floating"),
                new ConversionRule(Source+".Interface.Drawer Width",Id(width),"Single","From 360 to 720"),
                new ConversionRule(Source+".Interface.Window Opacity",Id(opacity),"Single","From 0.2 to 1"),
                new ConversionRule(Source+".Interface.Hide Advanced Settings",Id(showAdvanced),"Boolean"),
                new ConversionRule(Source+".General.Watch Config Files",Id(watchFiles),"Boolean")
            }));
            SourcePath=Path.Combine(Paths.ConfigPath,SourceGuid+".cfg");
        }
        /// <summary>Explicit source file selection is read-only, including a different mod-manager profile.</summary>
        public string SourcePath { get; set; }
        public IDisposable Register()=>MeadworksConversion.Register(new ConversionRegistration("almanac.configdrawers",AlmanacCore.Guid,SourceGuid,"BepInEx ConfigDrawers",()=>File.Exists(SourcePath),()=>Preview(),(plan,choices,backup)=>
        {if(!backup)throw new InvalidOperationException("Preference import requires a backup.");return Apply(plan as DrawerImportPreview??throw new ArgumentException("Wrong preview type."),choices);}));
        public DrawerImportPreview Preview()
        {
            Check();string path=Path.GetFullPath(SourcePath),hash=Hash(path);
            var source=MeadworksConversion.ReadSourceConfig(path,Source).Select(pair=>{
                if(pair.Key==Source+".Interface.Hide Advanced Settings"&&bool.TryParse(pair.Value.Trim(),out var hide))return new KeyValuePair<string,string>(pair.Key,(!hide).ToString());
                return pair;
            }).ToArray();
            var preview=converter.Preview(source);return new DrawerImportPreview(this,path,hash,preview,entries.ToDictionary(pair=>pair.Key,pair=>new NativeEntry(pair.Value).RawSerialized(),StringComparer.Ordinal));
        }
        public DrawerImportResult Apply(DrawerImportPreview preview,IReadOnlyDictionary<string,string> choices)
        {
            Check();if(preview==null||!ReferenceEquals(preview.Owner,this))throw new ArgumentException("Preview belongs to another preference importer.",nameof(preview));
            if(!string.Equals(Path.GetFullPath(SourcePath),preview.SourcePath,StringComparison.Ordinal)||Hash(preview.SourcePath)!=preview.SourceSha256)throw new InvalidOperationException("Source preferences changed; preview again.");
            foreach(var pair in preview.Current)if(new NativeEntry(entries[pair.Key]).RawSerialized()!=pair.Value)throw new InvalidOperationException("Target preferences changed; preview again.");
            var guarded=new PreferenceTarget(this);var result=converter.Apply(preview.Preview,guarded,choices??throw new ArgumentNullException(nameof(choices)));
            return new DrawerImportResult(result,guarded.BackupPath,preview.SourceSha256);
        }
        void Validate(IReadOnlyDictionary<string,object> values)
        {
            foreach(var pair in values)
            {
                if(!entries.TryGetValue(pair.Key,out var entry)||!ReferenceEquals(target[entry.Definition],entry))throw new InvalidOperationException("Preference target identity changed.");
                if(!MeadworksConfig.TryGetMetadata(entry,out var metadata)||metadata.ProductId!=AlmanacCore.Guid||metadata.Scope!=ConfigScope.Local)throw new InvalidOperationException("Preference import requires Almanac's actual local entries.");
                var facts=resolve(entry);var native=new NativeEntry(entry);var attributes=native.Attributes;
                if(!facts.Known||!facts.Editable||facts.EntrySynced||!EntryWritePolicy.MaySaveFile(facts.ConnectedAsClient,facts.FileHasSyncedEntries)
                    ||native.Tags.Errors.Count!=0||attributes.ReadOnly==true||attributes.CharterReadOnly||attributes.GenericReadOnly)throw new InvalidOperationException("Preference import is locked.");
                object value=entry.SettingType.IsInstanceOfType(pair.Value)?pair.Value:TomlTypeConverter.ConvertToValue(Convert.ToString(pair.Value,CultureInfo.InvariantCulture),entry.SettingType);
                var constraint=entry.Description.AcceptableValues;if(constraint!=null&&!constraint.IsValid(value))throw new ArgumentException("Imported preference violates the native constraint.");
            }
        }
        sealed class PreferenceTarget:IValidatedConversionTarget
        {
            readonly ConfigDrawersPreferences owner;readonly IConversionTarget target;
            internal PreferenceTarget(ConfigDrawersPreferences owner){this.owner=owner;target=MeadworksConversion.Target(owner.target);}
            public string? BackupPath { get; private set; }
            public int SchemaVersion=>target.SchemaVersion;
            public void Validate(IReadOnlyDictionary<string,object> values){owner.Validate(values);((IValidatedConversionTarget)target).Validate(values);}
            public void Backup()
            {
                string path=owner.target.ConfigFilePath,folder=Path.GetDirectoryName(Path.GetFullPath(path))!,pattern=Path.GetFileName(path)+".import-*.bak";
                var before=new HashSet<string>(Directory.GetFiles(folder,pattern),StringComparer.Ordinal);target.Backup();BackupPath=Directory.GetFiles(folder,pattern).SingleOrDefault(file=>!before.Contains(file));
            }
            public void WriteAtomic(IReadOnlyDictionary<string,object> values){
                Validate(values);bool save=owner.target.SaveOnConfigSet;owner.target.SaveOnConfigSet=false;
                var changed=new List<KeyValuePair<ConfigEntryBase,string>>();
                try{
                    foreach(var pair in values){var entry=owner.entries[pair.Key];var native=new NativeEntry(entry);string before=native.RawSerialized();
                        object value=entry.SettingType.IsInstanceOfType(pair.Value)?pair.Value:TomlTypeConverter.ConvertToValue(Convert.ToString(pair.Value,CultureInfo.InvariantCulture),entry.SettingType);
                        string text=TomlTypeConverter.ConvertToString(value,entry.SettingType);var result=owner.writer.TryWrite(entry,text);
                        if(!result.Accepted||result.PendingAuthority)throw new InvalidOperationException(result.Error??"Preference write refused.");
                        if(before!=new NativeEntry(entry).RawSerialized())changed.Add(new KeyValuePair<ConfigEntryBase,string>(entry,before));
                    }
                    // The proven C09 serializer publishes current native values atomically; no config text is replaced by Almanac.
                    target.WriteAtomic(new Dictionary<string,object>());
                }catch(Exception error){var failures=new List<string>();foreach(var pair in Enumerable.Reverse(changed)){var result=owner.writer.TryWrite(pair.Key,pair.Value);if(!result.Accepted)failures.Add(result.Error??pair.Key.Definition.ToString());}
                    if(failures.Count>0)throw new InvalidOperationException(error.GetBaseException().Message+"; restore refused: "+string.Join("; ",failures),error);throw;
                }finally{owner.target.SaveOnConfigSet=save;}
            }
        }
        static string Id(ConfigEntryBase entry)=>entry.Definition.Section+"."+entry.Definition.Key;
        static string Hash(string path)
        {using(var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read)){if(input.Length>1024*1024)throw new IOException("Source preferences exceed 1 MiB.");using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(input)).Replace("-","");}}
        void Check(){if(Thread.CurrentThread.ManagedThreadId!=thread)throw new InvalidOperationException("Preference imports must run on their owning thread.");}
    }
}
