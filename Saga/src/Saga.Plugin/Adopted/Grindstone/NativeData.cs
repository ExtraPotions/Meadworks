// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using ExtraPotions.Saga.Config;
using ExtraPotions.Meadworks.Crucible;
using UnityEngine;
using YamlConfig;
using ExtraPotions.Saga.Powers;
using ExtraPotions.Saga.Conversion;
namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone {
    public sealed class YamlFileSet {
        public readonly string Pattern,SyncKey;public readonly Func<YamlModel> Create;public readonly Action<YamlModel> Apply;
        public string DefaultContent="";
        public YamlFileSet(string pattern,string key,Func<YamlModel> create,Action<YamlModel> apply){Pattern=pattern;SyncKey=key;Create=create;Apply=apply;}
    }
    public sealed class NativeData {
        sealed class Entry {public YamlFileSet Set=null!;public DataFamily Family;public string Last="",AuthorityAttempt="",NetworkAttempt="",LastError="";public bool AuthorityValid;public int ReadGeneration;}
        readonly List<Entry> entries=new List<Entry>();float nextPoll;
        // Files are listed and read only after the watch saw a change (or on the first read); otherwise the cached packet is used.
        SagaDataWatch? watch;public int Reads;
        public void AddPowers(){var defaults=new System.Text.StringBuilder();foreach(Boss boss in Enum.GetValues(typeof(Boss))){defaults.Append(PowerCatalog.Name(boss)).Append(":\n");for(int level=1;level<=7;level++){var required=PowerRequirements.Defaults.Describe(boss,level).Split(' ');defaults.Append("  ").Append(level).Append(": { ").Append(required[0]).Append(": ").Append(required[1]).Append(" }\n");}}
            Add(new YamlFileSet("Saga.PowerLevels*.yml","saga.power_levels",()=>null!,_=>{}){DefaultContent=defaults.ToString()});Add(new YamlFileSet("Saga.Powers*.yml","saga.powers",()=>null!,_=>{}){DefaultContent="# Advanced definitions. Powers omitted here use Saga feature settings.\n"});}
        public void Add(YamlFileSet set) {
            if(!Enum.TryParse(set.Pattern.Substring("Saga.".Length).Split('*')[0],out DataFamily family))throw new ArgumentException("Unknown Saga YAML family");
            entries.Add(new Entry{Set=set,Family=family});watch?.Dispose();watch=null;
        }
        public IReadOnlyList<string> ValidateImport(DataFamily family,string text){var entry=entries.FirstOrDefault(e=>e.Family==family)??throw new ArgumentException("Unknown family");
            if(family==DataFamily.PowerLevels){PowerRequirements.Parse(text,NativePowers.BossNames,out var errors);return errors;}if(family==DataFamily.Powers){PowerDefinitions.Parse(text,out var errors);return errors;}
            var model=entry.Set.Create();model.Load(text);return model.Errors;
        }
        public void Tick() {
            if(Time.unscaledTime<nextPoll||Plugin.Settings==null)return;nextPoll=Time.unscaledTime+2;
            if(watch==null){watch=new SagaDataWatch(Path.GetFullPath(Paths.ConfigPath),entries.Select(e=>(e.Family,e.Set.Pattern)));watch.Start();}
            var generation=watch.Generation;var settled=watch.Settled(Environment.TickCount);
            foreach(var entry in entries)try{Poll(entry,generation,settled);entry.LastError="";}catch(Exception error){var signature=error.GetType().Name+":"+error.Message;if(entry.LastError!=signature){entry.LastError=signature;NativeSaga.Report(error,"YAML "+entry.Family);}}
        }
        void Poll(Entry entry,int generation,bool settled) {
            var setting=SagaDataFiles.Carrier(entry.Family);
            var decision=MeadworksAuthority.Decide(Plugin.Guid,ConfigScope.ServerAuthoritative);
            if(!decision.ProductActive)return;
            if(decision.Allowed&&!decision.RequiresServerAmendment) {
                if(entry.ReadGeneration!=generation&&(settled||entry.ReadGeneration==0)){
                    var root=Path.GetFullPath(Paths.ConfigPath);var basePath=Path.Combine(root,SagaDataFiles.BaseName(entry.Family));
                    if(!File.Exists(basePath))File.WriteAllText(basePath,entry.Set.DefaultContent);
                    var files=SagaDataFiles.Layers(entry.Family,Directory.GetFiles(root,entry.Set.Pattern));
                    if(files.Sum(p=>new FileInfo(p).Length)>1024*1024)throw new InvalidDataException("YAML family exceeds 1 MiB");
                    var texts=files.Select(p=>new KeyValuePair<string,string>(Path.GetFileName(p),File.ReadAllText(p))).ToArray();
                    var packet=Encode(texts.Select(p=>p.Value));
                    if(packet!=entry.AuthorityAttempt){entry.AuthorityAttempt=packet;entry.AuthorityValid=Validate(entry,texts,out _);}
                    Reads++;entry.ReadGeneration=generation;
                }
                if(!entry.AuthorityValid)return;
                var carrier=(ConfigEntry<string>)Plugin.Settings!.Entry(setting);
                // Amend publishes from a host or dedicated server; a plain write would stay on the server (Jotunn sends only published changes).
                if(carrier.Value!=entry.AuthorityAttempt)MeadworksConfig.Amend(carrier,Plugin.Guid,ConfigScope.ServerAuthoritative,entry.AuthorityAttempt);
            }
            var current=Plugin.Settings!.Get(setting);if(current==entry.Last||current==entry.NetworkAttempt||current.Length==0)return;entry.NetworkAttempt=current;
            if(!Validate(entry,Decode(current).Select((text,index)=>new KeyValuePair<string,string>(index.ToString(),text)),out var candidate))return;
            if(candidate is PowerRequirements levels)NativePowers.Requirements=levels;else if(candidate is PowerDefinitions powers)NativePowers.Definitions=powers;else entry.Set.Apply((YamlModel)candidate);entry.Last=current;
        }
        static bool Validate(Entry entry,IEnumerable<KeyValuePair<string,string>> texts,out object candidate){
            if(entry.Family==DataFamily.PowerLevels||entry.Family==DataFamily.Powers){var merged=MiniYaml.Merge(texts.Select(t=>t.Value),out var errors);candidate=entry.Family==DataFamily.PowerLevels?(object)PowerRequirements.Parse(merged,NativePowers.BossNames,out var semantic):PowerDefinitions.Parse(merged,out semantic);errors.AddRange(semantic);if(errors.Count==0)return true;foreach(var error in errors)NativeSaga.Log.LogWarning(entry.Family+": "+error);return false;}
            var model=entry.Set.Create();model.LoadAll(texts);candidate=model;if(model.Errors.Count==0)return true;foreach(var error in model.Errors)NativeSaga.Log.LogWarning(error);return false;
        }
        // Length-independent framing preserves layer boundaries and avoids YAML anchors crossing files.
        public static string Encode(IEnumerable<string> layers)=>YamlLayers.Encode(layers);
        public static IEnumerable<string> Decode(string packet)=>YamlLayers.Decode(packet);
        public void Invalidate(){foreach(var entry in entries){entry.AuthorityAttempt="";entry.NetworkAttempt="";entry.ReadGeneration=0;}nextPoll=0;}
        public void Clear(){watch?.Dispose();watch=null;entries.Clear();nextPoll=0;}
    }
}
