// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using BepInEx.Configuration;
using ExtraPotions.Meadworks.Crucible;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Wildroot.Runtime {
 // Reuses Timber/Keel's checked conversion target and existing amendment publication boundary.
 // Core ReferenceImport keeps its mapping, conflict selection and final shortcut validation.
 internal sealed class ReferenceImportTarget : IValidatedConversionTarget {
  readonly ConfigFile config;
  readonly IConversionTarget target;
  readonly string[] sources;
  readonly string sourceHash,targetHash;
  readonly long generation;
  readonly SessionRole role;
  bool backupRequested,written;
  internal bool SavedLocally=>written;

  internal ReferenceImportTarget(ConfigFile config,IEnumerable<string> sourcePaths){
   this.config=config??throw new ArgumentNullException(nameof(config));
   target=MeadworksConversion.Target(config);
   sources=sourcePaths.Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
   generation=CruciblePlugin.Session.Generation;role=WildrootConfig.Role;
   sourceHash=SourceHash();targetHash=TargetHash();
  }
  public int SchemaVersion=>target.SchemaVersion;

  static string Read(string path){
   if(!File.Exists(path))return "missing";
   using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
   if(stream.Length>1024*1024)throw new IOException("Import configuration exceeds 1 MiB.");
   using var sha=SHA256.Create();
   return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","");
  }
  static string Hash(string text){using var sha=SHA256.Create();return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-","");}
  string SourceHash()=>Hash(string.Join("\n",sources.Select(path=>path+"="+Read(path))));
  string TargetHash()=>Hash(Read(config.ConfigFilePath)+"\n"+string.Join("\n",config.Keys.OrderBy(k=>k.Section,StringComparer.Ordinal).ThenBy(k=>k.Key,StringComparer.Ordinal)
   .Select(k=>k.Section+"."+k.Key+"="+TomlTypeConverter.ConvertToString(config[k].BoxedValue,config[k].SettingType))));
  ConfigEntryBase Entry(string id){
   var matches=config.Keys.Where(k=>k.Section+"."+k.Key==id).Take(2).ToArray();
   if(matches.Length!=1)throw new ArgumentException("Unknown or ambiguous Wildroot import target: "+id);
   return config[matches[0]];
  }
  void Admission(){
   if(written)throw new InvalidOperationException("This Wildroot import has already been applied.");
   if(!CruciblePlugin.IsReady||!ReferenceImport.Allowed(WildrootConfig.Role)||role!=WildrootConfig.Role
    ||generation!=CruciblePlugin.Session.Generation||sourceHash!=SourceHash()||targetHash!=TargetHash())
    throw new InvalidOperationException("Import source, target or session changed; request the import again.");
  }
  ConfigEntryBase[] Entries(IReadOnlyDictionary<string,object> values){
   var entries=new List<ConfigEntryBase>();
   foreach(var pair in values){
    var entry=Entry(pair.Key);
    if(!MeadworksConfig.TryGetMetadata(entry,out var metadata)||metadata.ProductId!=WildrootSettings.ProductId
     ||!metadata.Decision.Allowed||metadata.Decision.RequiresServerAmendment)
     throw new InvalidOperationException("Import on the authority machine while this setting is editable: "+pair.Key);
    object value=entry.SettingType.IsInstanceOfType(pair.Value)?pair.Value:TomlTypeConverter.ConvertToValue(Convert.ToString(pair.Value,CultureInfo.InvariantCulture),entry.SettingType);
    if(entry.Description.AcceptableValues!=null&&!entry.Description.AcceptableValues.IsValid(value))
     throw new ArgumentException("Invalid Wildroot import target value: "+pair.Key);
    entries.Add(entry);
   }
   return entries.Distinct().ToArray();
  }
  public void Validate(IReadOnlyDictionary<string,object> values){
   Admission();((IValidatedConversionTarget)target).Validate(values);Entries(values);
  }
  public void Backup(){
   Admission();
   if(!File.Exists(config.ConfigFilePath))throw new IOException("Save Wildroot configuration before importing.");
   // Defer the actual backup until the batch admits the exact final targets and synchronization is available.
   backupRequested=true;
  }
  public void WriteAtomic(IReadOnlyDictionary<string,object> values){
   Validate(values);
   if(!backupRequested)throw new InvalidOperationException("Wildroot import requires a backup.");
   var entries=Entries(values);
   if(entries.Length==0)return;
   try{
    var amended=MeadworksConfig.AmendBatch(WildrootSettings.ProductId,entries,()=>{
     Admission();Entries(values);
     target.Backup();target.WriteAtomic(values);written=true;
    },"Wildroot");
    if(!amended.Applied||!written)throw new InvalidOperationException("Current authority or synchronization refused the Wildroot import.");
   }catch(Exception error)when(written){
    // Core's conversion refusal means no write; a later publication failure must preserve the actual saved outcome.
    throw new IOException("Wildroot settings were saved locally, but synchronization did not complete.",error);
   }
  }
 }
}
