// Saga - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original. Reimplemented from ProgressivePowers 0.3.6 behavior only (README "How mastery works" and the
// Levels.yaml schema as user data; no code): kill requirements per power and level, and mastery from kill counts.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ExtraPotions.Saga.Conversion;

namespace ExtraPotions.Saga.Powers
{
    /// <summary>
    /// The kill requirements of design 4.3.3 (<c>Saga.PowerLevels.yml</c>). Each level lists the total repeat kills of
    /// each boss it needs; levels are cumulative, so a level is reached when it and every level below it are met. A level
    /// without an entry needs nothing more. A power has at most 100 levels.
    /// </summary>
    public sealed class PowerRequirements
    {
        public const int MaxLevels = 100;
        public const int DefaultLevels = 7;

        private readonly Dictionary<Boss, SortedDictionary<int, List<(Boss Boss, int Count)>>> table;
        private readonly Dictionary<Boss,SortedDictionary<int,List<(string Name,int Count)>>> additional=new Dictionary<Boss,SortedDictionary<int,List<(string,int)>>>();

        private PowerRequirements(Dictionary<Boss, SortedDictionary<int, List<(Boss, int)>>> table)
        {
            this.table = table;
        }

        /// <summary>Design 4.3.3 defaults (ProgressivePowers' default table).</summary>
        public static PowerRequirements Defaults { get; } = Build(new Dictionary<Boss, (Boss, int)[]>
        {
            [Boss.Eikthyr] = new[] { (Boss.Eikthyr, 1), (Boss.TheElder, 1), (Boss.Bonemass, 1), (Boss.Moder, 1), (Boss.Yagluth, 1), (Boss.Queen, 1), (Boss.Fader, 1) },
            [Boss.TheElder] = new[] { (Boss.TheElder, 1), (Boss.Bonemass, 1), (Boss.Moder, 1), (Boss.Yagluth, 1), (Boss.Queen, 1), (Boss.Fader, 1), (Boss.TheElder, 2) },
            [Boss.Bonemass] = new[] { (Boss.Bonemass, 1), (Boss.Moder, 1), (Boss.Yagluth, 1), (Boss.Queen, 1), (Boss.Fader, 1), (Boss.Bonemass, 2), (Boss.Fader, 2) },
            [Boss.Moder] = new[] { (Boss.Moder, 1), (Boss.Yagluth, 1), (Boss.Queen, 1), (Boss.Fader, 1), (Boss.Moder, 2), (Boss.Fader, 2), (Boss.Moder, 3) },
            [Boss.Yagluth] = new[] { (Boss.Yagluth, 1), (Boss.Queen, 1), (Boss.Fader, 1), (Boss.Yagluth, 2), (Boss.Fader, 2), (Boss.Yagluth, 3), (Boss.Fader, 3) },
            [Boss.Queen] = new[] { (Boss.Queen, 1), (Boss.Fader, 1), (Boss.Queen, 2), (Boss.Fader, 2), (Boss.Queen, 3), (Boss.Fader, 3), (Boss.Queen, 4) },
            [Boss.Fader] = new[] { (Boss.Fader, 1), (Boss.Fader, 2), (Boss.Fader, 3), (Boss.Fader, 4), (Boss.Fader, 5), (Boss.Fader, 6), (Boss.Fader, 7) },
            // Meadworks-original, the last Forsaken like Fader: its own kills only. The seven older powers keep their tables,
            // so existing characters keep the mastery they have.
            [Boss.Kall] = new[] { (Boss.Kall, 1), (Boss.Kall, 2), (Boss.Kall, 3), (Boss.Kall, 4), (Boss.Kall, 5), (Boss.Kall, 6), (Boss.Kall, 7) },
        });

        /// <summary>
        /// Powers added after the original seven. A file that does not mention one was written before it existed, so it
        /// takes the default requirements instead of needing nothing (which would grant full mastery for free).
        /// </summary>
        private static readonly Boss[] LaterPowers = { Boss.Kall };

        private static PowerRequirements Build(Dictionary<Boss, (Boss, int)[]> levels)
        {
            var table = new Dictionary<Boss, SortedDictionary<int, List<(Boss, int)>>>();
            foreach (var pair in levels)
            {
                var power = new SortedDictionary<int, List<(Boss, int)>>();
                for (var i = 0; i < pair.Value.Length; i++) power[i + 1] = new List<(Boss, int)> { pair.Value[i] };
                table[pair.Key] = power;
            }
            return new PowerRequirements(table);
        }

        /// <summary>Reads a <c>Saga.PowerLevels.yml</c> (or ProgressivePowers <c>Levels.yaml</c>) text; invalid entries are reported and skipped.</summary>
        public static PowerRequirements Parse(string? text, out List<string> errors) => Parse(text,Array.Empty<string>(),out errors);
        public static PowerRequirements Parse(string? text,IEnumerable<string> registeredBosses,out List<string> errors)
        {
            errors = new List<string>();
            var root = MiniYaml.Parse(text, errors);
            var table = new Dictionary<Boss, SortedDictionary<int, List<(Boss, int)>>>();
            var result=new PowerRequirements(table);
            var registered=new HashSet<string>(registeredBosses??Array.Empty<string>(),StringComparer.Ordinal);
            foreach (var powerEntry in root.Entries)
            {
                if (!PowerCatalog.TryParse(powerEntry.Key, out var power) || powerEntry.Key != PowerCatalog.Name(power))
                {
                    errors.Add("line " + powerEntry.Line + ": unknown power '" + powerEntry.Key + "'");
                    continue;
                }
                if (!table.TryGetValue(power, out var levels)) table[power] = levels = new SortedDictionary<int, List<(Boss, int)>>();
                foreach (var levelEntry in powerEntry.Value.Entries)
                {
                    if (!int.TryParse(levelEntry.Key, NumberStyles.None, CultureInfo.InvariantCulture, out var level) || level < 1 || level > MaxLevels)
                    {
                        errors.Add("line " + levelEntry.Line + ": " + power + " level must be 1 to " + MaxLevels + ", got '" + levelEntry.Key + "'");
                        continue;
                    }
                    var needs = new List<(Boss, int)>();
                    foreach (var need in levelEntry.Value.Entries)
                    {
                        if (!PowerCatalog.TryParse(need.Key, out var boss) || need.Key != PowerCatalog.Name(boss))
                        {
                            if(registered.Contains(need.Key)&&int.TryParse(need.Value.Scalar,NumberStyles.None,CultureInfo.InvariantCulture,out var extra)&&extra>=0) {
                                if(!result.additional.TryGetValue(power,out var more))result.additional[power]=more=new SortedDictionary<int,List<(string,int)>>();
                                if(!more.TryGetValue(level,out var list))more[level]=list=new List<(string,int)>();
                                list.Add((need.Key,extra));continue;
                            }
                            errors.Add("line " + levelEntry.Line + ": " + power + " level " + level + ": unknown boss '" + need.Key + "'");
                            continue;
                        }
                        if (!int.TryParse(need.Value.Scalar, NumberStyles.None, CultureInfo.InvariantCulture, out var count) || count < 0)
                        {
                            errors.Add("line " + levelEntry.Line + ": " + power + " level " + level + ": " + boss + " needs a whole number of kills");
                            continue;
                        }
                        needs.Add((boss, count));
                    }
                    levels[level] = needs;
                }
            }
            foreach (var later in LaterPowers)
                if (!table.ContainsKey(later)) table[later] = new SortedDictionary<int, List<(Boss, int)>>(Defaults.table[later].ToDictionary(p => p.Key, p => p.Value.ToList()));
            return result;
        }

        private IReadOnlyList<(Boss Boss, int Count)> Level(Boss power, int level) =>
            table.TryGetValue(power, out var levels) && levels.TryGetValue(level, out var needs) ? needs : (IReadOnlyList<(Boss, int)>)Array.Empty<(Boss, int)>();

        /// <summary>A level's own requirement as text, <c>Boss count, Boss count</c>; empty when it needs nothing.</summary>
        public string Describe(Boss power, int level) =>
            string.Join(", ", Level(power, level).Select(n => PowerCatalog.Name(n.Boss) + " " + n.Count.ToString(CultureInfo.InvariantCulture)).Concat(additional.TryGetValue(power,out var levels)&&levels.TryGetValue(level,out var more)?more.Select(n=>n.Name+" "+n.Count.ToString(CultureInfo.InvariantCulture)):Array.Empty<string>()));
        public IReadOnlyDictionary<string,int> NeededNamed(Boss power,int level,IReadOnlyDictionary<string,int> kills){var need=new Dictionary<string,int>();for(int n=1;n<=Math.Min(MaxLevels,level);n++){
            foreach(var item in Level(power,n)){var name=PowerCatalog.Name(item.Boss);need[name]=Math.Max(need.TryGetValue(name,out var count)?count:0,item.Count);}
            if(additional.TryGetValue(power,out var levels)&&levels.TryGetValue(n,out var more))foreach(var item in more)need[item.Name]=Math.Max(need.TryGetValue(item.Name,out var count)?count:0,item.Count);
        }return need.Select(p=>new KeyValuePair<string,int>(p.Key,p.Value-(kills.TryGetValue(p.Key,out var count)?count:0))).Where(p=>p.Value>0).ToDictionary(p=>p.Key,p=>p.Value);}

        private static int Have(IReadOnlyDictionary<Boss, int> kills, Boss boss) => kills != null && kills.TryGetValue(boss, out var n) ? n : 0;

        /// <summary>The mastery level: how many levels from 1 up are met, at most <paramref name="levelCount"/> (and 100).</summary>
        public int Mastery(Boss power, IReadOnlyDictionary<Boss, int> kills, int levelCount)
        {
            var max = Math.Min(MaxLevels, Math.Max(0, levelCount));
            for (var level = 1; level <= max; level++)
                if (Level(power, level).Any(n => Have(kills, n.Boss) < n.Count)) return level - 1;
            return max;
        }
        public int MasteryNamed(Boss power,IReadOnlyDictionary<string,int> kills,int levelCount) {
            var standard=new Dictionary<Boss,int>();foreach(Boss boss in Enum.GetValues(typeof(Boss)))standard[boss]=kills.TryGetValue(PowerCatalog.Name(boss),out var count)?count:0;
            var earned=Mastery(power,standard,levelCount);
            if(additional.TryGetValue(power,out var levels))foreach(var level in levels)
                if(level.Key<=earned&&level.Value.Any(n=>!kills.TryGetValue(n.Name,out var count)||count<n.Count))return level.Key-1;
            return earned;
        }

        /// <summary>Kills still needed to reach <paramref name="level"/>: per boss, the most any level up to it asks for, less what is held.</summary>
        public IReadOnlyDictionary<Boss, int> Needed(Boss power, int level, IReadOnlyDictionary<Boss, int> kills)
        {
            var most = new Dictionary<Boss, int>();
            for (var l = 1; l <= Math.Min(MaxLevels, level); l++)
                foreach (var (boss, count) in Level(power, l))
                    most[boss] = Math.Max(most.TryGetValue(boss, out var m) ? m : 0, count);
            return most.Select(p => (p.Key, Missing: p.Value - Have(kills, p.Key))).Where(p => p.Missing > 0)
                .OrderBy(p => p.Key).ToDictionary(p => p.Key, p => p.Missing);
        }

        /// <summary>Kill counts as text in game order, <c>Boss n, Boss n</c>; zero counts left out.</summary>
        public static string Format(IReadOnlyDictionary<Boss, int> counts) =>
            string.Join(", ", (counts ?? new Dictionary<Boss, int>()).Where(p => p.Value > 0).OrderBy(p => p.Key)
                .Select(p => PowerCatalog.Name(p.Key) + " " + p.Value.ToString(CultureInfo.InvariantCulture)));
    }
}
