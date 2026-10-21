// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using BepInEx.Logging;
using ExtraPotions.Meadworks.Crucible;

namespace ExtraPotions.Almanac.Runtime.Utilities
{
    public sealed class LoggingSource
    {
        internal LoggingSource(string source,ConsoleFilterLevel level,bool own,long written,long hidden){Source=source;Level=level;HasOwnRule=own;Written=written;Hidden=hidden;}
        public string Source { get; }
        public ConsoleFilterLevel Level { get; }
        public bool HasOwnRule { get; }
        public long Written { get; }
        public long Hidden { get; }
    }
    /// <summary>Local C06 controls and bounded source/count inspection. No log message is read or retained.</summary>
    public sealed class LoggingTools
    {
        readonly int thread=Thread.CurrentThread.ManagedThreadId;
        public LogVerbosity Level=>MeadworksLogging.Policy.Level;
        public IReadOnlyDictionary<string,LogVerbosity> ProductOverrides=>MeadworksLogging.Policy.Overrides;
        public bool Enabled=>MeadworksLogging.ConsoleFilter.Enabled;
        public IReadOnlyList<LoggingSource> Sources()
        {
            Check();var rules=MeadworksLogging.ConsoleFilter.Rules;
            var counts=MeadworksLogging.SourceCounters.ToDictionary(row=>row.Source,StringComparer.Ordinal);
            return Logger.Sources.Select(source=>source.SourceName).Concat(counts.Keys).Concat(rules.Keys).Append(ConsoleFilter.Wildcard)
                .Where(name=>!string.IsNullOrWhiteSpace(name)).Distinct(StringComparer.Ordinal).OrderBy(name=>name==ConsoleFilter.Wildcard?0:1)
                .ThenBy(name=>name,StringComparer.OrdinalIgnoreCase).Take(2048)
                .Select(name=>{counts.TryGetValue(name,out var count);return new LoggingSource(name,MeadworksLogging.ConsoleFilter.LevelFor(name),rules.ContainsKey(name),count?.Written??0,count?.Hidden??0);}).ToArray();
        }
        public void SetLevel(LogVerbosity value){Check();MeadworksLogging.SetLevel(value);}
        public void SetProduct(string product,LogVerbosity? value)
        {
            Check();var validation=new LogPolicy();if(value.HasValue)validation.SetOverride(product,value.Value);
            else if(string.IsNullOrWhiteSpace(product)||product.Length>128||product.IndexOfAny(new[]{'=',';',',','\r','\n','\0'})>=0)throw new ArgumentException("Invalid product name.",nameof(product));
            MeadworksLogging.SetProductLevel(product,value);
        }
        public void SetEnabled(bool value){Check();MeadworksLogging.SetConsoleEnabled(value);}
        public void SetDisk(bool value){Check();MeadworksLogging.SetFilterDisk(value);}
        public void SetSource(string source,ConsoleFilterLevel? value)
        {
            Check();var filter=new ConsoleFilter();filter.Load(MeadworksLogging.ConsoleFilter.Format());
            if(value.HasValue)filter.Set(source,value.Value);else filter.Remove(source);
            MeadworksLogging.SetConsoleRules(filter.Format());
        }
        public void SetRules(string text){Check();MeadworksLogging.SetConsoleRules(text);}
        void Check(){if(Thread.CurrentThread.ManagedThreadId!=thread)throw new InvalidOperationException("Logging controls must run on their owning thread.");}
    }
}
