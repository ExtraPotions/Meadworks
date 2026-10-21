// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx.Configuration;

namespace ExtraPotions.Meadworks.Crucible {
    public static partial class MeadworksConfig {
        /// <summary>Persists one admitted own-value edit. Other settings come from disk, never live server overlays.</summary>
        public static void PersistOwnValue(ConfigEntryBase entry){
            if(entry==null)throw new ArgumentNullException(nameof(entry));
            var file=entry.ConfigFile;
            if(!file.ContainsKey(entry.Definition)||!ReferenceEquals(file[entry.Definition],entry))throw new InvalidOperationException("The configuration entry is no longer bound to its file.");
            string value=TomlTypeConverter.ConvertToString(entry.BoxedValue,entry.SettingType);
            string path=Path.GetFullPath(file.ConfigFilePath),temporary=path+".edit-"+Guid.NewGuid().ToString("N")+".tmp";
            byte[]? before=File.Exists(path)?File.ReadAllBytes(path):null;
            bool reserved=false;
            try{
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                using(var output=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None)){
                    reserved=true;if(before!=null)output.Write(before,0,before.Length);
                }
                var candidate=new ConfigFile(temporary,false){SaveOnConfigSet=false};
                var bind=typeof(ConfigFile).GetMethods(BindingFlags.Instance|BindingFlags.Public).Single(method=>method.Name=="Bind"&&method.IsGenericMethodDefinition&&method.GetParameters().Length==3&&method.GetParameters()[0].ParameterType==typeof(ConfigDefinition));
                var copy=(ConfigEntryBase)bind.MakeGenericMethod(entry.SettingType).Invoke(candidate,new object[]{entry.Definition,entry.DefaultValue,entry.Description})!;
                copy.BoxedValue=entry.BoxedValue;
                if(TomlTypeConverter.ConvertToString(copy.BoxedValue,copy.SettingType)!=value)throw new IOException("The configuration serializer changed the requested value.");
                candidate.Save();
                using(var written=new FileStream(temporary,FileMode.Open,FileAccess.Write,FileShare.None))written.Flush(true);
                candidate.Reload();
                if(TomlTypeConverter.ConvertToString(copy.BoxedValue,copy.SettingType)!=value)throw new IOException("The saved configuration value failed readback.");
                byte[] staged=File.ReadAllBytes(temporary);
                if(before==null?File.Exists(path):!File.Exists(path)||!before.SequenceEqual(File.ReadAllBytes(path)))throw new IOException("The configuration changed on disk during the edit.");
                try{
                    if(before==null)File.Move(temporary,path);else File.Replace(temporary,path,null);
                }catch(IOException error){throw new IOException("Configuration atomic "+(before==null?"creation":"replacement")+" failed (HRESULT 0x"+error.HResult.ToString("X8")+"): "+error.Message,error);}
                reserved=false;
                if(!staged.SequenceEqual(File.ReadAllBytes(path)))throw new IOException("The published configuration failed readback.");
            }finally{if(reserved&&File.Exists(temporary))File.Delete(temporary);}
        }

        static void AssignOwn<T>(ConfigEntry<T> entry,T value){
            var file=entry.ConfigFile;bool save=file.SaveOnConfigSet;T previous=entry.Value;
            file.SaveOnConfigSet=false;
            try{
                entry.Value=value;
                if(TomlTypeConverter.ConvertToString(entry.Value,typeof(T))!=TomlTypeConverter.ConvertToString(value,typeof(T)))throw new InvalidOperationException("The mod did not retain the requested setting.");
                PersistOwnValue(entry);
            }catch{entry.Value=previous;throw;}
            finally{file.SaveOnConfigSet=save;}
        }
    }
}
