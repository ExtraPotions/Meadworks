// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System.Text;
namespace ExtraPotions.Meadworks.Crucible {
    /// <summary>
    /// Translation keys for a configuration setting, derived from its section and key so they stay stable while the
    /// setting does: <c>cfg_&lt;section&gt;__&lt;key&gt;_name</c>, <c>cfg_&lt;section&gt;__&lt;key&gt;_help</c> and
    /// <c>cfg_&lt;section&gt;_heading</c>. Each part is lowercased; every run of other characters becomes one underscore.
    /// Returns null when a part has no ASCII letter or digit or the key would be longer than a translation key may be.
    /// </summary>
    public static class SettingTextKeys {
        public const string Prefix="cfg_";
        public static string? Name(string section,string key)=>Setting(section,key,"_name");
        public static string? Help(string section,string key)=>Setting(section,key,"_help");
        public static string? Heading(string section){var s=Slug(section);return s.Length==0?null:Checked(Prefix+s+"_heading");}
        static string? Setting(string section,string key,string suffix){
            var s=Slug(section);var k=Slug(key);
            return s.Length==0||k.Length==0?null:Checked(Prefix+s+"__"+k+suffix);
        }
        static string? Checked(string key)=>TranslationJson.IsValidKey(key)?key:null;
        public static string Slug(string? text){
            var result=new StringBuilder();bool gap=false;
            foreach(var raw in text??""){
                var c=char.ToLowerInvariant(raw);
                if((c>='a'&&c<='z')||(c>='0'&&c<='9')){if(gap&&result.Length>0)result.Append('_');result.Append(c);gap=false;}
                else gap=true;
            }
            return result.ToString();
        }
    }
}
