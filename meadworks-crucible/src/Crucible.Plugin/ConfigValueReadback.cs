// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BepInEx.Configuration;

namespace ExtraPotions.Meadworks.Crucible {
    internal static class ConfigValueReadback {
        static readonly char[] invalidNameChars={'=','\n','\t','\\','"','\'','[',']'};

        internal static bool TryReload(ConfigEntryBase entry,string path){
            var values=new List<string>();string section="";
            using(var reader=new StreamReader(path,Encoding.UTF8,true)){
                string? raw;
                while((raw=reader.ReadLine())!=null){
                    string line=raw.Trim();
                    if(line.StartsWith("#"))continue;
                    if(line.StartsWith("[")&&line.EndsWith("]")){section=line.Substring(1,line.Length-2);continue;}
                    int split=line.IndexOf('=');if(split<0)continue;
                    string key=line.Substring(0,split).Trim();
                    if(!ValidName(section)||!ValidName(key))return false;
                    if(section==entry.Definition.Section&&key==entry.Definition.Key)values.Add(line.Substring(split+1).Trim());
                }
            }
            if(values.Count==0)return false;
            // Validate the whole syntax before setters, so fallback never repeats a partial target reload.
            foreach(string value in values)entry.SetSerializedValue(value);
            return true;
        }

        static bool ValidName(string name)=>name==name.Trim()&&name.IndexOfAny(invalidNameChars)<0;
    }
}
