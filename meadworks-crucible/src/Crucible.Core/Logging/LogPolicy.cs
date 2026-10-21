using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
namespace ExtraPotions.Meadworks.Crucible {
    /// <summary>Suite-wide verbosity (roster decision 6). Quiet is the default.</summary>
    public enum LogVerbosity { Quiet, Normal, Verbose, Debug }

    /// <summary>Severity of one Meadworks log line.</summary>
    public enum LogSeverity { Error, Warning, Info, Verbose, Debug }

    public static class LogLevels {
        /// <summary>Quiet keeps errors and warnings; Normal adds info; Verbose adds verbose; Debug shows everything.</summary>
        public static bool Shows(LogVerbosity verbosity,LogSeverity severity){
            switch(severity){
                case LogSeverity.Error:case LogSeverity.Warning:return true;
                case LogSeverity.Info:return verbosity>=LogVerbosity.Normal;
                case LogSeverity.Verbose:return verbosity>=LogVerbosity.Verbose;
                case LogSeverity.Debug:return verbosity>=LogVerbosity.Debug;
                default:return false;
            }
        }
        /// <summary>Names only, case-insensitive; Charter's "Trace" maps to Debug.</summary>
        public static bool TryParse(string? text,out LogVerbosity verbosity){
            verbosity=LogVerbosity.Quiet;var name=text?.Trim()??"";
            if(string.Equals(name,"trace",StringComparison.OrdinalIgnoreCase)){verbosity=LogVerbosity.Debug;return true;}
            foreach(LogVerbosity candidate in Enum.GetValues(typeof(LogVerbosity)))
                if(string.Equals(name,candidate.ToString(),StringComparison.OrdinalIgnoreCase)){verbosity=candidate;return true;}
            return false;
        }
        internal static void ValidateName(string? name,string what){
            if(string.IsNullOrWhiteSpace(name)||name!.Length>128||name.IndexOfAny(new[]{'=',',',';','\r','\n','\0'})>=0)throw new ArgumentException("Invalid "+what+" name.",nameof(name));
        }
        internal static IEnumerable<string> Entries(string? text)=>(text??"").Split(new[]{',',';'},StringSplitOptions.RemoveEmptyEntries).Select(e=>e.Trim()).Where(e=>e.Length>0);
    }

    /// <summary>The suite-wide level with per-product overrides (case-insensitive product names, as passed to MeadworksLog.For).</summary>
    public sealed class LogPolicy {
        readonly Dictionary<string,LogVerbosity> overrides=new Dictionary<string,LogVerbosity>(StringComparer.OrdinalIgnoreCase);
        LogVerbosity level=LogVerbosity.Quiet;
        /// <summary>Increments on every change, so adapters know when to persist.</summary>
        public long Revision { get; private set; }
        public LogVerbosity Level {
            get=>level;
            set{if(!Enum.IsDefined(typeof(LogVerbosity),value))throw new ArgumentException("Unknown log level.",nameof(value));if(level!=value){level=value;Revision++;}}
        }
        public IReadOnlyDictionary<string,LogVerbosity> Overrides=>new Dictionary<string,LogVerbosity>(overrides,StringComparer.OrdinalIgnoreCase);
        public LogVerbosity LevelFor(string? product)=>product!=null&&overrides.TryGetValue(product.Trim(),out var own)?own:level;
        public bool Allows(string? product,LogSeverity severity)=>LogLevels.Shows(LevelFor(product),severity);
        public void SetOverride(string product,LogVerbosity verbosity){
            LogLevels.ValidateName(product,"product");
            if(!Enum.IsDefined(typeof(LogVerbosity),verbosity))throw new ArgumentException("Unknown log level.",nameof(verbosity));
            overrides[product.Trim()]=verbosity;Revision++;
        }
        public bool ClearOverride(string product){if(product==null||!overrides.Remove(product.Trim()))return false;Revision++;return true;}
        /// <summary>"Product=Level" entries separated by commas or semicolons, sorted by product.</summary>
        public string FormatOverrides()=>string.Join(",",overrides.OrderBy(o=>o.Key,StringComparer.OrdinalIgnoreCase).Select(o=>o.Key+"="+o.Value));
        /// <summary>Replaces every override from text; returns the entries that were ignored as invalid.</summary>
        public IReadOnlyList<string> LoadOverrides(string? text){
            var errors=new List<string>();var loaded=new Dictionary<string,LogVerbosity>(StringComparer.OrdinalIgnoreCase);
            foreach(var entry in LogLevels.Entries(text)){
                var cut=entry.IndexOf('=');
                var name=cut>0?entry.Substring(0,cut).Trim():"";
                if(cut<=0||name.Length==0||name.Length>128||!LogLevels.TryParse(entry.Substring(cut+1),out var verbosity)){errors.Add(entry);continue;}
                loaded[name]=verbosity;
            }
            overrides.Clear();foreach(var pair in loaded)overrides[pair.Key]=pair.Value;Revision++;
            return errors;
        }
    }

    public readonly struct RepeatVerdict {
        public bool Emit { get; }
        /// <summary>How many identical lines were swallowed in the window that just ended (emit a summary first).</summary>
        public int SuppressedBefore { get; }
        internal RepeatVerdict(bool emit,int suppressed){Emit=emit;SuppressedBefore=suppressed;}
    }

    public sealed class RepeatSummary {
        public string Key { get; }
        public int Count { get; }
        public object? Tag { get; }
        internal RepeatSummary(string key,int count,object? tag){Key=key;Count=count;Tag=tag;}
    }

    /// <summary>
    /// Collapses identical lines: the first line of a window is written, repeats inside the window only count, and the
    /// count is reported when the next identical line opens a new window or when <see cref="Flush"/> finds the window
    /// expired. A full table fails open (lines are written, not tracked). Window zero disables suppression.
    /// </summary>
    public sealed class RepeatSuppressor {
        sealed class Entry {internal double Start;internal int Count;internal object? Tag;}
        readonly Dictionary<string,Entry> entries=new Dictionary<string,Entry>(StringComparer.Ordinal);
        readonly int capacity;
        public double WindowSeconds { get; }
        public int Tracked=>entries.Count;
        public RepeatSuppressor(double windowSeconds=60,int capacity=256){
            if(double.IsNaN(windowSeconds)||windowSeconds<0||windowSeconds>86400)throw new ArgumentOutOfRangeException(nameof(windowSeconds));
            if(capacity<1||capacity>65536)throw new ArgumentOutOfRangeException(nameof(capacity));
            WindowSeconds=windowSeconds;this.capacity=capacity;
        }
        public RepeatVerdict Admit(string key,double now,object? tag=null){
            if(key==null)throw new ArgumentNullException(nameof(key));
            if(WindowSeconds==0)return new RepeatVerdict(true,0);
            if(entries.TryGetValue(key,out var entry)){
                if(now-entry.Start<WindowSeconds){entry.Count++;return new RepeatVerdict(false,0);}
                var suppressed=entry.Count;entry.Start=now;entry.Count=0;entry.Tag=tag??entry.Tag;
                return new RepeatVerdict(true,suppressed);
            }
            if(entries.Count>=capacity)Expire(now,null);
            if(entries.Count<capacity)entries[key]=new Entry{Start=now,Tag=tag};
            return new RepeatVerdict(true,0);
        }
        /// <summary>Drops expired windows and returns those that swallowed lines, so their counts are still written.</summary>
        public IReadOnlyList<RepeatSummary> Flush(double now){var result=new List<RepeatSummary>();Expire(now,result);return result;}
        void Expire(double now,List<RepeatSummary>? summaries){
            foreach(var pair in entries.Where(e=>now-e.Value.Start>=WindowSeconds).ToArray()){
                if(summaries!=null&&pair.Value.Count>0)summaries.Add(new RepeatSummary(pair.Key,pair.Value.Count,pair.Value.Tag));
                entries.Remove(pair.Key);
            }
        }
        public static string Summary(int count,double windowSeconds)=>
            "(previous message repeated "+count.ToString(CultureInfo.InvariantCulture)+" more time"+(count==1?"":"s")+" in "+windowSeconds.ToString("0.##",CultureInfo.InvariantCulture)+" s)";
    }

    /// <summary>Minimum severity the console keeps for one BepInEx log source.</summary>
    public enum ConsoleFilterLevel { All, Info, Warning, Error, None }

    /// <summary>
    /// Optional per-mod console filter for every BepInEx log source (roster decision 6). Off by default; when on,
    /// unlisted sources follow the "*" rule, or show everything without one.
    /// </summary>
    public sealed class ConsoleFilter {
        public const string Wildcard="*";
        readonly Dictionary<string,ConsoleFilterLevel> rules=new Dictionary<string,ConsoleFilterLevel>(StringComparer.OrdinalIgnoreCase);
        public bool Enabled { get; set; }
        public IReadOnlyDictionary<string,ConsoleFilterLevel> Rules=>new Dictionary<string,ConsoleFilterLevel>(rules,StringComparer.OrdinalIgnoreCase);
        public ConsoleFilterLevel LevelFor(string? source)=>source!=null&&rules.TryGetValue(source.Trim(),out var level)?level:rules.TryGetValue(Wildcard,out var fallback)?fallback:ConsoleFilterLevel.All;
        public bool Allows(string? source,LogSeverity severity){
            if(!Enabled)return true;
            switch(LevelFor(source)){
                case ConsoleFilterLevel.All:return true;
                case ConsoleFilterLevel.Info:return severity<=LogSeverity.Info;
                case ConsoleFilterLevel.Warning:return severity<=LogSeverity.Warning;
                case ConsoleFilterLevel.Error:return severity==LogSeverity.Error;
                default:return false;
            }
        }
        public void Set(string source,ConsoleFilterLevel level){
            LogLevels.ValidateName(source,"log source");
            if(!Enum.IsDefined(typeof(ConsoleFilterLevel),level))throw new ArgumentException("Unknown filter level.",nameof(level));
            rules[source.Trim()]=level;
        }
        public bool Remove(string source)=>source!=null&&rules.Remove(source.Trim());
        public string Format()=>string.Join(";",rules.OrderBy(r=>r.Key==Wildcard?0:1).ThenBy(r=>r.Key,StringComparer.OrdinalIgnoreCase).Select(r=>r.Key+"="+r.Value));
        /// <summary>Replaces every rule from "Source=Level" entries; returns the entries ignored as invalid.</summary>
        public IReadOnlyList<string> Load(string? text){
            var errors=new List<string>();var loaded=new Dictionary<string,ConsoleFilterLevel>(StringComparer.OrdinalIgnoreCase);
            foreach(var entry in LogLevels.Entries(text)){
                var cut=entry.IndexOf('=');var name=cut>0?entry.Substring(0,cut).Trim():"";
                if(cut<=0||name.Length==0||name.Length>128||!TryParseLevel(entry.Substring(cut+1),out var level)){errors.Add(entry);continue;}
                loaded[name]=level;
            }
            rules.Clear();foreach(var pair in loaded)rules[pair.Key]=pair.Value;
            return errors;
        }
        public static bool TryParseLevel(string? text,out ConsoleFilterLevel level){
            var name=text?.Trim()??"";level=ConsoleFilterLevel.All;
            if(string.Equals(name,"off",StringComparison.OrdinalIgnoreCase)){level=ConsoleFilterLevel.None;return true;}
            foreach(ConsoleFilterLevel candidate in Enum.GetValues(typeof(ConsoleFilterLevel)))
                if(string.Equals(name,candidate.ToString(),StringComparison.OrdinalIgnoreCase)){level=candidate;return true;}
            return false;
        }
    }
}
