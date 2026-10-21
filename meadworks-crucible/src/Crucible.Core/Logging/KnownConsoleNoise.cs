// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
namespace ExtraPotions.Meadworks.Crucible {
    /// <summary>Severity of a console line as the matcher sees it. Fatal is never matched.</summary>
    public enum NoiseSeverity { Info, Warning, Error, Fatal }

    /// <summary>
    /// One known-harmless vanilla or Unity console message: exact text, or a pattern anchored at both ends, on one log source
    /// at the listed levels. Loaded from the shared data file <c>tools/Meadworks.TestRunner/console-noise.json</c>, which the
    /// test runner reads too.
    /// </summary>
    public sealed class KnownNoiseRule {
        readonly Regex? regex;
        readonly string[] levels;
        public string Id { get; }
        public string Source { get; }
        public IReadOnlyList<string> Levels=>levels;
        public bool IsPattern { get; }
        public string Text { get; }
        public string Emitter { get; }
        public string Sample { get; }
        internal KnownNoiseRule(string id,string source,string[] levels,bool isPattern,string text,string emitter,string sample){
            Id=id;Source=source;this.levels=levels;IsPattern=isPattern;Text=text;Emitter=emitter;Sample=sample;
            if(isPattern)regex=new Regex(text,RegexOptions.ECMAScript,TimeSpan.FromMilliseconds(100));
        }
        public bool Matches(string source,NoiseSeverity severity,string message){
            if(!string.Equals(source,Source,StringComparison.Ordinal))return false;
            if(severity!=NoiseSeverity.Warning&&severity!=NoiseSeverity.Error)return false;
            if(Array.IndexOf(levels,severity.ToString())<0)return false;
            if(!IsPattern)return string.Equals(message,Text,StringComparison.Ordinal);
            try{return regex!.IsMatch(message);}catch(RegexMatchTimeoutException){return false;}
        }
    }

    /// <summary>
    /// Exact, versioned list of known-harmless vanilla and Unity console messages (world-generation hints, audio and video
    /// shaders missing on a <c>-nographics</c> server, and similar). The console hides these; LogOutput.log keeps everything.
    /// A line is known only when its whole text matches a rule on the rule's own source at the rule's level. Fatal lines,
    /// lines carrying a stack trace and every Meadworks or other mod source are never known: the rule loader rejects any rule
    /// whose source is not <c>Unity Log</c> or the one explicit third-party case below.
    /// </summary>
    public sealed class KnownConsoleNoise {
        public const string ResourceName="console-noise.json";
        public const string UnitySource="Unity Log";
        /// <summary>The single third-party source with rules: Jotunn reports vanilla prop names that exist twice in the game's own bundles.</summary>
        public const string JotunnAssetSource="Jotunn.Managers.AssetManager";
        static readonly string[] AllowedSources={UnitySource,JotunnAssetSource};
        static KnownConsoleNoise? embedded;

        readonly KnownNoiseRule[] rules;
        public string Version { get; }
        public IReadOnlyList<KnownNoiseRule> Rules=>rules;
        KnownConsoleNoise(string version,KnownNoiseRule[] rules){Version=version;this.rules=rules;}

        /// <summary>The list shipped inside Crucible.Core.</summary>
        public static KnownConsoleNoise Embedded=>embedded??(embedded=LoadEmbedded());

        static KnownConsoleNoise LoadEmbedded(){
            using(var stream=typeof(KnownConsoleNoise).Assembly.GetManifestResourceStream(ResourceName)){
                if(stream==null)throw new InvalidOperationException("The console-noise list is not embedded in Crucible.Core.");
                using(var reader=new StreamReader(stream,Encoding.UTF8))return Parse(reader.ReadToEnd());
            }
        }

        public static KnownConsoleNoise Parse(string json){
            var root=MiniJson.Parse(json) as Dictionary<string,object?>;
            if(root==null||!(root.TryGetValue("version",out var version)&&version is string versionText&&versionText.Length>0))throw new FormatException("console-noise: a version is required.");
            if(!(root.TryGetValue("rules",out var list)&&list is List<object?> items))throw new FormatException("console-noise: a rules list is required.");
            var parsed=new List<KnownNoiseRule>();var ids=new HashSet<string>(StringComparer.Ordinal);
            foreach(var item in items){
                var map=item as Dictionary<string,object?>;if(map==null)throw new FormatException("console-noise: a rule must be an object.");
                string id=Text(map,"id"),source=Text(map,"source"),match=Text(map,"match"),text=Text(map,"text");
                if(!ids.Add(id))throw new FormatException("console-noise: duplicate rule id "+id+".");
                if(Array.IndexOf(AllowedSources,source)<0)throw new FormatException("console-noise: rule "+id+" names source '"+source+"'; only vanilla Unity and the one listed Jotunn case may be hidden.");
                if(!(map.TryGetValue("levels",out var levelList)&&levelList is List<object?> levelItems&&levelItems.Count>0))throw new FormatException("console-noise: rule "+id+" needs levels.");
                var levels=levelItems.Select(l=>l as string??"").ToArray();
                foreach(var level in levels)if(level!="Warning"&&level!="Error")throw new FormatException("console-noise: rule "+id+" level '"+level+"' is not Warning or Error.");
                bool pattern;
                if(match=="exact")pattern=false;
                else if(match=="regex"){
                    pattern=true;
                    if(!text.StartsWith("^",StringComparison.Ordinal)||!text.EndsWith("$",StringComparison.Ordinal)||text.EndsWith("\\$",StringComparison.Ordinal))throw new FormatException("console-noise: pattern "+id+" must be anchored with ^ and $.");
                    if(text.Contains(".*")||text.Contains(".+"))throw new FormatException("console-noise: pattern "+id+" may not use an unbounded dot.");
                }
                else throw new FormatException("console-noise: rule "+id+" match must be 'exact' or 'regex'.");
                parsed.Add(new KnownNoiseRule(id,source,levels,pattern,text,Text(map,"emitter"),Text(map,"sample")));
            }
            if(parsed.Count==0)throw new FormatException("console-noise: the list is empty.");
            return new KnownConsoleNoise(versionText,parsed.ToArray());
        }

        static string Text(Dictionary<string,object?> map,string key){
            if(map.TryGetValue(key,out var value)&&value is string text&&text.Length>0)return text;
            throw new FormatException("console-noise: '"+key+"' is required.");
        }

        /// <summary>True when the whole message is a listed vanilla message on its source at this severity.</summary>
        public bool IsKnown(string? source,NoiseSeverity severity,string? message){
            if(source==null||message==null||message.Length==0||message.Length>1024)return false;
            if(severity!=NoiseSeverity.Warning&&severity!=NoiseSeverity.Error)return false;
            if(message.IndexOf('\n')>=0||message.IndexOf('\r')>=0)return false;
            foreach(var rule in rules)if(rule.Matches(source,severity,message))return true;
            return false;
        }
    }
}
