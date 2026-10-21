// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx.Configuration;
using ExtraPotions.Saga.Config;
namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone {
    public sealed class NativeSetting<T> {
        readonly Func<T> read;
        internal NativeSetting(Func<T> read){this.read=read;}
        public T Value=>read();
    }
    public sealed class NativeConfiguration {
        public NativeSetting<T> Bind<T>(string section,string key,T value,string description,bool synced=true,AcceptableValueBase? acceptableValues=null) {
            var module=key=="Defense Enabled"?"defense":key=="Woodcutting Enabled"?"woodcutting":key=="Pickaxes Enabled"?"mining":key=="Foraging Enabled"?"foraging":key=="Fishing Enabled"?"fishing":null;
            if(module!=null)return new NativeSetting<T>(()=>(T)(object)NativeSaga.Active(module));
            if(section=="6 - Death")section=SagaSettings.Progression.Section;
            var setting=SagaSettings.Find(section+"."+key)??throw new InvalidOperationException("Unmapped reference setting: "+section+" / "+key);
            return new NativeSetting<T>(()=>(T)Convert.ChangeType(Plugin.Settings!.GetBoxed(setting),typeof(T),System.Globalization.CultureInfo.InvariantCulture));
        }
        public void AddYaml(YamlFileSet set)=>NativeSaga.Data.Add(set);
        public static string EmbeddedResource(Assembly assembly,string name){using(var stream=assembly.GetManifestResourceStream(name)??throw new InvalidOperationException("Missing YAML resource "+name))using(var reader=new StreamReader(stream))return reader.ReadToEnd();}
    }
    public static class Settings {public static AcceptableValueRange<float> UpTo(float max)=>new AcceptableValueRange<float>(0,max);}
}
