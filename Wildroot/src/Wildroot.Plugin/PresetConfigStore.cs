// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx.Configuration;
namespace ExtraPotions.Wildroot.Runtime {
 /// <summary>Stages only this group's own values over disk, so a client's unrelated effective server settings never enter its saved preferences.</summary>
 internal static class PresetConfigStore {
  internal static void Write(ConfigFile file,IReadOnlyDictionary<string,object> values,Func<string,ConfigEntryBase?> find){
   string destination=Path.GetFullPath(file.ConfigFilePath),temporary=destination+".preset-"+Guid.NewGuid().ToString("N")+".tmp";
   var entries=values.Keys.ToDictionary(id=>id,id=>find(id)??throw new ArgumentException("Unknown preset member: "+id));
   var previous=entries.ToDictionary(pair=>pair.Key,pair=>pair.Value.BoxedValue);
   bool save=file.SaveOnConfigSet,reserved=false;file.SaveOnConfigSet=false;
   try{
    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
    byte[]? before=File.Exists(destination)?File.ReadAllBytes(destination):null;
    using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None)){reserved=true;if(before!=null)stream.Write(before,0,before.Length);}
    var candidate=new ConfigFile(temporary,false){SaveOnConfigSet=false};
    var bind=typeof(ConfigFile).GetMethods().Single(method=>method.Name=="Bind"&&method.IsGenericMethodDefinition&&method.GetParameters().Length==3&&method.GetParameters()[0].ParameterType==typeof(ConfigDefinition));
    foreach(var pair in values){
     var entry=entries[pair.Key];
     var copy=(ConfigEntryBase)bind.MakeGenericMethod(entry.SettingType).Invoke(candidate,new object[]{entry.Definition,entry.DefaultValue,entry.Description})!;
     copy.BoxedValue=pair.Value;
     if(!Equals(copy.BoxedValue,pair.Value))throw new ArgumentException("Preset value was clamped: "+pair.Key);
    }
    candidate.Save();candidate.Reload();
    foreach(var pair in values)if(!Equals(candidate[entries[pair.Key].Definition].BoxedValue,pair.Value))throw new IOException("Preset readback differs: "+pair.Key);
    using(var stream=new FileStream(temporary,FileMode.Open,FileAccess.Write,FileShare.None))stream.Flush(true);
    if(before==null?File.Exists(destination):!File.Exists(destination)||!before.SequenceEqual(File.ReadAllBytes(destination)))throw new IOException("Configuration changed during the preset edit.");
    foreach(var pair in values)entries[pair.Key].BoxedValue=pair.Value;
    if(before==null)File.Move(temporary,destination);else File.Replace(temporary,destination,null);
    reserved=false;
   }catch{foreach(var pair in previous)entries[pair.Key].BoxedValue=pair.Value;throw;}
   finally{file.SaveOnConfigSet=save;if(reserved&&File.Exists(temporary))File.Delete(temporary);}
  }
 }
}
