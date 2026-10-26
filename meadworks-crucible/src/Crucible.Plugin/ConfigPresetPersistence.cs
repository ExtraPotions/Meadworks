// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx.Configuration;

namespace ExtraPotions.Meadworks.Crucible {
    internal sealed class PresetPublicationException : IOException {
        internal PresetPublicationException(Exception error):base("Preset was published, but final readback failed.",error){}
    }
    // Same targeted staging/serializer/atomic-publication algorithm as the shipped Ember/Saga preset writer.
    public static partial class MeadworksConfig {
        internal static void PersistPresetEntries(IReadOnlyList<ConfigEntryBase> entries){
            if(entries==null||entries.Count==0)throw new ArgumentException("A preset needs entries.",nameof(entries));
            var file=entries[0].ConfigFile;
            var seen=new HashSet<ConfigEntryBase>();
            var values=new List<(ConfigEntryBase Entry,object Value,string Serialized)>();
            foreach(var entry in entries){
                if(entry==null||!ReferenceEquals(entry.ConfigFile,file)||!seen.Add(entry)||!file.ContainsKey(entry.Definition)||!ReferenceEquals(file[entry.Definition],entry))
                    throw new ArgumentException("Preset entries must be distinct current entries from one file.",nameof(entries));
                values.Add((entry,entry.BoxedValue,TomlTypeConverter.ConvertToString(entry.BoxedValue,entry.SettingType)));
            }
            string path=Path.GetFullPath(file.ConfigFilePath),temporary=path+".preset-"+Guid.NewGuid().ToString("N")+".tmp";
            byte[]? before=File.Exists(path)?File.ReadAllBytes(path):null;
            bool reserved=false,published=false;
            try{
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                using(var output=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None)){
                    reserved=true;if(before!=null)output.Write(before,0,before.Length);
                }
                var candidate=CreateOwnValueStaging(temporary);
                var bind=typeof(ConfigFile).GetMethods(BindingFlags.Instance|BindingFlags.Public).Single(method=>
                    method.Name=="Bind"&&method.IsGenericMethodDefinition&&method.GetParameters().Length==3&&method.GetParameters()[0].ParameterType==typeof(ConfigDefinition));
                var copies=new List<(ConfigEntryBase Entry,string Serialized)>();
                foreach(var value in values){
                    var entry=value.Entry;
                    var copy=(ConfigEntryBase)bind.MakeGenericMethod(entry.SettingType).Invoke(candidate,new object[]{entry.Definition,entry.DefaultValue,entry.Description})!;
                    copy.BoxedValue=value.Value;
                    if(TomlTypeConverter.ConvertToString(copy.BoxedValue,copy.SettingType)!=value.Serialized)throw new IOException("The configuration serializer changed a preset value.");
                    copies.Add((copy,value.Serialized));
                }
                candidate.Save();
                using(var written=new FileStream(temporary,FileMode.Open,FileAccess.Write,FileShare.None))written.Flush(true);
                candidate.Reload();
                foreach(var copy in copies)
                    if(TomlTypeConverter.ConvertToString(copy.Entry.BoxedValue,copy.Entry.SettingType)!=copy.Serialized)throw new IOException("A saved preset value failed readback.");
                byte[] staged=File.ReadAllBytes(temporary);
                if(before==null?File.Exists(path):!File.Exists(path)||!before.SequenceEqual(File.ReadAllBytes(path)))throw new IOException("The configuration changed on disk during the preset edit.");
                if(before==null)File.Move(temporary,path);else File.Replace(temporary,path,null);
                reserved=false;published=true;
                if(!staged.SequenceEqual(File.ReadAllBytes(path)))throw new IOException("The published preset configuration failed readback.");
            }catch(Exception error) when(published){throw new PresetPublicationException(error);}
            finally{if(reserved&&File.Exists(temporary))File.Delete(temporary);}
        }
    }
}
