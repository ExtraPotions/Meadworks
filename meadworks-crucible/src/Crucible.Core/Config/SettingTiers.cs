// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;

namespace ExtraPotions.Meadworks.Crucible {
    /// <summary>How prominently a setting is offered. Essential rows appear on a mod's MeadWorks menu page; Advanced rows
    /// only in Almanac; File-only rows in Almanac with its Advanced switch on.</summary>
    public enum SettingTier { Essential, Advanced, FileOnly }

    /// <summary>
    /// The tiers one product declares in the embedded resource <c>&lt;AssemblyName&gt;.settings-tiers.json</c>:
    /// <c>{"schema":1,"product":"Burrow","default":"Advanced","settings":{"1 - General/Enabled":"Essential"}}</c>.
    /// Keys are the bound <c>Section/Key</c>, case-sensitive; unlisted settings take <see cref="Default"/>. A key may use
    /// <c>*</c> for any run of characters (<c>Section/*</c>, <c>Ship: */*</c>, <c>Foods/* Duration</c>) for generated
    /// per-item rows. An exact key wins over a pattern; among matching patterns the longest wins.
    /// </summary>
    public sealed class SettingTierTable {
        public const int Schema=1;
        public const string ResourceSuffix=".settings-tiers.json";
        public static string ResourceName(string assemblyName)=>assemblyName+ResourceSuffix;

        /// <summary>A product without a tier resource: every setting is Advanced.</summary>
        public static SettingTierTable Missing { get; }=new SettingTierTable("",SettingTier.Advanced,new Dictionary<string,SettingTier>(StringComparer.Ordinal),false);

        readonly Dictionary<string,SettingTier> settings;
        readonly KeyValuePair<string,SettingTier>[] patterns;
        SettingTierTable(string product,SettingTier fallback,Dictionary<string,SettingTier> settings,bool declared){
            Product=product;Default=fallback;this.settings=settings;Declared=declared;
            // Longest first; equal lengths in ordinal order so the winner never depends on file order.
            patterns=settings.Where(pair=>pair.Key.IndexOf('*')>=0).OrderByDescending(pair=>pair.Key.Length)
                .ThenBy(pair=>pair.Key,StringComparer.Ordinal).ToArray();
        }

        public string Product { get; }
        public SettingTier Default { get; }
        /// <summary>False for <see cref="Missing"/>.</summary>
        public bool Declared { get; }
        public int Count=>settings.Count;
        public IReadOnlyCollection<string> Keys=>settings.Keys;

        public static string Key(string section,string key)=>(section??"")+"/"+(key??"");

        public bool TryGet(string section,string key,out SettingTier tier){
            string full=Key(section,key);
            if(settings.TryGetValue(full,out tier)&&full.IndexOf('*')<0)return true;
            foreach(var pattern in patterns)if(Matches(pattern.Key,full)){tier=pattern.Value;return true;}
            tier=Default;return false;
        }

        /// <summary><c>*</c> matches any run of characters, including none and including <c>/</c>; everything else is literal and case-sensitive.</summary>
        public static bool Matches(string pattern,string text){
            int p=0,t=0,star=-1,mark=0;
            while(t<text.Length){
                if(p<pattern.Length&&pattern[p]=='*'){star=p++;mark=t;}
                else if(p<pattern.Length&&pattern[p]==text[t]){p++;t++;}
                else if(star>=0){p=star+1;t=++mark;}
                else return false;
            }
            while(p<pattern.Length&&pattern[p]=='*')p++;
            return p==pattern.Length;
        }

        public SettingTier Tier(string section,string key)=>TryGet(section,key,out var tier)?tier:Default;

        public static bool TryParseTier(string? text,out SettingTier tier){
            switch(text){
                case "Essential":tier=SettingTier.Essential;return true;
                case "Advanced":tier=SettingTier.Advanced;return true;
                case "FileOnly":tier=SettingTier.FileOnly;return true;
                default:tier=SettingTier.Advanced;return false;
            }
        }

        /// <summary>Reads a tier resource. A wrong schema or shape throws <see cref="FormatException"/>; a bad single row is
        /// skipped (it keeps the default tier) and reported in <paramref name="warnings"/>.</summary>
        public static SettingTierTable Parse(string json,ICollection<string>? warnings=null){
            if(!(MiniJson.Parse(json) is Dictionary<string,object?> root))throw new FormatException("Setting tiers must be a JSON object.");
            if(!(root.TryGetValue("schema",out var schema)&&schema is double number&&number==Schema))throw new FormatException("Setting tiers need \"schema\": "+Schema+".");
            string product=root.TryGetValue("product",out var named)&&named is string text?text:"";
            if(product.Length==0)throw new FormatException("Setting tiers need a \"product\" name.");
            var fallback=SettingTier.Advanced;
            if(root.TryGetValue("default",out var declared)&&!(declared is string tierName&&TryParseTier(tierName,out fallback)))
                throw new FormatException("Setting tiers \"default\" must be Essential, Advanced or FileOnly.");
            var rows=new Dictionary<string,SettingTier>(StringComparer.Ordinal);
            if(root.TryGetValue("settings",out var list)&&list!=null){
                if(!(list is Dictionary<string,object?> entries))throw new FormatException("Setting tiers \"settings\" must be an object.");
                foreach(var pair in entries){
                    int slash=pair.Key.IndexOf('/');
                    if(slash<=0||slash==pair.Key.Length-1){warnings?.Add(product+": tier key '"+pair.Key+"' is not Section/Key; skipped.");continue;}
                    if(!(pair.Value is string value&&TryParseTier(value,out var tier))){warnings?.Add(product+": tier for '"+pair.Key+"' must be Essential, Advanced or FileOnly; skipped.");continue;}
                    rows[pair.Key]=tier;
                }
            }
            foreach(var extra in root.Keys.Where(key=>key!="schema"&&key!="product"&&key!="default"&&key!="settings"))
                warnings?.Add(product+": unknown setting tiers property '"+extra+"' ignored.");
            return new SettingTierTable(product,fallback,rows,true);
        }
    }

    /// <summary>
    /// Reads a plugin's tier resource. Returns false while the answer is not known yet (the plugin is not loaded), so
    /// the result is not remembered; true with null <paramref name="json"/> when the plugin has no resource.
    /// </summary>
    public delegate bool SettingTierSource(string pluginGuid,out string? json);

    /// <summary>Process-wide tier lookup by plugin GUID. Crucible installs the native source; tables are read once.</summary>
    public static class SettingTiers {
        sealed class Lease:IDisposable {
            public void Dispose(){lock(gate){if(ReferenceEquals(current,this)){current=null;source=null;warn=null;cache.Clear();}}}
        }
        static readonly object gate=new object();
        static readonly Dictionary<string,SettingTierTable> cache=new Dictionary<string,SettingTierTable>(StringComparer.Ordinal);
        static SettingTierSource? source;
        static Action<string>? warn;
        static Lease? current;

        /// <summary>Replaces the source; disposing the returned lease removes it unless a newer one replaced it.</summary>
        public static IDisposable Install(SettingTierSource reader,Action<string>? warning=null){
            if(reader==null)throw new ArgumentNullException(nameof(reader));
            var lease=new Lease();
            lock(gate){source=reader;warn=warning;cache.Clear();current=lease;}
            return lease;
        }

        /// <summary>The plugin's table; <see cref="SettingTierTable.Missing"/> when it ships none, is unreadable or no source is installed.</summary>
        public static SettingTierTable For(string pluginGuid){
            if(string.IsNullOrEmpty(pluginGuid))return SettingTierTable.Missing;
            SettingTierSource? reader;Action<string>? report;
            lock(gate){
                if(cache.TryGetValue(pluginGuid,out var known))return known;
                reader=source;report=warn;
            }
            if(reader==null)return SettingTierTable.Missing;
            var table=SettingTierTable.Missing;
            try{
                if(!reader(pluginGuid,out var json))return SettingTierTable.Missing;
                if(json!=null){
                    var warnings=new List<string>();
                    table=SettingTierTable.Parse(json,warnings);
                    foreach(var line in warnings)report?.Invoke(line);
                }
            }
            catch(Exception error){
                report?.Invoke(pluginGuid+": setting tiers unreadable, every setting stays Advanced: "+error.GetBaseException().Message);
                table=SettingTierTable.Missing;
            }
            lock(gate){if(ReferenceEquals(source,reader))cache[pluginGuid]=table;}
            return table;
        }

        public static SettingTier Tier(string pluginGuid,string section,string key)=>For(pluginGuid).Tier(section,key);
    }

    /// <summary>Which rows each settings view lists.</summary>
    public static class SettingTierRules {
        /// <summary>A mod's MeadWorks menu page lists Essential settings only.</summary>
        public static bool ShowOnProductPage(SettingTier tier)=>tier==SettingTier.Essential;

        /// <summary>Almanac's full view lists Essential and Advanced settings, and File-only ones while its Advanced switch is on.</summary>
        public static bool ShowInAlmanac(SettingTier tier,bool advancedOn)=>tier!=SettingTier.FileOnly||advancedOn;

        public static bool HostOwned(ConfigScope? scope)=>scope==ConfigScope.ServerAuthoritative||scope==ConfigScope.Cheat;

        /// <summary>A connected player who is not an admin sees host-owned rows folded into one "Set by the server" line.
        /// Hosts, admins and single player see the rows.</summary>
        public static bool CollapseHostOwned(SessionRole role,bool isAdmin,ConfigScope? scope)=>role==SessionRole.RemoteClient&&!isAdmin&&HostOwned(scope);

        /// <summary>A plugin that belongs to a product's page, such as <c>com.meadworks.crucible.ui</c> for <c>com.meadworks.crucible</c>.</summary>
        public static bool Companion(string product,string plugin)=>!string.IsNullOrEmpty(product)&&plugin!=null
            &&plugin.Length>product.Length+1&&plugin.StartsWith(product+".",StringComparison.Ordinal);
    }
}
