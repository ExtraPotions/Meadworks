// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using BepInEx;
using BepInEx.Configuration;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Saga.Config;
using ExtraPotions.Saga.Integration;

namespace ExtraPotions.Saga.Runtime {
    public static partial class SagaConverter {
        static bool active;
        static int thread;
        static long lifecycle;
        static readonly ConditionalWeakTable<SagaImportPreview,ImportSnapshot> snapshots=new ConditionalWeakTable<SagaImportPreview,ImportSnapshot>();

        sealed class ImportSnapshot {
            internal readonly ConfigFile Owner=config;
            internal readonly string Source,SourceHash;
            internal readonly long Generation=ExtraPotions.Meadworks.Crucible.Plugin.Session.Generation;
            internal readonly long Lifecycle=lifecycle;
            internal readonly SessionRole Role=ExtraPotions.Meadworks.Crucible.Plugin.Session.Role;
            internal string TargetHash;
            internal bool Consumed;
            internal ImportSnapshot(string source){Source=source;SourceHash=FilesHash(SourcePaths(source));TargetHash=SagaConverter.TargetHash(this);}
        }
        static void CheckOwner(){
            if(!active||!Plugin.Ready||!ExtraPotions.Meadworks.Crucible.Plugin.IsReady
                ||System.Threading.Thread.CurrentThread.ManagedThreadId!=thread)
                throw new InvalidOperationException(SagaText.Text("import_authority"));
        }
        static IEnumerable<string> SourcePaths(string source){
            yield return Path.Combine(Paths.ConfigPath,identities[source]+".cfg");
            if(source=="GrindstoneSkills")foreach(var family in new[]{DataFamily.Finds,DataFamily.MineFinds,DataFamily.Forage,DataFamily.Snags})
                yield return Path.Combine(Paths.ConfigPath,"GrindstoneSkills."+family+".yml");
            if(source=="ProgressivePowers")foreach(var name in new[]{"Levels.yaml","Powers.yaml"})
                yield return Path.Combine(Paths.ConfigPath,"ProgressivePowers",name);
        }
        static IEnumerable<string> TargetPaths(string source){
            if(source=="GrindstoneSkills")foreach(var family in new[]{DataFamily.Finds,DataFamily.MineFinds,DataFamily.Forage,DataFamily.Snags})
                yield return Path.Combine(Paths.ConfigPath,SagaDataFiles.BaseName(family));
            if(source=="ProgressivePowers")foreach(var family in new[]{DataFamily.PowerLevels,DataFamily.Powers})
                yield return Path.Combine(Paths.ConfigPath,SagaDataFiles.BaseName(family));
        }
        static string Hash(byte[] bytes){using var sha=SHA256.Create();return BitConverter.ToString(sha.ComputeHash(bytes));}
        static string FileHash(string path){
            if(!File.Exists(path))return "missing";
            using var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
            if(input.Length>1024*1024)throw new InvalidDataException(SagaText.Text("import_source_large"));
            using var sha=SHA256.Create();return BitConverter.ToString(sha.ComputeHash(input));
        }
        static string FilesHash(IEnumerable<string> paths)=>Hash(Encoding.UTF8.GetBytes(string.Join("\n",
            paths.Select(Path.GetFullPath).OrderBy(path=>path,StringComparer.Ordinal).Select(path=>path.Length+":"+path+"="+FileHash(path)))));
        static string TargetHash(ImportSnapshot snapshot)=>Hash(Encoding.UTF8.GetBytes(FilesHash(TargetPaths(snapshot.Source).Append(config.ConfigFilePath))+"\n"+
            string.Join("\n",config.Keys.OrderBy(key=>key.Section,StringComparer.Ordinal).ThenBy(key=>key.Key,StringComparer.Ordinal)
                .Select(key=>key.Section.Length+":"+key.Section+key.Key.Length+":"+key.Key+"="+TomlTypeConverter.ConvertToString(config[key].BoxedValue,config[key].SettingType)))));
        static void Admission(SagaImportPreview preview,ImportSnapshot snapshot){
            CheckOwner();var session=ExtraPotions.Meadworks.Crucible.Plugin.Session;
            var decision=MeadworksAuthority.Decide(Plugin.Guid,ConfigScope.ServerAuthoritative);
            if(snapshot.Consumed||snapshot.Lifecycle!=lifecycle||!ReferenceEquals(config,snapshot.Owner)||preview.Source!=snapshot.Source
                ||session.Role==SessionRole.RemoteClient||session.Role!=snapshot.Role||session.Generation!=snapshot.Generation
                ||!decision.Allowed||decision.RequiresServerAmendment)
                throw new InvalidOperationException(SagaText.Text("import_authority"));
            Unchanged(preview,snapshot);
        }
        static void Unchanged(SagaImportPreview preview,ImportSnapshot snapshot){
            if(snapshot.SourceHash!=FilesHash(SourcePaths(snapshot.Source))||snapshot.TargetHash!=TargetHash(snapshot)
                ||preview.Sources.Any(item=>!File.Exists(item.Key)||File.ReadAllText(item.Key)!=item.Value))
                throw new InvalidOperationException(SagaText.Text("import_changed"));
        }

        sealed class ImportTarget:IValidatedConversionTarget {
            readonly SagaImportPreview preview;readonly ImportSnapshot snapshot;readonly IConversionTarget target;
            readonly Dictionary<string,object> previous=new Dictionary<string,object>(StringComparer.Ordinal);
            bool backupRequested;
            internal bool SavedLocally{get;private set;}
            internal bool PublicationFailed{get;private set;}
            internal ImportTarget(SagaImportPreview preview,ImportSnapshot snapshot){this.preview=preview;this.snapshot=snapshot;target=MeadworksConversion.Target(config);}
            public int SchemaVersion=>target.SchemaVersion;
            ConfigEntryBase[] Entries(IReadOnlyDictionary<string,object> values){
                var entries=new List<ConfigEntryBase>();
                foreach(var value in values){
                    var matches=config.Keys.Where(key=>key.Section+"."+key.Key==value.Key).Take(2).ToArray();
                    if(matches.Length!=1)throw new ArgumentException(SagaText.Text("import_wrong_source"));
                    var entry=config[matches[0]];
                    if(!MeadworksConfig.TryGetMetadata(entry,out var metadata)||metadata.ProductId!=Plugin.Guid
                        ||!metadata.Decision.Allowed||metadata.Decision.RequiresServerAmendment)
                        throw new InvalidOperationException(SagaText.Text("import_authority"));
                    var native=entry.SettingType.IsInstanceOfType(value.Value)?value.Value
                        :TomlTypeConverter.ConvertToValue(Convert.ToString(value.Value,CultureInfo.InvariantCulture),entry.SettingType);
                    if(entry.Description.AcceptableValues!=null&&!entry.Description.AcceptableValues.IsValid(native))
                        throw new ArgumentException(SagaText.Text("import_wrong_source"));
                    entries.Add(entry);
                }
                return entries.Distinct().ToArray();
            }
            public void Validate(IReadOnlyDictionary<string,object> values){
                if(SavedLocally)throw new InvalidOperationException(SagaText.Text("import_changed"));
                Admission(preview,snapshot);((IValidatedConversionTarget)target).Validate(values);Entries(values);
            }
            public void Backup(){
                Admission(preview,snapshot);
                if(!File.Exists(config.ConfigFilePath))throw new IOException(SagaText.Text("import_confirm"));
                backupRequested=true;
            }
            public void WriteAtomic(IReadOnlyDictionary<string,object> values){
                Validate(values);if(!backupRequested)throw new InvalidOperationException(SagaText.Text("import_confirm"));
                var entries=Entries(values);if(entries.Length==0)return;
                foreach(var entry in entries)previous.Add(entry.Definition.Section+"."+entry.Definition.Key,entry.BoxedValue);
                try {
                    var result=MeadworksConfig.AmendBatch(Plugin.Guid,entries,()=>{
                        Admission(preview,snapshot);Entries(values);target.Backup();target.WriteAtomic(values);SavedLocally=true;
                        snapshot.TargetHash=TargetHash(snapshot);
                    },"Saga import");
                    if(!result.Applied||!SavedLocally)throw new InvalidOperationException(SagaText.Text("import_authority"));
                } catch(Exception error)when(SavedLocally){
                    PublicationFailed=true;
                    throw new IOException(SagaText.Text("import_saved_local"),error);
                }
            }
            internal void Rollback(){
                Admission(preview,snapshot);var entries=Entries(previous);
                if(entries.Length==0)return;
                try {
                    var result=MeadworksConfig.AmendBatch(Plugin.Guid,entries,()=>{
                        Admission(preview,snapshot);Entries(previous);target.WriteAtomic(previous);snapshot.TargetHash=TargetHash(snapshot);
                    },"Saga import recovery");
                    if(!result.Applied)throw new InvalidOperationException(SagaText.Text("import_authority"));
                } catch(Exception error){
                    throw new IOException(SagaText.Text("import_recovery_incomplete"),error);
                }
            }
        }
    }
}
