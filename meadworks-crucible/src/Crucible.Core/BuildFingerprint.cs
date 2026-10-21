// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
namespace ExtraPotions.Meadworks.Crucible {
    /// <summary>One loaded MeadWorks assembly: its name, informational version and SHA256 of the file on disk.</summary>
    public sealed class BuildEntry {
        public string Assembly{get;} public string Version{get;} public string Sha256{get;}
        public BuildEntry(string assembly,string version,string sha256){
            if(string.IsNullOrWhiteSpace(assembly)||assembly.Length>128)throw new ArgumentException("Invalid assembly name.",nameof(assembly));
            if(sha256==null||sha256.Length!=64||!sha256.All(Uri.IsHexDigit))throw new ArgumentException("SHA256 must be 64 hex characters.",nameof(sha256));
            Assembly=assembly;Version=(version??"").Length>64?version!.Substring(0,64):version??"";Sha256=sha256.ToUpperInvariant();
        }
    }
    /// <summary>
    /// Build-compatibility diagnostics: compares two peers' MeadWorks assembly hashes. It is a report, not an admission or
    /// integrity check: a client can report anything, a matching hash proves nothing about the running code, and a
    /// mismatch never refuses or kicks anyone (vanilla and older clients stay admitted under the host's own rules).
    /// </summary>
    public sealed class BuildComparison {
        /// <summary>Capability id under which Crucible publishes Func&lt;IReadOnlyList&lt;BuildEntry&gt;&gt; (version 1.0).</summary>
        public const string Capability="meadworks.crucible.build_hashes";
        public IReadOnlyList<string> Same{get;} public IReadOnlyList<string> Different{get;}
        public IReadOnlyList<string> OnlyLocal{get;} public IReadOnlyList<string> OnlyRemote{get;}
        public bool Matches=>Different.Count==0&&OnlyLocal.Count==0&&OnlyRemote.Count==0;
        BuildComparison(IReadOnlyList<string> same,IReadOnlyList<string> different,IReadOnlyList<string> onlyLocal,IReadOnlyList<string> onlyRemote){Same=same;Different=different;OnlyLocal=onlyLocal;OnlyRemote=onlyRemote;}
        public static BuildComparison Compare(IEnumerable<BuildEntry> local,IEnumerable<BuildEntry> remote){
            var a=Index(local);var b=Index(remote);
            var names=a.Keys.Union(b.Keys,StringComparer.Ordinal).OrderBy(n=>n,StringComparer.Ordinal).ToList();
            var same=new List<string>();var different=new List<string>();var onlyLocal=new List<string>();var onlyRemote=new List<string>();
            foreach(var name in names){
                bool inA=a.TryGetValue(name,out var x),inB=b.TryGetValue(name,out var y);
                if(inA&&!inB)onlyLocal.Add(name);
                else if(!inA)onlyRemote.Add(name);
                else if(x!.Sha256==y!.Sha256)same.Add(name);
                else different.Add(name+" "+x.Version+"/"+y.Version);
            }
            return new BuildComparison(same,different,onlyLocal,onlyRemote);
        }
        static Dictionary<string,BuildEntry> Index(IEnumerable<BuildEntry> entries){
            var result=new Dictionary<string,BuildEntry>(StringComparer.Ordinal);
            foreach(var e in entries??Enumerable.Empty<BuildEntry>())if(e!=null&&!result.ContainsKey(e.Assembly))result.Add(e.Assembly,e);
            return result;
        }
        public string Summary()=>Matches?"builds match ("+Same.Count+" assemblies)"
            :"builds differ: "+string.Join("; ",new[]{
                Different.Count>0?"different "+string.Join(", ",Different):null,
                OnlyLocal.Count>0?"only here "+string.Join(", ",OnlyLocal):null,
                OnlyRemote.Count>0?"only there "+string.Join(", ",OnlyRemote):null}.Where(s=>s!=null))+" ("+Same.Count+" same)";
    }
}
