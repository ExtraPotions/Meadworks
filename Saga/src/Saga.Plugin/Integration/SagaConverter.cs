// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Saga.Config;
using ExtraPotions.Saga.Conversion;
using ExtraPotions.Saga.Integration;
using ExtraPotions.Saga.Powers;
using ExtraPotions.Saga.Runtime.Adopted.Grindstone;
using HarmonyLib;
namespace ExtraPotions.Saga.Runtime {
    public sealed class SagaImportPreview {
        public ConversionPreview Config {get;internal set;}=null!;
        public IReadOnlyDictionary<string,string> Yaml {get;internal set;}=null!;
        public IReadOnlyList<string> Warnings {get;internal set;}=null!;
        internal ConfigConverter Converter=null!;internal readonly Dictionary<string,string> Sources=new Dictionary<string,string>();internal string Source="";
    }
    public static partial class SagaConverter {
        static ConfigFile config=null!;static readonly List<IDisposable> leases=new List<IDisposable>();
        static readonly Dictionary<string,string> identities=new Dictionary<string,string>{{"ProgressivePowers",SourceOverlap.Powers},{"SNEAKer",SourceOverlap.Sneaker},{"SecondaryAttacks",SourceOverlap.Secondary},{"GrindstoneSkills",SourceOverlap.Grindstone}};
        public static void Initialize(ConfigFile target){config=target;active=true;lifecycle++;thread=System.Threading.Thread.CurrentThread.ManagedThreadId;foreach(var item in identities){var source=item.Key;leases.Add(MeadworksConversion.Register(new ConversionRegistration("saga."+source.ToLowerInvariant(),Plugin.Guid,item.Value,source,()=>File.Exists(Path.Combine(Paths.ConfigPath,identities[source]+".cfg")),()=>Preview(source),(preview,choices,confirmed)=>Apply(source,preview,choices,confirmed))));}}
        static string Read(SagaImportPreview preview,string path){var info=new FileInfo(path);if(!info.Exists)return "";if(info.Length>1024*1024)throw new InvalidDataException(SagaText.Text("import_source_large"));var text=File.ReadAllText(info.FullName);preview.Sources.Add(info.FullName,text);return text;}
        static SagaImportPreview Preview(string source){
            CheckOwner();var snapshot=new ImportSnapshot(source);var preview=new SagaImportPreview{Source=source};var path=Path.Combine(Paths.ConfigPath,identities[source]+".cfg");var settings=CfgText.Read(Read(preview,path),source);var rules=new List<ConversionRule>();var values=new List<KeyValuePair<string,string>>();var warnings=new List<string>();var yaml=new Dictionary<string,string>();
            foreach(var item in settings){var identity=item.Key.Substring(source.Length+1);var split=identity.LastIndexOf('.');if(split<0)continue;var section=identity.Substring(0,split);var key=identity.Substring(split+1);var target=SagaImportRules.Target(source,section,key);
                if(target==null){values.Add(item);continue;}
                var constraints=target.Minimum!=null?SagaText.Format("import_range",Convert.ToString(target.Minimum,CultureInfo.InvariantCulture),Convert.ToString(target.Maximum,CultureInfo.InvariantCulture)):string.Join(",",target.Choices);
                rules.Add(new ConversionRule(item.Key,target.Id,target.ValueType.IsEnum?target.ValueType.Name:target.ValueType.Name,constraints));
                values.Add(new KeyValuePair<string,string>(item.Key,SagaImportRules.Normalize(source,target,item.Value,Player.m_localPlayer?.m_crouchSpeed??2f)));
            }
            if(source=="GrindstoneSkills")foreach(var family in new[]{DataFamily.Finds,DataFamily.MineFinds,DataFamily.Forage,DataFamily.Snags}){
                var sourcePath=Path.Combine(Paths.ConfigPath,"GrindstoneSkills."+family+".yml");if(File.Exists(sourcePath))yaml.Add(SagaDataFiles.BaseName(family),Read(preview,sourcePath));
            }
            if(source=="ProgressivePowers"){
                foreach(var pair in new[]{Tuple.Create("Levels.yaml",DataFamily.PowerLevels,"Level Requirements"),Tuple.Create("Powers.yaml",DataFamily.Powers,"Advanced Powers")}){
                    var sourcePath=Path.Combine(Paths.ConfigPath,"ProgressivePowers",pair.Item1);var text=File.Exists(sourcePath)?Read(preview,sourcePath):settings.FirstOrDefault(p=>p.Key==source+".General."+pair.Item3).Value;
                    if(!string.IsNullOrEmpty(text))yaml.Add(SagaDataFiles.BaseName(pair.Item2),text.Replace("\\r\\n","\n").Replace("\\n","\n"));
                }
                warnings.Add(SagaText.Text("import_unsupported_powers"));
            }
            if(source=="SNEAKer"&&settings.Any(p=>p.Key.EndsWith(".Enable Exp Multiplier")&&p.Value=="On"))warnings.Add(SagaText.Text("import_sneak_larder"));
            foreach(var file in yaml){var family=SagaDataFiles.Classify(file.Key)!.Value;var errors=NativeSaga.Data.ValidateImport(family,file.Value);if(errors.Count>0)throw new InvalidDataException(string.Join("; ",errors));}
            preview.Converter=new ConfigConverter(new ConversionMapping(1,source,0,rules));preview.Config=preview.Converter.Preview(values);preview.Yaml=new System.Collections.ObjectModel.ReadOnlyDictionary<string,string>(yaml);preview.Warnings=warnings.AsReadOnly();Unchanged(preview,snapshot);snapshots.Add(preview,snapshot);return preview;
        }
        static object Apply(string source,object raw,IReadOnlyDictionary<string,string> choices,bool confirmed){
            if(!confirmed)throw new InvalidOperationException(SagaText.Text("import_confirm"));
            if(!(raw is SagaImportPreview preview)||preview.Source!=source)throw new ArgumentException(SagaText.Text("import_wrong_source"));
            if(!snapshots.TryGetValue(preview,out var snapshot))throw new ArgumentException(SagaText.Text("import_wrong_source"));
            Admission(preview,snapshot);
            foreach(var file in preview.Yaml){var errors=NativeSaga.Data.ValidateImport(SagaDataFiles.Classify(file.Key)!.Value,file.Value);if(errors.Count>0)throw new InvalidDataException(string.Join("; ",errors));}
            if(preview.Config.Errors.Count>0)throw new InvalidDataException(string.Join("; ",preview.Config.Errors));
            var target=new ImportTarget(preview,snapshot);var backups=new Dictionary<string,string?>();var staged=new Dictionary<string,string>();
            var committed=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try {
                foreach(var item in preview.Yaml){var destination=Path.GetFullPath(Path.Combine(Paths.ConfigPath,item.Key));backups[destination]=File.Exists(destination)?File.ReadAllText(destination):null;var temporary=destination+".import-"+System.Guid.NewGuid().ToString("N")+".tmp";File.WriteAllText(temporary,item.Value);staged.Add(destination,temporary);}
                var result=preview.Converter.Apply(preview.Config,target,choices);
                foreach(var item in staged){Admission(preview,snapshot);if(File.Exists(item.Key)){File.Copy(item.Key,item.Key+".import-"+DateTime.UtcNow.Ticks+".bak",false);File.Replace(item.Value,item.Key,null);}else File.Move(item.Value,item.Key);committed.Add(item.Key);snapshot.TargetHash=TargetHash(snapshot);}
                Admission(preview,snapshot);var marker=(ConfigEntry<string>)Plugin.Settings!.Entry(SagaSettings.Import.ImportedSources);marker.Value=string.Join(",",marker.Value.Split(',').Where(v=>v.Length>0).Concat(new[]{source}).Distinct());snapshot.Consumed=true;return result;
            } catch(Exception error) {
                if(target.PublicationFailed){snapshot.Consumed=true;throw;}
                try {
                    if(target.SavedLocally)target.Rollback();
                    foreach(var item in backups.Where(item=>committed.Contains(item.Key))){Admission(preview,snapshot);if(item.Value==null){if(File.Exists(item.Key))File.Delete(item.Key);}else File.WriteAllText(item.Key,item.Value);snapshot.TargetHash=TargetHash(snapshot);}
                } catch(Exception recovery) {snapshot.Consumed=true;throw new IOException(SagaText.Text("import_recovery_incomplete"),new AggregateException(error,recovery));}
                if(target.SavedLocally||committed.Count>0)snapshot.Consumed=true;
                throw;
            }
            finally{foreach(var temporary in staged.Values)if(File.Exists(temporary))File.Delete(temporary);}
        }
        [HarmonyPatch(typeof(Player),nameof(Player.Load))]static class CharacterImport {
            static void Postfix(Player __instance){if(__instance==Player.m_localPlayer)ImportCharacter(__instance);}
        }
        public static void ImportCharacter(Player __instance){if(__instance!=Player.m_localPlayer||__instance.m_customData.ContainsKey("saga.imported.discoveries"))return;
                foreach(var pair in SagaImportRules.CharacterKeys)if(!__instance.m_customData.ContainsKey(pair.Value)&&__instance.m_customData.TryGetValue(pair.Key,out var data))__instance.m_customData[pair.Value]=data;
                __instance.m_customData["saga.imported.discoveries"]="1";
        }
        public static void Shutdown(){active=false;lifecycle++;foreach(var lease in leases)lease.Dispose();leases.Clear();}
    }
}
