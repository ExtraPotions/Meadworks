// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
namespace ExtraPotions.Meadworks.Crucible {
    public sealed class LogSourceCounts {
        public string Source { get; }
        public long Written { get; }
        public long Hidden { get; }
        internal LogSourceCounts(string source,long written,long hidden){Source=source;Written=written;Hidden=hidden;}
    }
    public static partial class MeadworksLogging {
        static readonly Dictionary<string,long[]> sourceCounts=new Dictionary<string,long[]>(StringComparer.Ordinal);
        static SourceListener? sourceListener;
        /// <summary>Session counts by source. Counts represent console-filter admission, once per native event; no messages are retained.</summary>
        public static IReadOnlyList<LogSourceCounts> SourceCounters {
            get { lock(gate)return sourceCounts.Select(p=>new LogSourceCounts(p.Key,p.Value[0],p.Value[1])).OrderBy(p=>p.Source,StringComparer.OrdinalIgnoreCase).ToArray(); }
        }
        static void StartCounting(){if(sourceListener!=null)return;sourceListener=new SourceListener();BepInEx.Logging.Logger.Listeners.Add(sourceListener);}
        static void StopCounting(){if(sourceListener!=null)BepInEx.Logging.Logger.Listeners.Remove(sourceListener);sourceListener=null;lock(gate)sourceCounts.Clear();}
        public static void SetLevel(LogVerbosity value){if(!Enum.IsDefined(typeof(LogVerbosity),value))throw new ArgumentOutOfRangeException(nameof(value));if(level==null)throw new InvalidOperationException("Logging is not bound.");level.Value=value;}
        public static void SetProductLevel(string product,LogVerbosity? value){
            if(string.IsNullOrWhiteSpace(product)||product.IndexOfAny(new[]{',','='})>=0)throw new ArgumentException("Invalid product.",nameof(product));
            if(value.HasValue&&!Enum.IsDefined(typeof(LogVerbosity),value.Value))throw new ArgumentOutOfRangeException(nameof(value));
            if(overrides==null)throw new InvalidOperationException("Logging is not bound.");
            Dictionary<string,LogVerbosity> current;lock(gate)current=Policy.Overrides.ToDictionary(p=>p.Key,p=>p.Value,StringComparer.OrdinalIgnoreCase);
            if(value.HasValue)current[product]=value.Value;else current.Remove(product);
            overrides.Value=string.Join(",",current.OrderBy(p=>p.Key,StringComparer.OrdinalIgnoreCase).Select(p=>p.Key+"="+p.Value));
        }
        public static void SetConsoleRules(string text){var test=new ConsoleFilter();var errors=test.Load(text??"");if(errors.Count>0)throw new ArgumentException(string.Join(", ",errors));if(filterRules==null)throw new InvalidOperationException("Logging is not bound.");filterRules.Value=test.Format();}
        public static void SetConsoleEnabled(bool enabled){if(filterEnabled==null)throw new InvalidOperationException("Logging is not bound.");filterEnabled.Value=enabled;}
        public static bool FilterDiskEnabled=>filterDisk?.Value??false;
        public static void SetFilterDisk(bool enabled){if(filterDisk==null)throw new InvalidOperationException("Logging is not bound.");filterDisk.Value=enabled;}
        sealed class SourceListener:ILogListener {
            public void LogEvent(object sender,LogEventArgs args){lock(gate){
                string name=args.Source?.SourceName??"Unknown";if(!sourceCounts.TryGetValue(name,out var counts)){
                    if(sourceCounts.Count>=1023)name="Other sources";
                    if(!sourceCounts.TryGetValue(name,out counts)){counts=new long[2];sourceCounts.Add(name,counts);}
                }
                bool allowed=ConsoleFilter.Allows(args.Source?.SourceName,Map(args.Level));int slot=allowed?0:1;
                if(counts[slot]<long.MaxValue)counts[slot]++;
            }}
            public void Dispose(){}
        }
    }
}
